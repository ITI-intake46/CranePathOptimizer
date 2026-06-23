using CraneOptimizer.Application.Reports.DTOs;

namespace CraneOptimizer.Application.Reports.Services;

public interface IReportExcelExporter
{
    Task ExportAsync(ReportDocumentDto report, string outputPath, CancellationToken cancellationToken = default);
}
