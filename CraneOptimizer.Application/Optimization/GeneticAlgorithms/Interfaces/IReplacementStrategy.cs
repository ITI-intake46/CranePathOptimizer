using CraneOptimizer.Application.Optimization.GeneticAlgorithm.Models;
using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Models;

namespace CraneOptimizer.Application.Optimization.GeneticAlgorithms.Interfaces
{
    /// <summary>
    /// Responsible for creating the next generation.
    /// </summary>
    public interface IReplacementStrategy
    {
        /// <summary>
        /// Replaces the old population with the new offspring.
        /// </summary>
        Population Replace(Population oldPopulation, List<CraneSolution> offspring);
    }
}