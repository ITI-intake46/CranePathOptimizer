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
    }
}
