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
    /// ── Fitness formula ──────────────────────────────────────────────
    ///
    ///   fitness =
    ///       CoverageWeight      * coverageRatio                (0–1)
    ///     - RelocationWeight    * normalisedRelocationPenalty  (0–1)
    ///     - RadiusPenaltyWeight * normalisedOutOfRadius        (0–1)
    ///     - OverweightWeight    * normalisedOverweight         (0–1)
    ///     - HeightPenaltyWeight * normalisedHeightFail         (0–1)
    ///     - SpecialCaseWeight   * normalisedSpecialCase        (0–1)
    ///
    ///   All terms are in the same unit → smooth, comparable surface.
    ///
    /// </summary>
    public class FitnessEvaluator : IFitnessEvaluator
    {
        // SpecialCase penalty weight is intentionally small.
        // These are data gaps, not confirmed lift failures.
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
            // 1. COVERAGE
            // ══════════════════════════════════════════════════════

            int liftableCount = results.Count(r => r.Status == LiftStatus.Liftable);

            double coverageRatio = (double)liftableCount / total;

            // ── Hard constraint: zero coverage → zero fitness ──────
            if (liftableCount == 0)
            {
                breakdown.FitnessScore = 0;
                breakdown.CoverageRatio = 0;
                breakdown.LiftableElements = 0;
                breakdown.TotalElements = total;
                return breakdown;
            }

            // ══════════════════════════════════════════════════════
            // 2. RELOCATION PENALTY  (normalised by MaxCranePositions)
            // ══════════════════════════════════════════════════════
            // More crane setups = more cost, time, and risk.
            // Normalised so 1 position → 1/Max, Max positions → 1.
            // This removes bias from chromosome length variation.

            int craneCount = solution.Chromosome.CranePositions.Count;
            double normalisedRelocation = settings.MaxCranePositions > 0
                ? (double)craneCount / settings.MaxCranePositions
                : 0;

            // ══════════════════════════════════════════════════════
            // 3. FAILURE PENALTIES  (each normalised by total elements)
            // ══════════════════════════════════════════════════════
            // Normalising by total removes project-size bias.
            // A project with 200 elements and 20 failures has the same
            // normalised penalty as one with 20 elements and 2 failures.

            int outOfRadiusCount = results.Count(r => r.Status == LiftStatus.OutOfRadius);
            int overweightCount = results.Count(r => r.Status == LiftStatus.Overweight);
            int heightFailCount = results.Count(r => r.Status == LiftStatus.HeightFail);
            int specialCaseCount = results.Count(r => r.Status == LiftStatus.SpecialCase);

            double normOutOfRadius = (double)outOfRadiusCount / total;
            double normOverweight = (double)overweightCount / total;
            double normHeightFail = (double)heightFailCount / total;
            double normSpecialCase = (double)specialCaseCount / total;

            // ══════════════════════════════════════════════════════
            // 4. RADIUS QUALITY BONUS  (smooth signal for the GA)
            // ══════════════════════════════════════════════════════
            // Among liftable elements, reward smaller working radius.
            // Smaller radius = safer lift, less crane stress.
            // Normalised average radius gives a [0,1] penalty signal.
            // Uses MaxRadius as the normalisation ceiling.

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
            // 5. FINAL FITNESS
            // ══════════════════════════════════════════════════════

            double fitness =
                  settings.CoverageWeight * coverageRatio
                - settings.RelocationWeight * normalisedRelocation
                - settings.RadiusPenaltyWeight * normOutOfRadius
                - settings.OverweightPenaltyWeight * normOverweight
                - settings.HeightPenaltyWeight * normHeightFail
                - SpecialCasePenaltyWeight * normSpecialCase
                - settings.RadiusPenaltyWeight * radiusQualityPenalty * 0.5;
            // Note: radiusQualityPenalty uses half the RadiusPenaltyWeight
            // so it nudges the GA toward smaller radii without overriding
            // the hard OutOfRadius penalty signal.

            // ── Clamp: fitness is never negative ──────────────────
            fitness = Math.Max(0, fitness);

            // ══════════════════════════════════════════════════════
            // 6. POPULATE BREAKDOWN
            // ══════════════════════════════════════════════════════

            breakdown.FitnessScore = fitness;
            breakdown.CoverageRatio = coverageRatio;
            breakdown.LiftableElements = liftableCount;
            breakdown.TotalElements = total;
            breakdown.RelocationPenalty = normalisedRelocation;
            breakdown.RadiusPenalty = normOutOfRadius;
            breakdown.OverweightPenalty = normOverweight;
            breakdown.HeightPenalty = normHeightFail;

            return breakdown;
        }
    }
}