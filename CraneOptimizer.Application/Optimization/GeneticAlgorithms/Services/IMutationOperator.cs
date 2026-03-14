using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Models;

namespace CraneOptimizer.Application.Optimization.GeneticAlgorithms.Services
{
    /// <summary>
    /// Applies random modification to a solution
    /// to maintain population diversity.
    /// </summary>
    public interface IMutationOperator
    {
        /// <summary>
        /// Mutates the solution position slightly.
        /// </summary>
        void Mutate(CraneSolution solution);
    }
}