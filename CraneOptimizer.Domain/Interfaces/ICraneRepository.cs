using CraneOptimizer.Domain.Entities;

namespace CraneOptimizer.Domain.Interfaces
{
    public interface ICraneRepository
    {
        Task<List<Crane>> GetAllAsync();
        Task<List<Crane>> GetAllWithSpecsAsync();
        Task<Crane?> GetByIdWithSpecsAsync(int id);
        Task AddAsync(Crane crane);
        Task AddRangeAsync(List<Crane> cranes);
        Task UpdateAsync(Crane crane);
        Task DeleteAsync(int id);

        // Optional (important for CSV duplicate check)
        Task<bool> ExistsByNameAsync(string name);
    }
}
