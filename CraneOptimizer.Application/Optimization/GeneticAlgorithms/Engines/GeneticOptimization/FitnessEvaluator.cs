using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Interfaces;
using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Models;
using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Settings;
using System;
using System.Linq;
using CraneOptimizer.Application.Optimization.Utilities;

namespace CraneOptimizer.Application.Optimization.GeneticAlgorithms.Engines.GeneticOptimization
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
    ///  7. Duplicate coverage is penalised.
    ///     If multiple crane positions lift the SAME element,
    ///     only one counts toward coverage; the rest are penalised.
    ///     This forces GA to distribute lifting across positions.
    ///
    ///  8. RadiusPenaltyWeight and RadiusQualityPenaltyWeight are kept
    ///     separate (Issue 8 fix). RadiusPenaltyWeight penalises the count
    ///     of out-of-radius elements; RadiusQualityPenaltyWeight penalises
    ///     the average working-radius magnitude independently.
    ///
    ///  9. Balance penalty uses liftable-only counts per crane (Issue 10 fix).
    ///     Cranes that attract many elements they cannot lift are not
    ///     counted as "busy" — only confirmed Liftable assignments matter.
    ///
    /// ── Fitness formula ──────────────────────────────────────────────
    ///
    ///   fitness =
    ///       CoverageWeight             * coverageRatio
    ///     - RelocationWeight           * relocationPenalty
    ///     - RadiusPenaltyWeight        * outOfRadiusFraction
    ///     - OverweightWeight           * overweightFraction
    ///     - HeightPenaltyWeight        * heightFailFraction
    ///     - SpecialCaseWeight          * specialCaseFraction
    ///     - RadiusQualityPenaltyWeight * avgRadiusQuality  ← uses own weight
    ///     - BalancePenaltyWeight       * liftableVariance  ← liftable only
    ///
    /// </summary>
    public class FitnessEvaluator : IFitnessEvaluator
    {
        private readonly IConstraintEngine _constraintEngine;
        private const double SpecialCasePenaltyWeight = 1.0;

        public FitnessEvaluator(IConstraintEngine constraintEngine)
        {
            _constraintEngine = constraintEngine;
        }

        public FitnessBreakdown EvaluateFitness(
            CraneSolution solution,
            AssignmentResult assignment,
            GASettings settings)
        {
            using (new GAProfiler.ProfileScope("FitnessEvaluator.EvaluateFitness"))
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
                // 1. SINGLE-PASS METRIC EXTRACTION
                // ══════════════════════════════════════════════════════
                int outOfRadiusCount = 0;
                int overweightCount = 0;
                int heightFailCount = 0;
                int specialCaseCount = 0;
                double sumRadius = 0;
                int liftableCount = 0;

                // Use localized HashSet for unique liftable element count
            var uniqueLiftableIds = new HashSet<string>(StringComparer.Ordinal);

                foreach (var r in results)
                {
                    if (r.Status == LiftStatus.Liftable)
                    {
                        uniqueLiftableIds.Add(r.ElementId);
                        sumRadius += r.Radius;
                        liftableCount++;
                    }
                    else if (r.Status == LiftStatus.OutOfRadius)
                    {
                        outOfRadiusCount++;
                    }
                    else if (r.Status == LiftStatus.Overweight)
                    {
                        overweightCount++;
                    }
                    else if (r.Status == LiftStatus.HeightFail)
                    {
                        heightFailCount++;
                    }
                    else if (r.Status == LiftStatus.SpecialCase || r.Status == LiftStatus.NotCharted)
                    {
                        specialCaseCount++;
                    }
                }

                int uniqueLiftableCount = uniqueLiftableIds.Count;
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

                // Step-wise relocation penalty (1 setup = 0 penalty, 2 setups = RelocationWeight, etc.)
                double relocationPenaltyVal = craneCount > 1
                    ? settings.RelocationWeight * (craneCount - 1)
                    : 0;

                double normalisedRelocation = settings.MaxCranePositions > 0
                    ? (double)craneCount / settings.MaxCranePositions
                    : 0;

                // ══════════════════════════════════════════════════════
                // 3. FAILURE PENALTIES
                // ══════════════════════════════════════════════════════
                double normOutOfRadius = (double)outOfRadiusCount / total;
                double normOverweight = (double)overweightCount / total;
                double normHeightFail = (double)heightFailCount / total;
                double normSpecialCase = (double)specialCaseCount / total;

                // ══════════════════════════════════════════════════════
                // 4. RADIUS QUALITY BONUS
                // ══════════════════════════════════════════════════════
                double radiusQualityPenalty = 0;

                if (settings.MaxRadiusForNormalisation > 0 && liftableCount > 0)
                {
                    double avgRadius = sumRadius / liftableCount;
                    radiusQualityPenalty = avgRadius / settings.MaxRadiusForNormalisation;
                    radiusQualityPenalty = Math.Min(radiusQualityPenalty, 1.0);
                }

                // ══════════════════════════════════════════════════════
                // 5. BALANCE & RADIUS STD-DEV PENALTIES
                // ══════════════════════════════════════════════════════

                // Issue 10 fix: use AssignedLiftableElements (only confirmed Liftable results)
                // Allocation-free variance loop:
                double balancePenalty = 0;
                if (assignment.AssignedLiftableElements.Count > 1)
                {
                    double sumLiftable = 0;
                    int activeCranes = 0;
                    foreach (var kv in assignment.AssignedLiftableElements)
                    {
                        sumLiftable += kv.Value.Count;
                        activeCranes++;
                    }

                    if (activeCranes > 1)
                    {
                        double avgLiftable = sumLiftable / activeCranes;
                        double sumSqDiff = 0;
                        foreach (var kv in assignment.AssignedLiftableElements)
                        {
                            sumSqDiff += Math.Pow(kv.Value.Count - avgLiftable, 2);
                        }
                        double variance = sumSqDiff / activeCranes;
                        double normalizedVariance = total > 0 ? Math.Min(variance / total, 1.0) : 0;
                        balancePenalty = settings.BalancePenaltyWeight * normalizedVariance;
                    }
                }

                // Standard deviation of radii: allocation-free second pass
                double radiusStdDev = 0;
                if (liftableCount > 1)
                {
                    double avgRadius = sumRadius / liftableCount;
                    double sumSqDiffRadius = 0;
                    foreach (var r in results)
                    {
                        if (r.Status == LiftStatus.Liftable)
                        {
                            sumSqDiffRadius += Math.Pow(r.Radius - avgRadius, 2);
                        }
                    }
                    radiusStdDev = Math.Sqrt(sumSqDiffRadius / liftableCount);
                }

                double normalizedStdDev = settings.MaxRadiusForNormalisation > 0 ? 
                    Math.Min(radiusStdDev / settings.MaxRadiusForNormalisation, 1.0) : 0;
                double radiusStdDevPenalty = settings.RadiusStdDevPenaltyWeight * normalizedStdDev;

                // Similar setup points are not real engineering relocations.
                double similarPositionPenalty =
                    _constraintEngine.CalculateSimilarityPenalty(
                        solution.Chromosome.CranePositions,
                        settings);

                // Movement distance is a proxy for mobilization/travel effort.
                double relocationDistancePenalty =
                    _constraintEngine.CalculateNormalizedRelocationDistance(
                        solution.Chromosome.CranePositions,
                        settings);

                // ══════════════════════════════════════════════════════
                // 6. FINAL FITNESS
                // ══════════════════════════════════════════════════════
                double fitness =
                      settings.CoverageWeight * settings.CoverageDominanceMultiplier * coverageRatio
                    - relocationPenaltyVal
                    - settings.RadiusPenaltyWeight * normOutOfRadius
                    - settings.OverweightPenaltyWeight * normOverweight
                    - settings.HeightPenaltyWeight * normHeightFail
                    - SpecialCasePenaltyWeight * normSpecialCase
                    - settings.RadiusQualityPenaltyWeight * radiusQualityPenalty
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
}
