using CraneOptimizer.UI.Features.F10_ProjectManagement.Models;

namespace CraneOptimizer.UI.Features.F10_ProjectManagement.Services
{
    public interface IProjectStateService
    {
        Task SaveProjectStateAsync(
            ProjectSession session,
            ProjectStateSnapshot state,
            CancellationToken cancellationToken = default);

        Task<ProjectStateSnapshot> LoadProjectStateAsync(
            ProjectSession session,
            CancellationToken cancellationToken = default);
    }
}
