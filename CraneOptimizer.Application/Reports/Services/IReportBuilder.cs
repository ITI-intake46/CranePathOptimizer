using CraneOptimizer.Application.Reports.DTOs;

namespace CraneOptimizer.Application.Reports.Services;

public interface IReportBuilder
{
    Task<ReportDocumentDto> BuildAsync(ReportBuildRequestDto request, CancellationToken cancellationToken = default);
}
