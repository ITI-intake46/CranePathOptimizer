using CraneOptimizer.Application.Reports.Definitions;
using CraneOptimizer.UI.Features.F12_Reports.ViewModels;
using CraneOptimizer.UI.Features.F12_Reports.Views;
using Microsoft.Extensions.DependencyInjection;
using System.Windows;

namespace CraneOptimizer.UI.Features.F12_Reports.Services;

public sealed class ReportBuilderDialogService : IReportBuilderDialogService
{
    private readonly IServiceProvider _serviceProvider;

    public ReportBuilderDialogService(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public void ShowReportBuilder(ReportFamily family, string templateName, string itemsIncluded)
    {
        var viewModel = _serviceProvider.GetRequiredService<ReportBuilderViewModel>();
        viewModel.Initialize(family, templateName, itemsIncluded);

        var dialog = new ReportBuilderDialog(viewModel)
        {
            Owner = System.Windows.Application.Current.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive)
                ?? System.Windows.Application.Current.MainWindow
        };

        dialog.ShowDialog();
    }
}
