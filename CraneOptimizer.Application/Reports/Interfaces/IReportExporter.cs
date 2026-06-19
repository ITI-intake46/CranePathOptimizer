using CraneOptimizer.Application.Reports.Models;

namespace CraneOptimizer.Application.Reports.Interfaces;

public interface IReportExporter
{
    ReportFormat Format { get; }

    Task<ReportExportResult> ExportAsync(
        ReportDocument document,
        string outputPath,
        CancellationToken cancellationToken = default);
}
