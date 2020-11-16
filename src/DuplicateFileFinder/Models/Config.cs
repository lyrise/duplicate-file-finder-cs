using System.IO;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace DuplicateFileFinder.Models
{
    public record Config
    {
        public string[]? DirectoryPathPatterns;

        public static Config LoadFile(string path)
        {
            var deserializer = new DeserializerBuilder()
                .WithNamingConvention(CamelCaseNamingConvention.Instance)
                .Build();

            using var reader = new StreamReader(path);
            var result = deserializer.Deserialize<Config>(reader);
            return result;
        }
    }
}
