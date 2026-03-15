using CraneOptimizer.Application.Optimization.GeneticAlgorithm.Models;
using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Interfaces;
using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Models;

namespace CraneOptimizer.Application.Optimization.GeneticAlgorithms.Services
{
    /// <summary>
    /// Replacement strategy used to build the next generation.
    /// Combines old population with new children.
    /// </summary>
    public class ReplacementStrategy : IReplacementStrategy
    {
        /// <summary>
        /// Replaces the current population with a new generation.
        /// </summary>
        public Population Replace(Population population, List<CraneSolution> children)
        {
            // TODO:
            // 1️⃣ Combine old population with children

            // TODO:
            // 2️⃣ Sort individuals by fitness

            // TODO:
            // 3️⃣ Keep best individuals up to population size

            // TODO:
            // 4️⃣ Create new population object

            return population;
        }
    }
}