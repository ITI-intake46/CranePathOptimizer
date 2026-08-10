using CraneOptimizer.Application.Reports.Definitions;
using CraneOptimizer.Application.Reports.DTOs;
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

    public async Task ShowReportBuilderAsync(
        ReportFamily family,
        string templateName,
        string itemsIncluded,
        GaReportRunSelection? runSelection = null)
    {
        var viewModel = _serviceProvider.GetRequiredService<ReportBuilderViewModel>();
        await viewModel.InitializeAsync(family, templateName, itemsIncluded, runSelection);

        var dialog = new ReportBuilderDialog(viewModel)
        {
            Owner = System.Windows.Application.Current.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive)
                ?? System.Windows.Application.Current.MainWindow
        };

        dialog.ShowDialog();
    }
}
