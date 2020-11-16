using System;
using System.IO;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace DuplicateFileFinder.Models
{
    public class FileMeta
    {
        public string? FullPath { get; set; }

        public DateTime LastWriteTimeUtc { get; set; }

        public byte[]? Sha256HashValue { get; set; }
    }
}
