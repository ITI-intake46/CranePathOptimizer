using CraneOptimizer.UI.Features.F10_ProjectManagement.Models;

namespace CraneOptimizer.UI.Features.F10_ProjectManagement.Services
{
    public interface IProjectService
    {
        Task<ProjectCardModel> CreateProjectAsync(string name, CancellationToken cancellationToken);
        Task<IReadOnlyList<ProjectCardModel>> GetRecentProjectsAsync(CancellationToken cancellationToken);
        Task<ProjectCardModel?> OpenProjectAsync(Guid projectId, CancellationToken cancellationToken);
        Task DeleteProjectAsync(Guid projectId, CancellationToken cancellationToken);
        Task ToggleFavoriteAsync(Guid projectId, bool isFavorite, CancellationToken cancellationToken);
        Task<IReadOnlyList<ProjectCardModel>> SearchProjectsAsync(string searchText, bool favoritesOnly, CancellationToken cancellationToken);
    }
}
