using System.Collections.Generic;
using System.IO;
using System.Linq;
using DuplicateFileFinder.Models;
using LiteDB;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace DuplicateFileFinder.Repositories
{
    public class FileMetaRepository
    {
        private readonly LiteDatabase _database;

        public FileMetaRepository(LiteDatabase database)
        {
            _database = database;

            var collection = this.GetCollection();
            collection.EnsureIndex(n => n.FullPath, true);
        }

        private ILiteCollection<FileMeta> GetCollection()
        {
            return _database.GetCollection<FileMeta>("file_meta");
        }

        public IEnumerable<FileMeta> GetAll()
        {
            var collection = this.GetCollection();
            return collection.FindAll();
        }

        public FileMeta? Get(string fullPath)
        {
            var collection = this.GetCollection();
            return collection.Find(n => n.FullPath == fullPath).FirstOrDefault();
        }

        public void Upsert(FileMeta meta)
        {
            var collection = this.GetCollection();
            collection.Upsert(meta);
        }
    }
}
