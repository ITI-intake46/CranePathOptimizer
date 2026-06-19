namespace CraneOptimizer.Application.Reports.Models;

public sealed class ReportManifest
{
    public string ReportId { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public DateTime CreatedAtUtc { get; init; }
    public List<string> Files { get; init; } = new();
}
