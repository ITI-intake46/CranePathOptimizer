using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Models;
using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Settings;
using CraneOptimizer.Domain.Schemas;

namespace CraneOptimizer.Application.Optimization.GeneticAlgorithms.Interfaces
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
        void Mutate(
            CraneSolution solution,
            List<GreenAreaZone> zones,
            List<GreenAreaZone> forbiddenZones,
            GASettings settings);
    }
}
