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

        /// <summary>
        /// Reachability diagnostics computed before running GA.
        /// </summary>
        public ReachabilityDiagnostics? ReachabilityDiagnostics { get; set; }

        // ── Crane / run metadata (populated by CranePlacementService) ─────────

        /// <summary>Display name of the crane used in this optimization run.</summary>
        public string? CraneName { get; set; }

        public string? CraneManufacturer { get; set; }
        public string? CraneModelOrSpecification { get; set; }
        public string? CraneType { get; set; }
        public double MaxCapacity { get; set; }
        public double MinRadius { get; set; }
        public double MaxRadius { get; set; }
        public double MinBoomLength { get; set; }
        public double MaxBoomLength { get; set; }
        public double VehicleLength { get; set; }
        public double VehicleWidth { get; set; }
        public double VehicleHeight { get; set; }
        public double? OutriggerLength { get; set; }
        public double? OutriggerWidth { get; set; }
        public string? OutriggerConfiguration { get; set; }

        /// <summary>Wire length setting (m) used in this optimization run.</summary>
        public double WireLength { get; set; }

        /// <summary>Hook, rigging, and accessory weight setting (t) used in this optimization run.</summary>
        public double AdditionalWeight { get; set; }

        /// <summary>Derating ratio applied to all load chart capacity values.</summary>
        public double DeratingRatio { get; set; }

        /// <summary>Safety factor applied after derating.</summary>
        public double SafetyFactor { get; set; }

        /// <summary>
        /// Counterweight configured on the crane specification (tonnes).
        /// Zero if not set.
        /// </summary>
        public double CounterWeight { get; set; }
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
        public string ElementId { get; set; } = string.Empty;

        /// <summary>
        /// Element mark/name from the source model when available.
        /// </summary>
        public string? ElementMark { get; set; }

        // Phase 5: Canonical Display Identity (additive; ElementMark kept for backward compat)
        public string? CanonicalMark { get; set; }
        public string? MarkSource { get; set; }
        public string? MarkConfidence { get; set; }
        public bool IsExplicitMark { get; set; }

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
        /// This is the raw chart value multiplied by DeratingRatio × SafetyFactor.
        /// </summary>
        public double AvailableCapacity { get; set; }

        /// <summary>
        /// Raw manufacturer load chart capacity at the selected (radius, boom) cell,
        /// before derating ratio and safety factor are applied.
        /// Null when no chart cell was resolved (e.g. OutOfRadius, NotCharted).
        /// </summary>
        public double? RawChartCapacity { get; set; }

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

        public double CraneBaseX { get; set; }
        public double CraneBaseY { get; set; }
        public double CraneBaseZ { get; set; }

        public double SlewCenterX { get; set; }
        public double SlewCenterY { get; set; }
        public double SlewCenterZ { get; set; }

        public double BoomHingeX { get; set; }
        public double BoomHingeY { get; set; }
        public double BoomHingeZ { get; set; }

        public double LoadPointX { get; set; }
        public double LoadPointY { get; set; }
        public double LoadPointZ { get; set; }

        public string RadiusOriginType { get; set; } = "SlewCenter";
        public string BoomOriginType { get; set; } = "BoomHinge";
        public string CandidateBoomsChecked { get; set; } = string.Empty;
        public string RejectedCandidates { get; set; } = string.Empty;

        // GA Assignment and Uniqueness Fields
        public string CandidatePointId { get; set; } = string.Empty;
        public int CandidatePointIndex { get; set; } = -1;
        public bool IsFeasibleFromPoint { get; set; }
        public bool IsFinalAssignedToPoint { get; set; }
        public string AssignedPointId { get; set; } = string.Empty;
        public int AssignedPointIndex { get; set; } = -1;
        public string AssignmentReason { get; set; } = string.Empty;
        public double AssignmentScore { get; set; }
        public bool DuplicateAssignmentResolved { get; set; }
        public string FeasiblePointIds { get; set; } = string.Empty;
        public string FinalOwnerPointId { get; set; } = string.Empty;
        public bool IsFinalAssigned { get; set; }
        public string BestFailedPointId { get; set; } = string.Empty;
        public string BestFailureReason { get; set; } = string.Empty;

        public bool HasBestFailedCandidate { get; set; }
        public LiftStatus BestFailedLiftStatus { get; set; } = LiftStatus.OutOfRadius;
        public ReachStatus BestFailedReachStatus { get; set; } = ReachStatus.OutOfRadius;
        public ChartStatus BestFailedChartStatus { get; set; } = ChartStatus.NoListedCapacity;
        public double BestFailedSafetyMargin { get; set; }
        public double BestFailedRequiredRadius { get; set; }
        public double BestFailedRequiredBoomLength { get; set; }
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

        public int NotCharted { get; set; }
    }
}
