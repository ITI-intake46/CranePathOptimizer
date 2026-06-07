using System;
using System.Threading;
using System.Threading.Tasks;

namespace CraneOptimizer.Application.Services
{
    public interface IProjectMaterialSettingsService
    {
        Task<double> GetConcreteDensityKgPerM3Async(Guid projectId, CancellationToken ct = default);

        Task SetConcreteDensityKgPerM3Async(Guid projectId, double density, CancellationToken ct = default);
    }
}
