using System.Collections.Generic;
using System.Threading.Tasks;
using ParikramaCounter.Domain;

namespace ParikramaCounter.Repositories
{
    // Temples are persistent entities the user builds up over time.
    // Each temple accumulates heading data from every session at that location,
    // which drives future accuracy improvements for that specific sanctum.
    public interface ITempleRepository
    {
        Task<IReadOnlyList<Temple>> GetAllAsync();
        Task<Temple?> GetByIdAsync(string id);
        Task SaveAsync(Temple temple);
        Task DeleteAsync(string id);
    }
}
