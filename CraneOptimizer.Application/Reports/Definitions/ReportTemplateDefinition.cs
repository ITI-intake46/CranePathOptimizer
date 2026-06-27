namespace CraneOptimizer.Application.Reports.Definitions;

public sealed class ReportTemplateDefinition
{
    public string Name { get; init; } = string.Empty;
    public ReportFamily Family { get; init; }
    public string DefaultItemsIncluded { get; init; } = string.Empty;
    public IReadOnlyList<ReportColumnDefinition> Columns { get; init; } = Array.Empty<ReportColumnDefinition>();
}
