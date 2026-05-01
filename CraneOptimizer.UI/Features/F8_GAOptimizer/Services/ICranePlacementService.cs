using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Settings;
using CraneOptimizer.Domain.Entities;
using CraneOptimizer.UI.Features.F8_GAOptimizer.DTOs;
using System.Threading;

namespace CraneOptimizer.UI.Features.F8_GAOptimizer.Services
{
    public interface ICranePlacementService
    {
        Task<CranePlacementResult> RunOptimizationAsync(
            GASettings settings,
            Crane crane,
            CancellationToken cancellationToken = default);
    }
}
