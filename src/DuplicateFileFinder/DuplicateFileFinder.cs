using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Security.Cryptography;
using DuplicateFileFinder.Models;
using DuplicateFileFinder.Repositories;
using LiteDB;
using ShellProgressBar;

namespace DuplicateFileFinder
{
    public class DuplicateFileFinder
    {
        const string ConfigFileName = "config.yml";
        const string DatabaseFileName = "lite.db";
        private readonly Config _config;
        private readonly LiteDatabase _database;
        private readonly FileMetaRepository _fileMetaRepo;

        private readonly object _lockObject = new();

        public DuplicateFileFinder(string configDirectoryPath)
        {
            _config = Config.LoadFile(Path.Combine(configDirectoryPath, ConfigFileName));
            _database = new LiteDatabase(Path.Combine(configDirectoryPath, DatabaseFileName));
            _fileMetaRepo = new FileMetaRepository(_database);
        }

        public async Task FindAndDeleteAsync(bool isDeleteEnabled, CancellationToken cancellationToken = default)
        {
            var foundFiles = this.GetFiles(cancellationToken);
            var filteredFiles = this.FilterFilesByDuplicateSize(foundFiles, cancellationToken);
            var groupedFiles = await this.GroupFilesByHashValueAsync(filteredFiles, cancellationToken);

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
                        File.Delete(deleteFile);
                    }
                }
            }
        }

        private IEnumerable<string> GetFiles(CancellationToken cancellationToken = default)
        {
            var results = new List<string>();
            var hashSet = new HashSet<string>();

            foreach (var targetDir in _config.Targets ?? Array.Empty<string>())
            {
                var tempResults = new List<string>();

                foreach (var path in Directory.EnumerateFiles(targetDir, "*", new EnumerationOptions() { RecurseSubdirectories = true }))
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    if (hashSet.Add(path))
                    {
                        tempResults.Add(path);
                    }
                }

                tempResults.Sort();
                results.AddRange(tempResults);
            }

            return results;
        }

        private IEnumerable<string> FilterFilesByDuplicateSize(IEnumerable<string> files, CancellationToken cancellationToken = default)
        {
            var options = new ProgressBarOptions
            {
                ProgressCharacter = '#',
                ProgressBarOnBottom = true
            };
            using var pbar = new ProgressBar(files.Count(), "Initial message", options);
            var map = new ConcurrentDictionary<long, List<string>>();

            foreach (var (i, path) in files.Select((n, i) => (i, n)))
            {
                cancellationToken.ThrowIfCancellationRequested();
                pbar.Tick(i, $"Compare file sizes {pbar.CurrentTick} / {pbar.MaxTicks}");

                var key = new FileInfo(path).Length;
                var list = map.GetOrAdd(key, _ => new List<string>());
                list.Add(path);
            }

            return map.Where(n => n.Value.Count > 1).SelectMany(n => n.Value).ToArray();
        }

        private async ValueTask<IEnumerable<string[]>> GroupFilesByHashValueAsync(IEnumerable<string> files, CancellationToken cancellationToken = default)
        {
            var options = new ProgressBarOptions
            {
                ProgressCharacter = '#',
                ProgressBarOnBottom = true
            };
            using var pbar = new ProgressBar(files.Count(), "Initial message", options);
            var map = new ConcurrentDictionary<byte[], ConcurrentQueue<string>>(new ByteArrayEqualityComparer());

            var lockObject = new object();
            int current = 0;

            var parallelOptions = new ParallelOptions() { MaxDegreeOfParallelism = 2 };
            await Parallel.ForEachAsync(files, parallelOptions, async (path, token) =>
            {
                cancellationToken.ThrowIfCancellationRequested();

                lock (lockObject)
                {
                    pbar.Tick(++current, $"Compare file hashes {pbar.CurrentTick} / {pbar.MaxTicks}");
                }

                var meta = this.GetFileMeta(path);
                if (meta is null) return;

                var queue = map.GetOrAdd(meta.Sha256HashValue!, _ => new ConcurrentQueue<string>());
                queue.Enqueue(path);
            });

            return map.Where(n => n.Value.Count > 1).Select(n => n.Value.ToArray()).ToArray();
        }


        private FileMeta? GetFileMeta(string filePath)
        {
            var meta = _fileMetaRepo.Find(filePath);
            if (meta is not null) return meta;

            try
            {
                var fileInfo = new FileInfo(filePath);

                using var stream = new FileStream(filePath, FileMode.Open);
                using var hash = SHA256.Create();
                var sha256HashValue = hash.ComputeHash(stream);

                meta = new FileMeta()
                {
                    FullPath = filePath,
                    LastWriteTime = fileInfo.LastWriteTimeUtc,
                    Sha256HashValue = sha256HashValue
                };
            }
            catch (UnauthorizedAccessException)
            {
                return null;
            }

            _fileMetaRepo.Upsert(meta);

            return meta;
        }

        private unsafe class ByteArrayEqualityComparer : IEqualityComparer<byte[]>
        {
            public bool Equals(byte[]? x, byte[]? y)
            {
                if (x is null || y is null)
                {
                    if (x is null && y is null)
                    {
                        return true;
                    }

                    return false;
                }

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
