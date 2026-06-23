using CraneOptimizer.Application.Reports.DTOs;

namespace CraneOptimizer.Application.Reports.Services;

public interface IReportPdfExporter
{
    Task ExportAsync(ReportDocumentDto report, string outputPath, CancellationToken cancellationToken = default);
}
