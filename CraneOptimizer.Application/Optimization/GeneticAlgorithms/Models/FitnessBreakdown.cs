namespace CraneOptimizer.Application.Optimization.GeneticAlgorithms.Models
{
    /// <summary>
    /// Represents a detailed breakdown of the fitness calculation
    /// for a crane placement solution.
    /// </summary>
    public class FitnessBreakdown
    {
        /// <summary>
        /// Final fitness score used by the genetic algorithm.
        /// Higher values represent better solutions.
        /// </summary>
        public double FitnessScore { get; set; }

        /// <summary>
        /// Ratio of successfully liftable elements.
        /// Coverage = LiftableElements / TotalElements
        /// </summary>
        public double CoverageRatio { get; set; }

        /// <summary>
        /// Penalty applied due to crane relocations.
        /// More crane setups increase the penalty.
        /// </summary>
        public double RelocationPenalty { get; set; }

        /// <summary>
        /// Penalty related to large working radius.
        /// </summary>
        public double RadiusPenalty { get; set; }

        /// <summary>
        /// Penalty applied when elements exceed crane lifting capacity.
        /// </summary>
        public double OverweightPenalty { get; set; }

        /// <summary>
        /// Penalty applied when the required boom height is invalid.
        /// </summary>
        public double HeightPenalty { get; set; }

        /// <summary>
        /// Number of elements successfully assigned to crane positions.
        /// </summary>
        public int LiftableElements { get; set; }

        /// <summary>
        /// Total number of evaluated elements.
        /// </summary>
        public int TotalElements { get; set; }
    }
}