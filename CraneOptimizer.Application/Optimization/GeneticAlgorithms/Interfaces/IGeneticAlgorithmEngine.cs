using CraneOptimizer.Application.Optimization.GeneticAlgorithms.DTOs;
using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Models;
using System.Threading;

namespace CraneOptimizer.Application.Optimization.GeneticAlgorithms.Interfaces
{
    /// <summary>
    /// Main Genetic Algorithm engine.
    /// Coordinates all GA steps.
    /// </summary>
    public interface IGeneticAlgorithmEngine
    {
        /// <summary>
        /// Executes the optimization process.
        /// </summary>
        CraneSolution Run(PlacementInput input, CancellationToken cancellationToken = default);
    }
}
