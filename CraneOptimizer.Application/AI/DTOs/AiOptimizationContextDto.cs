namespace CraneOptimizer.Application.AI.DTOs
{
    public sealed class AiOptimizationContextDto
    {
        public bool HasOptimizationResult { get; init; }
        public string ProjectName { get; init; } = string.Empty;
        public string CraneName { get; init; } = string.Empty;
        public string CraneModelOrSpecification { get; init; } = string.Empty;
        public int TotalElementCount { get; init; }
        public int LiftedElementCount { get; init; }
        public int PositionCount { get; init; }
        public double CoverageRatio { get; init; }
        public double FitnessScore { get; init; }
        public double MaxCraneRadius { get; init; }
        public double MaxCraneCapacity { get; init; }
        public double SafetyFactor { get; init; }
        public double DeratingRatio { get; init; }
        public double AdditionalWeight { get; init; }
        public double WireLength { get; init; }
        public string? DiagnosticsPath { get; init; }
        public FailureCountsDto FailureCounts { get; init; } = new();
        public ReachabilitySnapshotDto? Reachability { get; init; }
    }

    public sealed class FailureCountsDto
    {
        public int Liftable { get; init; }
        public int OutOfRadius { get; init; }
        public int Overweight { get; init; }
        public int HeightFail { get; init; }
        public int SpecialCase { get; init; }
        public int NotCharted { get; init; }

        public int TotalFailed => OutOfRadius + Overweight + HeightFail + SpecialCase + NotCharted;
    }

    public sealed class ReachabilitySnapshotDto
    {
        public int TotalCandidatePoints { get; init; }
        public int ValidCandidatePoints { get; init; }
        public int RejectedCandidatePoints { get; init; }
        public int ZeroCoveragePoints { get; init; }
        public int HotSpotCount { get; init; }
        public double MaxHotSpotCoverage { get; init; }
        public double AverageHotSpotCoverage { get; init; }
        public double ReachabilityMean { get; init; }
        public double ReachabilityStdDev { get; init; }
    }
}
