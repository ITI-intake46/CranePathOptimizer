namespace CraneOptimizer.Application.Reports.Models;

public sealed class ReportMetric
{
    public string Label { get; init; } = string.Empty;
    public string Value { get; init; } = string.Empty;
    public string? Unit { get; init; }
}
