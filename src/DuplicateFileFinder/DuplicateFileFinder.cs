
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;
using DuplicateFileFinder.Models;
using DuplicateFileFinder.Repositories;
using LiteDB;
using ShellProgressBar;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace DuplicateFileFinder
{
    public class DuplicateFileFinder
    {
        const string ConfigFileName = "config.yml";
        const string DatabaseFileName = "lite.db";
        private readonly Config _config;
        private readonly LiteDatabase _database;
        private readonly FileMetaRepository _fileMetaRepository;

        public DuplicateFileFinder(string configDirectoryPath)
        {
            _config = Config.LoadFile(Path.Combine(configDirectoryPath, ConfigFileName));
            _database = new LiteDatabase(Path.Combine(configDirectoryPath, DatabaseFileName));

            _fileMetaRepository = new FileMetaRepository(_database);
        }

        public async Task FindAndDeleteAsync(bool isDeleteEnabled)
        {
            var foundFiles = this.GetFiles();
            var filteredFiles = this.FilterFilesByDuplicateSize(foundFiles);
            var groupedFiles = this.GroupFilesByHashValue(filteredFiles);

            await Console.Out.WriteLineAsync("FoundFiles: " + foundFiles.Count());
            await Console.Out.WriteLineAsync("FilteredFiles: " + filteredFiles.Count());
            await Console.Out.WriteLineAsync("GroupedFiles: " + groupedFiles.Count());

            foreach (var files in groupedFiles)
            {
                var firstFile = files.First();
                await Console.Out.WriteLineAsync($"S {firstFile}");

                foreach (var deleteFile in files.Skip(1))
                {
                    await Console.Out.WriteLineAsync($"D {deleteFile}");
                    if (isDeleteEnabled)
                    {

                    }
                }
            }
        }

        private IEnumerable<string> GetFiles()
        {
            var results = new List<string>();
            var hashSet = new HashSet<string>();

            foreach (var pattern in _config.DirectoryPathPatterns ?? Array.Empty<string>())
            {
                foreach (var fileSystemInfo in Ganss.IO.Glob.Expand(pattern))
                {
                    bool isDirectory = fileSystemInfo.Attributes.HasFlag(FileAttributes.Directory);
                    if (isDirectory)
                    {
                        continue;
                    }

                    if (hashSet.Add(fileSystemInfo.FullName))
                    {
                        results.Add(fileSystemInfo.FullName);
                    }
                }
            }

            results.Sort();
            return results.ToArray();
        }

        private IEnumerable<string> FilterFilesByDuplicateSize(IEnumerable<string> files)
        {
            var options = new ProgressBarOptions
            {
                DisplayTimeInRealTime = false
            };
            using (var pbar = new ProgressBar(files.Count(), "FilterFilesByDuplicateSize", options))
            {
                var results = new HashSet<string>();
                var map = new Dictionary<long, string?>();

                foreach (var path in files)
                {
                    var key = new FileInfo(path).Length;
                    pbar.Tick();

                    if (!map.TryAdd(key, path))
                    {
                        results.Add(path);

                        var addedPath = map.GetValueOrDefault(key);
                        if (addedPath is not null)
                        {
                            results.Add(addedPath);
                        }
                        map[key] = null;
                    }
                }

                return results.ToArray();
            }
        }

        private IEnumerable<string[]> GroupFilesByHashValue(IEnumerable<string> files)
        {
            var options = new ProgressBarOptions
            {
                DisplayTimeInRealTime = false
            };
            using (var pbar = new ProgressBar(files.Count(), "GroupFilesByHashValue", options))
            {
                var map = new ConcurrentDictionary<byte[], List<string>>(new ByteArrayEqualityComparer());

                foreach (var path in files)
                {
                    var meta = this.GetFileMeta(path);
                    var list = map.GetOrAdd(meta.Sha256HashValue, _ => new List<string>());
                    list.Add(path);

                    pbar.Tick();
                }

                return map.Select(n => n.Value.ToArray()).ToArray();
            }
        }

        private FileMeta GetFileMeta(string filePath)
        {
            var meta = _fileMetaRepository.Get(filePath);

            if (meta == null)
            {
                var fileInfo = new FileInfo(filePath);

                using var stream = new FileStream(filePath, FileMode.Open);
                using var hash = SHA256.Create();
                var sha256HashValue = hash.ComputeHash(stream);

                meta = new FileMeta()
                {
                    FullPath = filePath,
                    LastWriteTimeUtc = fileInfo.LastWriteTimeUtc,
                    Sha256HashValue = sha256HashValue
                };

                _fileMetaRepository.Upsert(meta);
            }

            return meta;
        }

        private unsafe class ByteArrayEqualityComparer : IEqualityComparer<byte[]>
        {
            public bool Equals(byte[]? x, byte[]? y)
            {
                fixed (byte* px = x)
                fixed (byte* py = y)
                {
                    return Equals(px, py, x.Length);
                }
            }

            public int GetHashCode([DisallowNull] byte[] obj)
            {
                var hashCode = new HashCode();
                for (int i = Math.Min(obj.Length, 4) - 1; i >= 0; i--)
                {
                    hashCode.Add(obj[i]);
                }
                return hashCode.ToHashCode();
            }

            private static bool Equals(byte* source1, byte* source2, int length)
            {
                byte* t_x = source1, t_y = source2;

                for (int i = (length / 8) - 1; i >= 0; i--, t_x += 8, t_y += 8)
                {
                    if (*((long*)t_x) != *((long*)t_y))
                    {
                        return false;
                    }
                }

                if ((length & 4) != 0)
                {
                    if (*((int*)t_x) != *((int*)t_y))
                    {
                        return false;
                    }

                    t_x += 4;
                    t_y += 4;
                }

                if ((length & 2) != 0)
                {
                    if (*((short*)t_x) != *((short*)t_y))
                    {
                        return false;
                    }

                    t_x += 2;
                    t_y += 2;
                }

                if ((length & 1) != 0)
                {
                    if (*t_x != *t_y)
                    {
                        return false;
                    }
                }

                return true;
            }
        }
    }
}
