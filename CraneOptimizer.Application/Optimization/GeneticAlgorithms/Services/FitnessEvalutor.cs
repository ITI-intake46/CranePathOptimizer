using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Interfaces;
using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Models;
using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Settings;

namespace CraneOptimizer.Application.Optimization.GeneticAlgorithms.Services
{
    /// <summary>
    /// Computes the fitness value of a GA solution
    /// based on lifting feasibility and penalties.
    ///
    /// ── Design principles ────────────────────────────────────────────
    ///
    ///  1. Coverage is the dominant signal.
    ///     A solution that lifts 90% of elements is always better than
    ///     one that lifts 50%, regardless of other penalties.
    ///     CoverageWeight must be the largest weight in GASettings.
    ///
    ///  2. Hard constraint — zero coverage → zero fitness.
    ///     A solution that cannot lift a single element has no value.
    ///     We return 0 immediately to avoid wasting GA selection pressure.
    ///
    ///  3. Penalties are normalised before weighting.
    ///     Raw counts (e.g. 12 out-of-radius elements) scale with
    ///     project size and create inconsistent pressure across projects.
    ///     We normalise all penalties to [0, 1] by dividing by total elements.
    ///     This keeps the fitness surface smooth and project-independent.
    ///
    ///  4. Relocation penalty is normalised by MaxCranePositions.
    ///     One relocation in a 3-position chromosome is not the same as
    ///     one relocation in a 10-position chromosome. Normalising removes
    ///     the bias introduced by the chromosome length range.
    ///
    ///  5. SpecialCase (no chart data) is penalised lightly.
    ///     These elements are unknowns — not confirmed failures.
    ///     A heavy penalty would discourage positions that might actually
    ///     be valid once chart data is filled in.
    ///
    ///  6. Fitness is always >= 0.
    ///     Negative fitness scores are valid mathematically but confuse
    ///     tournament selection and elitism implementations.
    ///     We clamp the final value at 0.
    ///
    ///  7. NEW — Duplicate coverage is penalised.
    ///     If multiple crane positions lift the SAME element,
    ///     only one counts toward coverage; the rest are penalised.
    ///     This forces GA to distribute lifting across positions.
    ///
    /// ── Fitness formula ──────────────────────────────────────────────
    ///
    ///   fitness =
    ///       CoverageWeight        * coverageRatio
    ///     - RelocationWeight      * relocationPenalty
    ///     - RadiusPenaltyWeight   * outOfRadius
    ///     - OverweightWeight      * overweight
    ///     - HeightPenaltyWeight   * heightFail
    ///     - SpecialCaseWeight     * specialCase
    ///     - DuplicationWeight     * duplicationPenalty   ← NEW
    ///
    /// </summary>
    public class FitnessEvaluator : IFitnessEvaluator
    {
        private const double SpecialCasePenaltyWeight = 1.0;

        public FitnessBreakdown EvaluateFitness(
            CraneSolution solution,
            AssignmentResult assignment,
            GASettings settings)
        {
            var breakdown = new FitnessBreakdown();
            var results = assignment.EvaluationResults;

            // ── Guard: empty evaluation ────────────────────────────
            if (results == null || results.Count == 0)
            {
                breakdown.FitnessScore = 0;
                return breakdown;
            }

            int total = results.Count;

            // ══════════════════════════════════════════════════════
            // NEW: TRACK UNIQUE COVERAGE + DUPLICATION
            // ══════════════════════════════════════════════════════

            // Unique elements lifted (true coverage)
            int uniqueLiftableCount = results
                .Where(r => r.Status == LiftStatus.Liftable)
                .Select(r => r.ElementId)
                .Distinct()
                .Count();

            double coverageRatio = (double)uniqueLiftableCount / total;

            // ── Hard constraint: zero coverage → zero fitness ──────
            if (uniqueLiftableCount == 0)
            {
                breakdown.FitnessScore = 0;
                breakdown.CoverageRatio = 0;
                breakdown.LiftableElements = 0;
                breakdown.TotalElements = total;
                return breakdown;
            }

            // ══════════════════════════════════════════════════════
            // 2. RELOCATION PENALTY
            // ══════════════════════════════════════════════════════

            int craneCount = solution.Chromosome.CranePositions.Count;

            double normalisedRelocation = settings.MaxCranePositions > 0
                ? (double)craneCount / settings.MaxCranePositions
                : 0;

            // ══════════════════════════════════════════════════════
            // 3. FAILURE PENALTIES
            // ══════════════════════════════════════════════════════

            int outOfRadiusCount = results.Count(r => r.Status == LiftStatus.OutOfRadius);
            int overweightCount = results.Count(r => r.Status == LiftStatus.Overweight);
            int heightFailCount = results.Count(r => r.Status == LiftStatus.HeightFail);
            int specialCaseCount = results.Count(r => r.Status == LiftStatus.SpecialCase);

            double normOutOfRadius = (double)outOfRadiusCount / total;
            double normOverweight = (double)overweightCount / total;
            double normHeightFail = (double)heightFailCount / total;
            double normSpecialCase = (double)specialCaseCount / total;

            // ══════════════════════════════════════════════════════
            // 4. RADIUS QUALITY BONUS
            // ══════════════════════════════════════════════════════

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

            // ══════════════════════════════════════════════════════
            // 5. BALANCE & RADIUS STD-DEV PENALTIES
            // ══════════════════════════════════════════════════════

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

            // Similar setup points are not real engineering relocations.
            // Penalize near-duplicate crane positions so the GA cannot treat
            // 1-2 meter adjustments as independent setup locations.
            double similarPositionPenalty =
                CranePlacementConstraintService.CalculateSimilarityPenalty(
                    solution.Chromosome.CranePositions,
                    settings);

            // Movement distance is a proxy for mobilization/travel effort.
            // This complements the existing relocation-count penalty.
            double relocationDistancePenalty =
                CranePlacementConstraintService.CalculateNormalizedRelocationDistance(
                    solution.Chromosome.CranePositions,
                    settings);

            // ══════════════════════════════════════════════════════
            // 6. FINAL FITNESS
            // ══════════════════════════════════════════════════════

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
                - settings.SimilarPositionPenaltyWeight * similarPositionPenalty
                - settings.RelocationDistancePenaltyWeight * relocationDistancePenalty;

            fitness = Math.Max(0, fitness);

            // ══════════════════════════════════════════════════════
            // 7. POPULATE BREAKDOWN
            // ══════════════════════════════════════════════════════

            breakdown.FitnessScore = fitness;
            breakdown.CoverageRatio = coverageRatio;
            breakdown.LiftableElements = uniqueLiftableCount;
            breakdown.TotalElements = total;
            breakdown.RelocationPenalty = normalisedRelocation;
            breakdown.RadiusPenalty = normOutOfRadius;
            breakdown.OverweightPenalty = normOverweight;
            breakdown.HeightPenalty = normHeightFail;
            breakdown.SimilarPositionPenalty = similarPositionPenalty;
            breakdown.RelocationDistancePenalty = relocationDistancePenalty;

            return breakdown;
        }
    }
}
