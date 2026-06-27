namespace CraneOptimizer.Application.Optimization.GeneticAlgorithms.Models
{
    public record ReachabilityDiagnostics(
        int TotalCandidatePoints,
        int ValidCandidatePoints,
        int RejectedCandidatePoints,
        int ZeroCoveragePoints,         // Points with 0 reachable elements
        int HotSpotCount,
        double MaxHotSpotCoverage,      // % of elements reachable from the best single point
        double AverageHotSpotCoverage,
        double ReachabilityMean,
        double ReachabilityStdDev
    );

    public record ScoredCoordinate(double X, double Y, int ReachableCount, Guid ZoneId = default);

    public sealed class ZoneReachabilitySummary
    {
        public Guid ZoneId { get; init; }
        public int CandidateGridPointCount { get; init; }
        public int ValidGridPointCount { get; init; }
        public int RejectedGridPointCount { get; init; }
        public int ZeroReachabilityPointCount { get; init; }
        public int ReachabilityMax { get; init; }
        public double ReachabilityMean { get; init; }
        public double ReachabilityStdDev { get; init; }
        public int SelectedSeedCount { get; set; }
    }
}
