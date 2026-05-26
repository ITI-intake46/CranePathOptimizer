using System;

namespace CraneOptimizer.Application.Optimization.GeneticAlgorithms.DTOs
{
    public class GAElementDiagnosticDto
    {
        public string Timestamp { get; set; } = string.Empty;
        public string ProjectName { get; set; } = string.Empty;
        public string IfcFileName { get; set; } = string.Empty;
        public string CraneNameOrId { get; set; } = string.Empty;
        public string ElementId { get; set; } = string.Empty;
        public string ElementName { get; set; } = string.Empty;
        public string ElementType { get; set; } = string.Empty;
        public string StoreyOrElevation { get; set; } = string.Empty;
        public double Weight { get; set; }
        public double CogX { get; set; }
        public double CogY { get; set; }
        public double CogZ { get; set; }
        public int AssignedSetupIndex { get; set; }
        public string AssignedCraneX { get; set; } = string.Empty;
        public string AssignedCraneY { get; set; } = string.Empty;
        public string AssignedCraneZ { get; set; } = string.Empty;
        public double BestRadius { get; set; }
        public double RequiredBoomLength { get; set; }
        public double RequiredCapacity { get; set; }
        public double AvailableCapacity { get; set; }
        public string LiftStatus { get; set; } = string.Empty;
        public bool IsLifted { get; set; }
        public string FailureReason { get; set; } = string.Empty;
        public string FailureCategory { get; set; } = string.Empty;
        public string SuggestedAction { get; set; } = string.Empty;

        public string ReachStatus { get; set; } = string.Empty;
        public string ChartStatus { get; set; } = string.Empty;
        public bool IsExactMatch { get; set; }
        public bool IsSteppedRadius { get; set; }
        public bool IsBlankCell { get; set; }
        public bool IsZeroCell { get; set; }
        public double SafetyMargin { get; set; }
        public string LookupMode { get; set; } = string.Empty;
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
    }
}
