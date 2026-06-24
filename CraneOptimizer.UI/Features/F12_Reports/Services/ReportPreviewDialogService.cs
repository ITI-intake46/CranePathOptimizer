using System.Windows;
using CraneOptimizer.Application.Reports.DTOs;
using CraneOptimizer.UI.Features.F12_Reports.ViewModels;
using CraneOptimizer.UI.Features.F12_Reports.Views;

namespace CraneOptimizer.UI.Features.F12_Reports.Services;

public sealed class ReportPreviewDialogService : IReportPreviewDialogService
{
    public void ShowPreview(
        ReportDocumentDto report,
        ReportLayoutFitResult layoutFit,
        int? previewRowLimit,
        Func<Task> exportPdfAsync,
        Func<Task> exportExcelAsync)
    {
        var viewModel = new ReportPreviewViewModel(report, layoutFit, previewRowLimit, exportPdfAsync, exportExcelAsync);
        var dialog = new ReportPreviewDialog(viewModel)
        {
            Owner = System.Windows.Application.Current.Windows.OfType<Window>().FirstOrDefault(window => window.IsActive)
                ?? System.Windows.Application.Current.MainWindow
        };

        dialog.ShowDialog();
    }
}
