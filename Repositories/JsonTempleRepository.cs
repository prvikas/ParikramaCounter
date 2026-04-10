using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using ParikramaCounter.Domain;

namespace ParikramaCounter.Repositories
{
    // Each temple is a named JSON file in AppDataDirectory/temples/.
    // The directory approach (one file per temple) avoids re-writing the entire
    // collection on every heading update, which happens at session end.
    public class JsonTempleRepository : ITempleRepository
    {
        private static readonly string Dir =
            Path.Combine(FileSystem.AppDataDirectory, "temples");

        private readonly SemaphoreSlim _lock = new SemaphoreSlim(1, 1);
        private readonly JsonSerializerOptions _opts =
            new JsonSerializerOptions { WriteIndented = false };

        private static string FilePath(string id)
            => Path.Combine(Dir, $"{id}.json");

        public async Task<IReadOnlyList<Temple>> GetAllAsync()
        {
            await _lock.WaitAsync();
            try
            {
                Directory.CreateDirectory(Dir);
                var files  = Directory.GetFiles(Dir, "*.json");
                var result = new List<Temple>(files.Length);
                foreach (var f in files)
                {
                    try
                    {
                        var json = await File.ReadAllTextAsync(f);
                        var t    = JsonSerializer.Deserialize<Temple>(json);
                        if (t != null) result.Add(t);
                    }
                    catch { /* skip corrupt file */ }
                }
                return result.OrderBy(t => t.Name).ToList();
            }
            finally { _lock.Release(); }
        }

        public async Task<Temple?> GetByIdAsync(string id)
        {
            await _lock.WaitAsync();
            try
            {
                var path = FilePath(id);
                if (!File.Exists(path)) return null;
                var json = await File.ReadAllTextAsync(path);
                return JsonSerializer.Deserialize<Temple>(json);
            }
            finally { _lock.Release(); }
        }

        public async Task SaveAsync(Temple temple)
        {
            temple.UpdatedAt = DateTime.UtcNow;
            await _lock.WaitAsync();
            try
            {
                Directory.CreateDirectory(Dir);
                var json = JsonSerializer.Serialize(temple, _opts);
                await File.WriteAllTextAsync(FilePath(temple.Id), json);
            }
            finally { _lock.Release(); }
        }

        public async Task DeleteAsync(string id)
        {
            await _lock.WaitAsync();
            try
            {
                var path = FilePath(id);
                if (File.Exists(path)) File.Delete(path);
            }
            finally { _lock.Release(); }
        }
    }
}
