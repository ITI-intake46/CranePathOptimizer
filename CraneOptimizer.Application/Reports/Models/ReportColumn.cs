namespace CraneOptimizer.Application.Reports.Models;

public sealed class ReportColumn
{
    public string Key { get; init; } = string.Empty;
    public string Header { get; init; } = string.Empty;
    public string? Unit { get; init; }
    public ReportColumnType Type { get; init; } = ReportColumnType.Text;
}
