using CraneOptimizer.Application.Reports.DTOs;

namespace CraneOptimizer.UI.Features.F12_Reports.Services;

public interface IReportPreviewDialogService
{
    void ShowPreview(
        ReportDocumentDto report,
        ReportLayoutFitResult layoutFit,
        Func<Task> exportPdfAsync,
        Func<Task> exportExcelAsync);
}
