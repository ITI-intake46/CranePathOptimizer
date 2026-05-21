using CraneOptimizer.UI.Features.F10_ProjectManagement.Models;

namespace CraneOptimizer.UI.Features.F10_ProjectManagement.Services
{
    public sealed class ProjectService : IProjectService
    {
        private readonly IProjectWorkspaceService _workspaceService;

        public ProjectService(IProjectWorkspaceService workspaceService)
        {
            _workspaceService = workspaceService;
        }

        public async Task<IReadOnlyList<ProjectCardModel>> GetRecentProjectsAsync(CancellationToken cancellationToken)
        {
            return await _workspaceService.GetRecentProjectsAsync(cancellationToken).ConfigureAwait(false);
        }

        public async Task<ProjectCardModel?> OpenProjectAsync(Guid projectId, CancellationToken cancellationToken)
        {
            var session = await _workspaceService.OpenProjectAsync(projectId, cancellationToken).ConfigureAwait(false);
            return session?.ProjectCard;
        }

        public async Task DeleteProjectAsync(Guid projectId, CancellationToken cancellationToken)
        {
            await _workspaceService.DeleteProjectAsync(projectId, cancellationToken).ConfigureAwait(false);
        }

        public async Task ToggleFavoriteAsync(Guid projectId, bool isFavorite, CancellationToken cancellationToken)
        {
            await _workspaceService.ToggleFavoriteAsync(projectId, isFavorite, cancellationToken).ConfigureAwait(false);
        }

        public async Task<IReadOnlyList<ProjectCardModel>> SearchProjectsAsync(
            string searchText,
            bool favoritesOnly,
            CancellationToken cancellationToken)
        {
            return await _workspaceService.SearchProjectsAsync(searchText, favoritesOnly, cancellationToken).ConfigureAwait(false);
        }
    }
}
