using CraneOptimizer.Application.Optimization.GeneticAlgorithm.Models;
using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Models;

namespace CraneOptimizer.Application.Optimization.GeneticAlgorithms.Services
{
    /// <summary>
    /// Selects parent solutions from the population.
    /// </summary>
    public interface ISelectionStrategy
    {
        /// <summary>
        /// Selects individuals that will participate in crossover.
        /// </summary>
        List<CraneSolution> SelectParents(Population population, int numberOfParents);
    }
}