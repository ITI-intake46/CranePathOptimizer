namespace CraneOptimizer.Application.Reports.Definitions;

public sealed class ReportColumnDefinition
{
    public string Key { get; init; } = string.Empty;
    public string Header { get; init; } = string.Empty;
    public ReportColumnValueKind ValueKind { get; init; } = ReportColumnValueKind.Text;
    public bool IsSelected { get; init; } = true;
    public int Order { get; init; }
    public string? Format { get; init; }
    public string? Alignment { get; init; }
    public string? TextSize { get; init; }
    public string? Category { get; init; }
    public string? Unit { get; init; }
}
