using System.Collections.Generic;

namespace WPF_IFC_test1.Analysis
{
    /// <summary>
    /// Aggregates a list of <see cref="ElementLiftResult"/> into a single scalar Lift Score.
    ///
    /// Formula:  Score = (nLiftable × 0) + (nNonLiftable × penaltyNL) + (nUnreachable × penaltyUR)
    ///
    /// Lower score = better crane position.
    /// Score = 0 means every element is Liftable.
    /// </summary>
    public static class LiftScoreCalculator
    {
        /// <summary>
        /// Calculates the Lift Score and returns the per-status counts.
        /// </summary>
        public static double Calculate(
            IEnumerable<ElementLiftResult> results,
            int penaltyNonLiftable,
            int penaltyUnreachable,
            out int countLiftable,
            out int countNonLiftable,
            out int countUnreachable)
        {
            countLiftable    = 0;
            countNonLiftable = 0;
            countUnreachable = 0;

            foreach (var r in results)
            {
                switch (r.Status)
                {
                    case LiftStatus.Liftable:    countLiftable++;    break;
                    case LiftStatus.NonLiftable: countNonLiftable++; break;
                    case LiftStatus.Unreachable: countUnreachable++; break;
                }
            }

            return (countNonLiftable * penaltyNonLiftable)
                 + (countUnreachable * penaltyUnreachable);
        }

        /// <summary>Convenience overload when counts are not needed.</summary>
        public static double Calculate(
            IEnumerable<ElementLiftResult> results,
            OptimizerSettings settings)
        {
            return Calculate(results,
                settings.PenaltyNonLiftable,
                settings.PenaltyUnreachable,
                out _, out _, out _);
        }
    }
}
