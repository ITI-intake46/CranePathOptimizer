using CraneOptimizer.Domain.Interfaces;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace CraneOptimizer.Application.Services
{
    public class ProjectMaterialSettingsService : IProjectMaterialSettingsService
    {
        public const double DefaultConcreteDensityKgPerM3 = 2500.0;

        private readonly IProjectRepository _projectRepo;

        public ProjectMaterialSettingsService(IProjectRepository projectRepo)
        {
            _projectRepo = projectRepo;
        }

        public async Task<double> GetConcreteDensityKgPerM3Async(Guid projectId, CancellationToken ct = default)
        {
            var project = await _projectRepo.GetProjectByIdAsync(projectId, ct).ConfigureAwait(false);
            if (project == null)
                return DefaultConcreteDensityKgPerM3;

            return project.ConcreteDensityKgPerM3 > 0
                ? project.ConcreteDensityKgPerM3
                : DefaultConcreteDensityKgPerM3;
        }

        public Task SetConcreteDensityKgPerM3Async(Guid projectId, double density, CancellationToken ct = default)
        {
            if (density <= 0)
                throw new ArgumentOutOfRangeException(nameof(density), "Density must be > 0");

            return _projectRepo.UpdateConcreteDensityAsync(projectId, density, ct);
        }
    }
}
