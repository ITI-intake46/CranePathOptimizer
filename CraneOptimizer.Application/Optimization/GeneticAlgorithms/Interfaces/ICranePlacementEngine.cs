using CraneOptimizer.Application.Optimization.GeneticAlgorithms.DTOs;
using System.Threading;

namespace CraneOptimizer.Application.Optimization.GeneticAlgorithms.Interfaces
{
    public interface ICranePlacementEngine
    {
        PlacementResult RunOptimization(PlacementInput input, CancellationToken cancellationToken = default);
    }
}
