using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using ParikramaCounter.Models;

namespace ParikramaCounter.Repositories
{
    // Fix #12: lightweight JSON-file session store.
    // Appends each session as a line of JSON (newline-delimited JSON).
    // No dependency on SQLite — easy to replace later.
    public class JsonSessionRepository : ISessionRepository
    {
        private static readonly string FilePath =
            Path.Combine(FileSystem.AppDataDirectory, "sessions.ndjson");

        private readonly SemaphoreSlim _lock = new SemaphoreSlim(1, 1);

        public async Task SaveAsync(SessionRecord session)
        {
            await _lock.WaitAsync();
            try
            {
                var line = JsonSerializer.Serialize(session) + Environment.NewLine;
                await File.AppendAllTextAsync(FilePath, line);
            }
            finally { _lock.Release(); }
        }

        public async Task<IReadOnlyList<SessionRecord>> GetAllAsync()
        {
            if (!File.Exists(FilePath)) return Array.Empty<SessionRecord>();
            await _lock.WaitAsync();
            try
            {
                var lines  = await File.ReadAllLinesAsync(FilePath);
                var result = new List<SessionRecord>();
                foreach (var line in lines)
                {
                    if (string.IsNullOrWhiteSpace(line)) continue;
                    try { result.Add(JsonSerializer.Deserialize<SessionRecord>(line)!); }
                    catch { /* skip corrupt lines */ }
                }
                return result;
            }
            finally { _lock.Release(); }
        }

        public async Task<SessionRecord?> GetLatestAsync()
        {
            var all = await GetAllAsync();
            return all.Count > 0 ? all[all.Count - 1] : null;
        }
    }
}
