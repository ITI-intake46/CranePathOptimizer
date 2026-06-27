using CraneOptimizer.Application.Reports.Models;

namespace CraneOptimizer.Application.Reports.Interfaces;

[System.Obsolete("Unused legacy headless reporting foundation.")]
public interface IReportExportService
{
    Task<ReportExportResult> ExportAsync(
        ReportDocument document,
        ReportFormat format,
        string outputPath,
        CancellationToken cancellationToken = default);
}
