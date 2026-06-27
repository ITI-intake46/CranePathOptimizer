namespace CraneOptimizer.Application.Reports.Models;

public sealed class ReportTable
{
    public string Name { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;

    public List<ReportColumn> Columns { get; init; } = new();
    public List<ReportRow> Rows { get; init; } = new();
}
