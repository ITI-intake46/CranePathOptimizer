using CraneOptimizer.UI.Features.F10_ProjectManagement.Models;

namespace CraneOptimizer.UI.Features.F10_ProjectManagement.Services
{
    public interface IProjectWorkspaceService
    {
        ProjectSession? CurrentSession { get; }

        Task<ProjectSession> CreateProjectAsync(
            CreateProjectRequest request,
            CancellationToken cancellationToken);

        Task<IReadOnlyList<ProjectCardModel>> GetRecentProjectsAsync(CancellationToken cancellationToken);

        Task<ProjectSession?> OpenProjectAsync(
            Guid projectId,
            CancellationToken cancellationToken);

        Task DeleteProjectAsync(Guid projectId, CancellationToken cancellationToken);

        Task ToggleFavoriteAsync(Guid projectId, bool isFavorite, CancellationToken cancellationToken);

        Task<IReadOnlyList<ProjectCardModel>> SearchProjectsAsync(
            string searchText,
            bool favoritesOnly,
            CancellationToken cancellationToken);
    }
}
