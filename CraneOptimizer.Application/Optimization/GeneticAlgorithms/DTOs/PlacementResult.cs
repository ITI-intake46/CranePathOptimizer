using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Models;
using System.Collections.Generic;
using System.Linq;

namespace CraneOptimizer.Application.Optimization.GeneticAlgorithms.DTOs
{
    /// <summary>
    /// Application-level result returned from the Crane Placement optimization engine.
    /// Contains best positions, fitness, coverage, and statistics in normalized metrics.
    /// </summary>
    public class PlacementResult
    {
        public List<CranePositionResult> Positions { get; set; } = new();

        public double FitnessScore { get; set; }

        public double CoverageRatio { get; set; }

        public int LiftedElementCount { get; set; }

        public int TotalElementCount { get; set; }

        public LiftStatistics Statistics { get; set; } = new();
        public string? DiagnosticsReportPath { get; set; }
    }

    /// <summary>
    /// Represents a single crane position in the optimized solution.
    /// </summary>
    public class CranePositionResult
    {
        public double X { get; set; }

        public double Y { get; set; }

        public double Z { get; set; }

        public int NewLiftableCount { get; set; }

        public int LiftableCount =>
            ElementResults.Count(result => result.Status == LiftStatus.Liftable);

        public List<ElementLiftResult> ElementResults { get; set; } = new();
    }

    /// <summary>
    /// Represents lift evaluation result for a single element from a specific crane position.
    /// </summary>
    public class ElementLiftResult
    {
        public string ElementId { get; set; }

        public string? ElementMark { get; set; }

        public string? ElementType { get; set; }

        public double? Weight { get; set; }

        public double CogX { get; set; }

        public double CogY { get; set; }

        public double CogZ { get; set; }

        public LiftStatus Status { get; set; }

        public double Radius { get; set; }

        public double BoomLength { get; set; }

        public double AvailableCapacity { get; set; }

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
    /// Aggregated lift statistics across all elements in the application layer.
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
