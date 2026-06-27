using CraneOptimizer.Application.Reports.Models;

namespace CraneOptimizer.Application.Reports.Interfaces;

[System.Obsolete("Unused legacy headless reporting foundation. UI uses IReportPdfExporter or IReportExcelExporter instead.")]
public interface IReportExporter
{
    ReportFormat Format { get; }

    Task<ReportExportResult> ExportAsync(
        ReportDocument document,
        string outputPath,
        CancellationToken cancellationToken = default);
}
