using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Models;
using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Settings;

namespace CraneOptimizer.Application.Optimization.GeneticAlgorithms.Interfaces
{
    /// <summary>
    /// Combines two parent solutions to produce a child solution.
    /// </summary>
    public interface ICrossoverOperator
    {
        /// <summary>
        /// Generates a new solution from two parents.
        /// </summary>
        CraneSolution Crossover(CraneSolution parent1, CraneSolution parent2, GASettings settings);
    }
}