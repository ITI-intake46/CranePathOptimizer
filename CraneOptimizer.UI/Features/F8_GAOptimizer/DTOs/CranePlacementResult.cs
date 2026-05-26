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
        /// Number of unique target elements lifted by the final displayed positions.
        /// </summary>
        public int LiftedElementCount { get; set; }

        /// <summary>
        /// Number of target elements included in this GA run after element-type filtering.
        /// </summary>
        public int TotalElementCount { get; set; }

        /// <summary>
        /// Aggregated statistics of lift results.
        /// </summary>
        public LiftStatistics Statistics { get; set; } = new();

        /// <summary>
        /// CSV diagnostics generated for the latest GA run.
        /// </summary>
        public string? DebugReportPath { get; set; }

        /// <summary>
        /// Non-fatal error captured if writing the diagnostic report fails.
        /// </summary>
        public string? DebugReportError { get; set; }
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
        /// Ground elevation of the crane setup point in scene/display coordinates.
        /// </summary>
        public double Z { get; set; }

        /// <summary>
        /// Number of elements newly lifted by this crane that were not
        /// covered by any earlier crane position. Used for display ordering.
        /// </summary>
        public int NewLiftableCount { get; set; }

        /// <summary>
        /// Number of elements this point can lift by itself.
        /// </summary>
        public int LiftableCount =>
            ElementResults.Count(result => result.Status == LiftStatus.Liftable);

        /// <summary>
        /// Full evaluation results for ALL elements (all statuses) when this
        /// crane position is selected. Used for viewport color-coding.
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
        /// Element mark/name from the source model when available.
        /// </summary>
        public string? ElementMark { get; set; }

        /// <summary>
        /// Element type from the source model.
        /// </summary>
        public string? ElementType { get; set; }

        /// <summary>
        /// Element weight as imported from the model.
        /// </summary>
        public double? Weight { get; set; }

        /// <summary>
        /// Element center of gravity in scene/display coordinates.
        /// </summary>
        public double CogX { get; set; }

        public double CogY { get; set; }

        public double CogZ { get; set; }

        /// <summary>
        /// Lift feasibility status.
        /// </summary>
        public LiftStatus Status { get; set; }

        /// <summary>
        /// Working radius between crane and element.
        /// </summary>
        public double Radius { get; set; }

        /// <summary>
        /// Required boom length for this evaluated lift.
        /// </summary>
        public double BoomLength { get; set; }

        /// <summary>
        /// Available capacity found in the selected crane load chart.
        /// </summary>
        public double AvailableCapacity { get; set; }

        /// <summary>
        /// Required lifting capacity for the element.
        /// </summary>
        public double RequiredCapacity { get; set; }

        public ReachStatus ReachStatus { get; set; }
        public ChartStatus ChartStatus { get; set; }
        public bool IsExactMatch { get; set; }
        public bool IsSteppedRadius { get; set; }
        public bool IsBlankCell { get; set; }
        public bool IsZeroCell { get; set; }
        public double SafetyMargin { get; set; }
        public string LookupMode { get; set; } = "ConservativeStep";
        public double RequiredRadius { get; set; }
        public double SelectedChartRadius { get; set; }
        public double SelectedChartBoomLength { get; set; }
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
