using CraneOptimizer.Application.Features.CranePlacement.DTOs;
using CraneOptimizer.Application.Optimization.GeneticAlgorithms.DTOs;

namespace CraneOptimizer.UI.Features.F6_CranePlacement.Services
{
    internal interface ICranePlacementService
    {
        Task<CranePlacementResult> RunOptimizationAsync(PlacementInput input);
    }
}
