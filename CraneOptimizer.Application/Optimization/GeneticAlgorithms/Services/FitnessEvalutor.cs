using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Interfaces;
using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Models;
using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Settings;
using CraneOptimizer.Domain.Assignment;
using CraneOptimizer.Domain.Schemas.Enums;
using System;
using System.Linq;

namespace CraneOptimizer.Application.Optimization.GeneticAlgorithms.Services
{
    public class FitnessEvaluator : IFitnessEvaluator
    {
        private const double SpecialCasePenaltyWeight = 1.0;

        public FitnessBreakdown EvaluateFitness(
            CraneSolution solution,
            CraneAssignment assignment,
            GASettings settings)
        {
            var breakdown = new FitnessBreakdown();
            var results = assignment.EvaluationResults;

            if (results == null || results.Count == 0)
            {
                breakdown.FitnessScore = 0;
                return breakdown;
            }

            int total = results.Count;

            // Unique elements lifted (true coverage)
            int uniqueLiftableCount = results
                .Where(r => r.Status == LiftStatus.Liftable)
                .Select(r => r.ElementId)
                .Distinct()
                .Count();

            double coverageRatio = (double)uniqueLiftableCount / total;

            if (uniqueLiftableCount == 0)
            {
                breakdown.FitnessScore = 0;
                breakdown.CoverageRatio = 0;
                breakdown.LiftableElements = 0;
                breakdown.TotalElements = total;
                return breakdown;
            }

            int craneCount = solution.Chromosome.CranePositions.Count;
            double normalisedRelocation = settings.MaxCranePositions > 0
                ? (double)craneCount / settings.MaxCranePositions
                : 0;

            int outOfRadiusCount = results.Count(r => r.Status == LiftStatus.OutOfRadius);
            int overweightCount = results.Count(r => r.Status == LiftStatus.Overweight);
            int heightFailCount = results.Count(r => r.Status == LiftStatus.HeightFail);
            int specialCaseCount = results.Count(r => r.Status == LiftStatus.SpecialCase);

            double normOutOfRadius = (double)outOfRadiusCount / total;
            double normOverweight = (double)overweightCount / total;
            double normHeightFail = (double)heightFailCount / total;
            double normSpecialCase = (double)specialCaseCount / total;

            double radiusQualityPenalty = 0;
            if (settings.MaxRadiusForNormalisation > 0)
            {
                var liftableResults = results.Where(r => r.Status == LiftStatus.Liftable).ToList();
                if (liftableResults.Count > 0)
                {
                    double avgRadius = liftableResults.Average(r => r.Radius);
                    radiusQualityPenalty = avgRadius / settings.MaxRadiusForNormalisation;
                    radiusQualityPenalty = Math.Min(radiusQualityPenalty, 1.0);
                }
            }

            var countsPerCrane = assignment.AssignedElements.Select(kv => kv.Value.Count).ToList();
            double variance = countsPerCrane.Count > 1 ? 
                countsPerCrane.Average(c => Math.Pow(c - countsPerCrane.Average(), 2)) : 0;
            double normalizedVariance = total > 0 ? Math.Min(variance / total, 1.0) : 0;
            double balancePenalty = settings.BalancePenaltyWeight * normalizedVariance;

            var liftableRadii = results.Where(r => r.Status == LiftStatus.Liftable).Select(r => r.Radius).ToList();
            double radiusStdDev = liftableRadii.Count > 1 ? 
                Math.Sqrt(liftableRadii.Average(r => Math.Pow(r - liftableRadii.Average(), 2))) : 0;
            double normalizedStdDev = settings.MaxRadiusForNormalisation > 0 ? 
                Math.Min(radiusStdDev / settings.MaxRadiusForNormalisation, 1.0) : 0;
            double radiusStdDevPenalty = settings.RadiusStdDevPenaltyWeight * normalizedStdDev;

            double fitness =
                  settings.CoverageWeight * coverageRatio
                - settings.RelocationWeight * normalisedRelocation
                - settings.RadiusPenaltyWeight * normOutOfRadius
                - settings.OverweightPenaltyWeight * normOverweight
                - settings.HeightPenaltyWeight * normHeightFail
                - SpecialCasePenaltyWeight * normSpecialCase
                - settings.RadiusPenaltyWeight * radiusQualityPenalty
                - balancePenalty
                - radiusStdDevPenalty
                - assignment.TotalConstraintPenalty;

            fitness = Math.Max(0, fitness);

            breakdown.FitnessScore = fitness;
            breakdown.CoverageRatio = coverageRatio;
            breakdown.LiftableElements = uniqueLiftableCount;
            breakdown.TotalElements = total;
            breakdown.RelocationPenalty = normalisedRelocation;
            breakdown.RadiusPenalty = normOutOfRadius;
            breakdown.OverweightPenalty = normOverweight;
            breakdown.HeightPenalty = normHeightFail;

            return breakdown;
        }
    }
}