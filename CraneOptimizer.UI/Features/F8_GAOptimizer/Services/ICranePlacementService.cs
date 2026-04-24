using CraneOptimizer.Application.Features.CranePlacement.DTOs;
using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Settings;
using CraneOptimizer.Domain.Entities;

namespace CraneOptimizer.UI.Features.F8_GAOptimizer.Services
{
    public interface ICranePlacementService
    {
        Task<CranePlacementResult> RunOptimizationAsync(GASettings settings, Crane crane);
    }
}
