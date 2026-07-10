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

        Task<ProjectSession> OpenProjectFolderAsync(
            string projectFolderPath,
            CancellationToken cancellationToken);

        Task DeleteProjectAsync(Guid projectId, CancellationToken cancellationToken);

        Task ToggleFavoriteAsync(Guid projectId, bool isFavorite, CancellationToken cancellationToken);

        Task<IReadOnlyList<ProjectCardModel>> SearchProjectsAsync(
            string searchText,
            bool favoritesOnly,
            CancellationToken cancellationToken);

        Task UpdateProjectMetadataAsync(
            Guid projectId,
            string name,
            string? companyName,
            string? country,
            string? siteLocation,
            string? projectImagePath,
            CancellationToken cancellationToken);

        /// <summary>
        /// Renames the physical project folder on disk, then updates the global catalog
        /// and the per-project database with the new name and path.
        /// The ProjectId is never changed.
        /// </summary>
        /// <returns>The new absolute folder path after successful rename.</returns>
        Task<string> RenameProjectFolderAsync(
            Guid projectId,
            string newProjectName,
            string? companyName,
            string? country,
            string? siteLocation,
            string? projectImagePath,
            CancellationToken cancellationToken);
    }
}
