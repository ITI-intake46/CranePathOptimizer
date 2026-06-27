using CraneOptimizer.Application.Reports.Definitions;

namespace CraneOptimizer.Application.Reports.DTOs;

public sealed class ReportTemplateDto
{
    public string TemplateName { get; init; } = string.Empty;
    public ReportFamily InternalFamily { get; init; }
    public IReadOnlyList<ReportColumnDefinition> Columns { get; init; } = Array.Empty<ReportColumnDefinition>();
    public string? ItemsIncluded { get; init; }
    public string? Sorting { get; init; }
    public string? Grouping { get; init; }
    public string? Filters { get; init; }
    public string? Formatting { get; init; }
    public string? TextAlignment { get; init; }
    public double? TextSizePt { get; init; }
}
