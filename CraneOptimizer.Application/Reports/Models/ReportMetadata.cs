namespace CraneOptimizer.Application.Reports.Models;

public sealed class ReportMetadata
{
    public string ProjectName { get; init; } = string.Empty;
    public string ProjectId { get; init; } = string.Empty;
    public string IfcFileName { get; init; } = string.Empty;
    public string CraneName { get; init; } = string.Empty;
    public string PreparedBy { get; init; } = string.Empty;
    public string AppVersion { get; init; } = string.Empty;

    public Dictionary<string, string> Extra { get; init; } = new(StringComparer.Ordinal);
}
