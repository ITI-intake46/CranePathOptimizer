using CraneOptimizer.Domain.Units;
using CraneOptimizer.UI.Features.F10_ProjectManagement.Models;

namespace CraneOptimizer.UI.Features.F10_ProjectManagement.Services
{
    public interface IProjectWorkspaceService
    {
        ProjectSession? CurrentSession { get; }

        Task<ProjectSession> CreateProjectAsync(
            string name,
            UnitSettings unitSettings,
            CancellationToken cancellationToken);

        Task<ProjectSession?> OpenProjectAsync(
            Guid projectId,
            CancellationToken cancellationToken);
    }
}
