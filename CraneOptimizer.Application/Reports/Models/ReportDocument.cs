namespace CraneOptimizer.Application.Reports.Models;

public sealed class ReportDocument
{
    public string ReportId { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public DateTime CreatedAtUtc { get; init; } = DateTime.UtcNow;

    public ReportMetadata Metadata { get; init; } = new();
    public List<ReportSection> Sections { get; init; } = new();
}
