using System.Windows;
using CraneOptimizer.UI.Features.F12_Reports.ViewModels;

namespace CraneOptimizer.UI.Features.F12_Reports.Views;

public partial class ReportPreviewDialog : Window
{
    public ReportPreviewDialog(ReportPreviewViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        viewModel.RequestClose += Close;
        Closed += (_, _) => viewModel.RequestClose -= Close;
        ReportWindowBoundsHelper.Attach(this);
    }
}
