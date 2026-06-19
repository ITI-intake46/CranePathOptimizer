namespace CraneOptimizer.Application.Reports.Models;

public sealed class ReportDefinition
{
    public string ReportId { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public IReadOnlyList<ReportFormat> SupportedFormats { get; init; } = Array.Empty<ReportFormat>();
}
