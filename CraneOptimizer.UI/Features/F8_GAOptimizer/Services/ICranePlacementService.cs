using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Settings;
using CraneOptimizer.Domain.Entities;
using CraneOptimizer.UI.Features.F8_GAOptimizer.DTOs;

namespace CraneOptimizer.UI.Features.F8_GAOptimizer.Services
{
    public interface ICranePlacementService
    {
        Task<CranePlacementResult> RunOptimizationAsync(GASettings settings, Crane crane);
    }
}
