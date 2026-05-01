using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Models;

namespace CraneOptimizer.UI.Features.F8_GAOptimizer.DTOs
{
    /// <summary>
    /// Final result returned from the Crane Placement optimization.
    /// Contains best positions, fitness, coverage, and statistics.
    /// </summary>
    public class CranePlacementResult
    {
        /// <summary>
        /// Best crane positions found by the optimization.
        /// </summary>
        public List<CranePositionResult> Positions { get; set; } = new();

        /// <summary>
        /// Final fitness score of the solution.
        /// </summary>
        public double FitnessScore { get; set; }

        /// <summary>
        /// Percentage of elements that can be successfully lifted.
        /// </summary>
        public double CoverageRatio { get; set; }

        /// <summary>
        /// Aggregated statistics of lift results.
        /// </summary>
        public LiftStatistics Statistics { get; set; } = new();
    }

    /// <summary>
    /// Represents a single crane position in the solution.
    /// </summary>
    public class CranePositionResult
    {
        /// <summary>
        /// X coordinate of crane position.
        /// </summary>
        public double X { get; set; }

        /// <summary>
        /// Y coordinate of crane position.
        /// </summary>
        public double Y { get; set; }

        /// <summary>
        /// Evaluation results of all elements for this position.
        /// </summary>
        public List<ElementLiftResult> ElementResults { get; set; } = new();
    }

    /// <summary>
    /// Represents lift evaluation result for a single element.
    /// </summary>
    public class ElementLiftResult
    {
        /// <summary>
        /// Element unique identifier.
        /// </summary>
        public string ElementId { get; set; }

        /// <summary>
        /// Lift feasibility status.
        /// </summary>
        public LiftStatus Status { get; set; }

        /// <summary>
        /// Working radius between crane and element.
        /// </summary>
        public double Radius { get; set; }

        /// <summary>
        /// Required lifting capacity for the element.
        /// </summary>
        public double RequiredCapacity { get; set; }
    }

    /// <summary>
    /// Aggregated lift statistics across all elements.
    /// </summary>
    public class LiftStatistics
    {
        public int Liftable { get; set; }

        public int OutOfRadius { get; set; }

        public int Overweight { get; set; }

        public int HeightFail { get; set; }

        public int SpecialCase { get; set; }
    }
}