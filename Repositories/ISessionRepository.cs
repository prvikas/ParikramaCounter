using System.Collections.Generic;
using System.Threading.Tasks;
using ParikramaCounter.Models;

namespace ParikramaCounter.Repositories
{
    // Fix #12: session history persistence contract.
    // Currently implemented with JSON in AppDataDirectory.
    // Swappable with SQLite or a backend API without changing consumers.
    public interface ISessionRepository
    {
        Task SaveAsync(SessionRecord session);
        Task<IReadOnlyList<SessionRecord>> GetAllAsync();
        Task<SessionRecord?> GetLatestAsync();
    }
}
