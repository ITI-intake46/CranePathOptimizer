namespace CraneOptimizer.Application.Optimization.GeneticAlgorithms.Models
{
    public record ReachabilityDiagnostics(
        int TotalCandidatePoints,
        int ZeroCoveragePoints,         // Points with 0 reachable elements
        int HotSpotCount,
        double MaxHotSpotCoverage,      // % of elements reachable from the best single point
        double AverageHotSpotCoverage
    );

    public record ScoredCoordinate(double X, double Y, int ReachableCount, Guid ZoneId = default);
}
