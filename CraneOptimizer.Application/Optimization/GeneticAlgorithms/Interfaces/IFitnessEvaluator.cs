using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Models;
using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Settings;

namespace CraneOptimizer.Application.Optimization.GeneticAlgorithms.Interfaces
{
    /// <summary>
    /// Responsible for calculating the fitness score
    /// of a crane placement solution.
    /// </summary>
    public interface IFitnessEvaluator
    {
        /// <summary>
        /// Computes the fitness breakdown of a GA solution.
        /// </summary>
        FitnessBreakdown EvaluateFitness(
            CraneSolution solution,
            AssignmentResult assignment,
            GASettings settings);
    }
}