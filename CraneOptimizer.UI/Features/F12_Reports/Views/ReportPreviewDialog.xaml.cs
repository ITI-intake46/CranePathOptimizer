using System.Windows;
using System.Windows.Input;
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

    private void PreviewGrid_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        PreviewScrollViewer.ScrollToVerticalOffset(PreviewScrollViewer.VerticalOffset - e.Delta);
        e.Handled = true;
    }
}
