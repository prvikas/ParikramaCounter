using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using ParikramaCounter.Models;

namespace ParikramaCounter.Repositories
{
    // Issue #7: GetLatestAsync now reads only the last line of the file — O(1)
    // regardless of how many sessions have been saved. Uses a reverse file scan
    // rather than loading and deserialising every record.
    public class JsonSessionRepository : ISessionRepository
    {
        private static readonly string FilePath =
            Path.Combine(FileSystem.AppDataDirectory, "sessions.ndjson");

        private readonly SemaphoreSlim _lock = new SemaphoreSlim(1, 1);

        public async Task SaveAsync(SessionRecord session)
        {
            session.Finalise();   // Issue #8: capture computed values before serialising
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
                var result = new List<SessionRecord>(lines.Length);
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

        // Issue #7: reads only the last non-empty line — O(file-tail), not O(n).
        // Uses a reverse byte scan to find the final newline-delimited record
        // without loading the whole file into memory.
        public async Task<SessionRecord?> GetLatestAsync()
        {
            if (!File.Exists(FilePath)) return null;
            await _lock.WaitAsync();
            try
            {
                return await ReadLastLineAsync();
            }
            finally { _lock.Release(); }
        }

        private static async Task<SessionRecord?> ReadLastLineAsync()
        {
            await using var fs = new FileStream(FilePath, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, useAsync: true);
            if (fs.Length == 0) return null;

            // Scan backward skipping trailing newlines, then find the previous newline
            long pos = fs.Length - 1;
            while (pos >= 0)
            {
                fs.Seek(pos, SeekOrigin.Begin);
                int b = fs.ReadByte();
                if (b != '\n' && b != '\r') break;
                pos--;
            }
            long lineEnd = pos + 1;

            // Now scan back to find the start of this line
            while (pos >= 0)
            {
                fs.Seek(pos, SeekOrigin.Begin);
                int b = fs.ReadByte();
                if (b == '\n' || b == '\r') { pos++; break; }
                pos--;
            }
            if (pos < 0) pos = 0;

            int length = (int)(lineEnd - pos);
            if (length <= 0) return null;

            var buffer = new byte[length];
            fs.Seek(pos, SeekOrigin.Begin);
            int read = await fs.ReadAsync(buffer, 0, length);
            var json = Encoding.UTF8.GetString(buffer, 0, read).Trim();

            try { return JsonSerializer.Deserialize<SessionRecord>(json); }
            catch { return null; }
        }
    }
}
