using System.Windows;
using CraneOptimizer.Application.GAAnalysis;
using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Snapshots;
using CraneOptimizer.UI.Features.F8_GAOptimizer.ViewModels;

namespace CraneOptimizer.UI.Features.F8_GAOptimizer.Views;

public partial class GaLiftAnalysisWindow : Window
{
    private readonly GaLiftAnalysisViewModel _viewModel;

    public GaLiftAnalysisWindow(GaLiftAnalysisViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = viewModel;
        _viewModel.CloseRequested += OnCloseRequested;
        Closed += OnClosed;
    }

    public void Initialize(GaResultSnapshotDto snapshot) => _viewModel.Initialize(snapshot);
    public void InitializeSaved(GaLiftAnalysisResult analysis) => _viewModel.InitializeSaved(analysis);

    private void OnCloseRequested(object? sender, EventArgs e) => Close();

    private void OnClosed(object? sender, EventArgs e)
    {
        _viewModel.CloseRequested -= OnCloseRequested;
    }
}
