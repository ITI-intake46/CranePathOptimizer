namespace CraneOptimizer.Application.Reports.Models;

public sealed class PdfExportOptions
{
    public int MaxRowsPerTable { get; init; } = 200;
    public bool AddTruncationNote { get; init; } = true;
    public ReportTheme Theme { get; init; } = new();
}
