namespace CraneOptimizer.Application.Reports.DTOs;

public sealed record GaReportRunSelection
{
    public string ProjectGuid { get; init; } = string.Empty;
    public string OptimizationRunId { get; init; } = string.Empty;
    public string SourceKind { get; init; } = string.Empty;
    public string Classification { get; init; } = string.Empty;
    public string SnapshotPath { get; init; } = string.Empty;
}
