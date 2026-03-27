using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Models;
using CraneOptimizer.Domain.GreenAreaEntities;
using CraneOptimizer.Domain.IfcEntities;

namespace CraneOptimizer.Application.Optimization.GeneticAlgorithms.Interfaces
{
    /// <summary>
    /// Responsible for generating the initial population
    /// for the Genetic Algorithm.
    /// Supports both pure random and heuristic-guided initialization.
    /// </summary>
    public interface IGAInitializer
    {
        /// <summary>
        /// Creates the first population of crane placement solutions.
        /// 
        /// Strategy:
        ///   70% of individuals → pure random positions inside zones
        ///   30% of individuals → heuristic positions guided by element centroids
        /// 
        /// The heuristic individuals seed the population with smarter starting
        /// points, accelerating GA convergence without losing diversity.
        /// </summary>
        /// <param name="zones">
        /// List of crane placement zones defined by the user.
        /// All generated crane positions must remain inside these zones.
        /// </param>
        /// <param name="elements">
        /// Precast elements extracted from the IFC model.
        /// Used by the heuristic strategy to compute element centroids.
        /// </param>
        /// <param name="populationSize">
        /// Total number of individuals in the initial population.
        /// </param>
        /// <param name="maxCranePositions">
        /// Maximum number of crane setup positions allowed per chromosome.
        /// Each chromosome will have between 1 and maxCranePositions positions.
        /// </param>
        /// <returns>
        /// Initialized population ready for GA evaluation.
        /// </returns>
        Population InitializePopulation(
            List<CranePlacementZone> zones,
            List<PrecastElement> elements,
            int populationSize,
            int maxCranePositions);
    }
}