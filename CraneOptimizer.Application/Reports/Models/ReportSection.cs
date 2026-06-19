namespace CraneOptimizer.Application.Reports.Models;

public sealed class ReportSection
{
    public string Title { get; init; } = string.Empty;
    public string? Description { get; init; }

    public List<ReportMetric> Metrics { get; init; } = new();
    public List<ReportTable> Tables { get; init; } = new();
    public List<string> Notes { get; init; } = new();
}
