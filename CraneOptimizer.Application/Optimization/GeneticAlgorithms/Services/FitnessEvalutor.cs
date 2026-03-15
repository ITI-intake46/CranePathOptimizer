using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Interfaces;
using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Models;
using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Settings;

namespace CraneOptimizer.Application.Optimization.GeneticAlgorithms.Services
{
    /// <summary>
    /// Computes the fitness value of a GA solution
    /// based on lifting feasibility and penalties.
    /// </summary>
    public class FitnessEvaluator : IFitnessEvaluator
    {
        public FitnessBreakdown EvaluateFitness(
            CraneSolution solution,
            AssignmentResult assignment,
            GASettings settings)
        {
            var breakdown = new FitnessBreakdown();

            // ----------------------------
            // 1️⃣ Coverage calculation
            // ----------------------------

            // coverage = liftable elements / total elements

            // TODO: count liftable elements from assignment.EvaluationResults


            // ----------------------------
            // 2️⃣ Relocation penalty
            // ----------------------------

            // simple relocation penalty based on crane positions count

            // TODO:
            // relocationPenalty = solution.Chromosome.CranePositions.Count


            // ----------------------------
            // 3️⃣ Radius penalty
            // ----------------------------

            // TODO:
            // sum penalties where status == OutOfRadius


            // ----------------------------
            // 4️⃣ Overweight penalty
            // ----------------------------

            // TODO:
            // sum penalties where status == Overweight


            // ----------------------------
            // 5️⃣ Height penalty
            // ----------------------------

            // TODO:
            // sum penalties where status == HeightFail


            // ----------------------------
            // 6️⃣ Final fitness calculation
            // ----------------------------

            // TODO:
            // fitness =
            // settings.CoverageWeight * coverage
            // - settings.RelocationWeight * relocationPenalty
            // - settings.RadiusPenaltyWeight * radiusPenalty
            // - settings.OverweightPenaltyWeight * overweightPenalty
            // - settings.HeightPenaltyWeight * heightPenalty

            return breakdown;
        }
    }
}