using CraneOptimizer.Application.Reports.Definitions;
using CraneOptimizer.Application.Reports.DTOs;

namespace CraneOptimizer.Application.Reports.Services;

public interface IReportTemplateStore
{
    Task SaveAsync(ReportTemplateDto template, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ReportTemplateDto>> LoadAsync(ReportFamily family, CancellationToken cancellationToken = default);
    Task DeleteAsync(ReportFamily family, string templateName, CancellationToken cancellationToken = default);
}
