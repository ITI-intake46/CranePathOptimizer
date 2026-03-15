using CraneOptimizer.Application.Optimization.GeneticAlgorithm.Models;
using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Models;
using CraneOptimizer.Domain.GreenAreaEntities;

namespace CraneOptimizer.Application.Optimization.GeneticAlgorithms.Interfaces
{
    /// <summary>
    /// Responsible for generating the initial population
    /// for the Genetic Algorithm.
    /// </summary>
    public interface IGAInitializer
    {
        /// <summary>
        /// Creates the first population of crane placement solutions.
        /// Random crane positions are generated inside the allowed crane zones.
        /// </summary>
        /// <param name="zones">
        /// List of crane placement zones defined by the user.
        /// </param>
        /// <param name="populationSize">
        /// Number of individuals in the population.
        /// </param>
        /// <param name="maxCranePositions">
        /// Maximum number of crane setups allowed per chromosome.
        /// </param>
        /// <returns>
        /// Initialized population ready for GA evaluation.
        /// </returns>
        Population InitializePopulation(
            List<CranePlacementZone> zones,
            int populationSize,
            int maxCranePositions);
    }
}