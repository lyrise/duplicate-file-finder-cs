using DuplicateFileFinder.Models;
using LiteDB;

namespace DuplicateFileFinder.Repositories
{
    public class FileMetaRepository
    {
        private readonly LiteDatabase _database;

        private readonly object _lockObject = new();

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

        public IEnumerable<FileMeta> FindAll()
        {
            lock (_lockObject)
            {
                var col = this.GetCollection();
                return col.FindAll();
            }
        }

        public FileMeta? Find(string fullPath)
        {
            lock (_lockObject)
            {
                var col = this.GetCollection();
                return col.Find(n => n.FullPath == fullPath).FirstOrDefault();
            }
        }

        public void Upsert(FileMeta meta)
        {
            lock (_lockObject)
            {
                var col = this.GetCollection();
                col.DeleteMany(n => n.FullPath == meta.FullPath);
                col.Insert(meta);
            }
        }
    }
}
