using CraneOptimizer.Domain.Entities;

namespace CraneOptimizer.Domain.Interfaces
{
    public interface ICraneRepository
    {
        Task<List<Crane>> GetAllAsync();
        Task<Crane?> GetByIdWithSpecsAsync(int id);
    }
}
