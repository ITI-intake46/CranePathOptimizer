using System.Collections.Generic;

namespace WPF_IFC_test1.Analysis
{
    /// <summary>Progress report emitted once per GA generation (for the UI progress bar).</summary>
    public sealed class OptimizerProgress
    {
        public int    Generation       { get; init; }
        public int    MaxGenerations   { get; init; }
        public double BestScore        { get; init; }
        public int    CountLiftable    { get; init; }
        public int    CountNonLiftable { get; init; }
        public int    CountUnreachable { get; init; }

        /// <summary>0–100 percent complete.</summary>
        public double GenerationPercent =>
            MaxGenerations > 0 ? 100.0 * Generation / MaxGenerations : 0;
    }

    /// <summary>
    /// Final result produced by <see cref="CranePositionGA"/> after the run completes.
    /// </summary>
    public sealed class OptimizerResult
    {
        /// <summary>Best found crane X position in world coordinates.</summary>
        public double BestX { get; init; }

        /// <summary>Best found crane Y position in world coordinates.</summary>
        public double BestY { get; init; }

        /// <summary>Lift Score at the best position (lower = better; 0 = all liftable).</summary>
        public double BestScore { get; init; }

        /// <summary>Number of Liftable elements at the best position.</summary>
        public int CountLiftable { get; init; }

        /// <summary>Number of Non-Liftable elements at the best position.</summary>
        public int CountNonLiftable { get; init; }

        /// <summary>Number of Unreachable elements at the best position.</summary>
        public int CountUnreachable { get; init; }

        /// <summary>Number of generations the GA actually ran.</summary>
        public int GenerationsRun { get; init; }

        /// <summary>Human-readable reason the GA stopped.</summary>
        public string StopReason { get; init; }

        /// <summary>Full per-element results at the best position (for recolouring the viewer).</summary>
        public IReadOnlyList<ElementLiftResult> ElementResults { get; init; }

        /// <summary>Best score recorded at each generation (for a convergence chart).</summary>
        public IReadOnlyList<double> GenerationBestScores { get; init; }

        /// <summary>Coverage percentage: Liftable / total analysed × 100.</summary>
        public double CoveragePct
        {
            get
            {
                int total = CountLiftable + CountNonLiftable + CountUnreachable;
                return total > 0 ? 100.0 * CountLiftable / total : 0;
            }
        }
    }
}
