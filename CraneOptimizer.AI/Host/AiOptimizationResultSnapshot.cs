namespace CraneOptimizer.AI.Host
{
    public sealed class AiOptimizationResultSnapshot
    {
        public bool HasResult { get; init; }
        public double CoverageRatio { get; init; }
        public int LiftedElementCount { get; init; }
        public int TotalElementCount { get; init; }
        public int PositionCount { get; init; }
        public string? DiagnosticsPath { get; init; }
        public double MaxCapacity { get; init; }
        public double MaxRadius { get; init; }
    }
}
