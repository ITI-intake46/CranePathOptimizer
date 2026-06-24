namespace CraneOptimizer.Application.Reports.DTOs;

public sealed class ReportDataSourceAuditRowDto
{
    public int No { get; init; }
    public string Area { get; init; } = string.Empty;
    public string ReportColumn { get; init; } = string.Empty;
    public string DisplayedHeader { get; init; } = string.Empty;
    public string CurrentSource { get; init; } = string.Empty;
    public string SourceObjectOrFile { get; init; } = string.Empty;
    public string SourceField { get; init; } = string.Empty;
    public string CurrentAvailability { get; init; } = string.Empty;
    public int RowsFound { get; init; }
    public string WhyItMayShowDash { get; init; } = string.Empty;
    public string RequiredAction { get; init; } = string.Empty;
}
