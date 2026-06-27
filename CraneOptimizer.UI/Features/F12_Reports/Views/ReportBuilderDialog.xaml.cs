using System.Windows;
using System.Windows.Input;
using CraneOptimizer.UI.Features.F12_Reports.ViewModels;

namespace CraneOptimizer.UI.Features.F12_Reports.Views;

public partial class ReportBuilderDialog : Window
{
    public ReportBuilderDialog(ReportBuilderViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        viewModel.RequestClose += Close;
        Closed += (_, _) => viewModel.RequestClose -= Close;
        ReportWindowBoundsHelper.Attach(this);
    }

    private void AvailableColumns_DoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is not ReportBuilderViewModel viewModel)
            return;

        if (viewModel.AddColumnCommand.CanExecute(null))
            viewModel.AddColumnCommand.Execute(null);
    }

    private void SelectedColumns_DoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is not ReportBuilderViewModel viewModel)
            return;

        if (viewModel.RemoveColumnCommand.CanExecute(null))
            viewModel.RemoveColumnCommand.Execute(null);
    }
}
