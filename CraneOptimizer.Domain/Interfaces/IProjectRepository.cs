using CraneOptimizer.Domain.Entities;

namespace CraneOptimizer.Domain.Interfaces
{
    public interface IProjectRepository
    {
        Task CreateAsync(Project project, CancellationToken ct);
        Task<List<Project>> GetAllAsync(CancellationToken ct);
        Task<List<Project>> GetRecentAsync(int count, CancellationToken ct);
        Task<Project?> GetByIdAsync(Guid id, CancellationToken ct);
        Task<bool> SoftDeleteAsync(Guid id, CancellationToken ct);
        Task OpenAsync(Guid id, DateTime openedAtUtc, CancellationToken ct);
        Task<bool> ToggleFavoriteAsync(Guid id, bool isFavorite, DateTime modifiedAtUtc, CancellationToken ct);
    }
}