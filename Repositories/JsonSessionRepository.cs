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

            // Read the whole file tail (last 8KB is more than enough for one JSON line)
            // then find the last non-empty line entirely in memory.
            // This avoids synchronous ReadByte() calls on an async FileStream,
            // which is unreliable on iOS/Android with overlapped I/O.
            const int tailSize = 8192;
            long readFrom = Math.Max(0, fs.Length - tailSize);
            int bufLen    = (int)(fs.Length - readFrom);
            var buf       = new byte[bufLen];

            fs.Seek(readFrom, SeekOrigin.Begin);
            int totalRead = 0;
            while (totalRead < bufLen)
            {
                int n = await fs.ReadAsync(buf, totalRead, bufLen - totalRead);
                if (n == 0) break;
                totalRead += n;
            }

            // Find the last non-empty line in the buffer
            var text  = Encoding.UTF8.GetString(buf, 0, totalRead);
            var lines = text.Split('\n');

            for (int i = lines.Length - 1; i >= 0; i--)
            {
                var line = lines[i].Trim('\r', ' ');
                if (string.IsNullOrWhiteSpace(line)) continue;
                try { return JsonSerializer.Deserialize<SessionRecord>(line); }
                catch { return null; }
            }
            return null;
        }
    }
}
