namespace CraneOptimizer.Domain.Diagnostics
{
    public sealed class ImportReadinessElementDiagnostic
    {
        public string ElementId { get; init; } = string.Empty;
        public bool IsReadyForGa { get; init; }
        public string ReadinessStatus { get; init; } = "Blocked";
        public IReadOnlyList<string> BlockingReasons { get; init; } = Array.Empty<string>();
        public IReadOnlyList<string> WarningMessages { get; init; } = Array.Empty<string>();
    }
}
