using CraneOptimizer.Domain.Entities;
using CraneOptimizer.Domain.Interfaces;
using CraneOptimizer.Domain.Schemas;
using CraneOptimizer.UI.Core.Interfaces;
using CraneOptimizer.UI.Features.F8_GAOptimizer.DTOs;
using CraneOptimizer.UI.Features.F8_GAOptimizer.Services;
using System.Threading;
using System.Windows;
using System.Windows.Controls;

namespace CraneOptimizer.UI.Features.F8_GAOptimizer.Views
{
    public partial class CraneOptimizerDialog : Window
    {
        private const double CompactDialogWidth = 460;
        private const double ExpandedDialogWidth = 780;
        private const double AdvancedPanelWidth = 320;

        private readonly IRenderingService _renderingService;
        private readonly IDialogService _dialogService;
        private readonly ISceneService _sceneService;
        private readonly CraneOptimizerDialogWorkflow _workflow;

        private CranePlacementResult? _currentResult;
        private CancellationTokenSource? _runCts;
        private bool _isRunning;
        private bool _isAdvancedPanelOpen;
        private List<PrecastElement> _cachedElements = new();

        public CranePlacementResult? LatestResult { get; private set; }

        public CraneOptimizerDialog(
            ICranePlacementService placementService,
            IRenderingService renderingService,
            IDialogService dialogService,
            ICraneRepository craneRepository,
            ISceneService sceneService)
        {
            InitializeComponent();

            _renderingService = renderingService;
            _dialogService = dialogService;
            _sceneService = sceneService;
            _workflow = new CraneOptimizerDialogWorkflow(placementService, craneRepository);

            Loaded += OnLoaded;
        }

        private async void OnLoaded(object sender, RoutedEventArgs e)
        {
            await LoadCranesAsync();
            RefreshSceneCache();
        }

        private async Task LoadCranesAsync()
        {
            var cranes = await _workflow.LoadCranesAsync();
            CboCrane.ItemsSource = cranes;
        }

        private async void BtnRun_Click(object sender, RoutedEventArgs e)
        {
            if (_isRunning)
                return;

            if (CboCrane.SelectedItem is not Crane selectedCrane)
            {
                _dialogService.ShowMessage("Please select a crane.");
                return;
            }

            _isRunning = true;
            _runCts = new CancellationTokenSource();

            RefreshSceneCache();
            ClearResultPanel();
            SelectedPointRenderer.Clear(_renderingService);

            BtnRun.IsEnabled = false;
            BtnCancel.IsEnabled = true;
            BtnApply.IsEnabled = false;

            ProgBar.IsIndeterminate = true;
            TxtStatus.Text = "Running optimizer...";

            try
            {
                var result = await _workflow.RunOptimizationAsync(
                    selectedCrane,
                    CaptureOptions(),
                    _runCts.Token);

                LatestResult = result;
                _currentResult = result;

                PointsList.ItemsSource = _workflow.CreatePointItems(result);

                if (PointsList.Items.Count > 0)
                    PointsList.SelectedIndex = 0;

                UpdateResultPanel(result);

                TxtStatus.Text = "Optimization completed.";
                ProgBar.IsIndeterminate = false;
                ProgBar.Value = 100;

                BtnApply.IsEnabled = true;
            }
            catch (OperationCanceledException)
            {
                LatestResult = null;
                _currentResult = null;
                TxtStatus.Text = "Optimization cancelled.";
                ProgBar.IsIndeterminate = false;
                ProgBar.Value = 0;
            }
            catch (Exception ex)
            {
                TxtStatus.Text = "Optimization failed.";
                ProgBar.IsIndeterminate = false;
                ProgBar.Value = 0;

                _dialogService.ShowMessage(ex.Message);
            }
            finally
            {
                _isRunning = false;
                BtnRun.IsEnabled = true;
                BtnCancel.IsEnabled = false;

                _runCts?.Dispose();
                _runCts = null;
            }
        }

        private void PointsList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (PointsList.SelectedItem is not CraneOptimizerPointListItem item)
                return;

            ShowPoint(item.Point);
            UpdateResultPanelForPoint(item.Point);
        }

        private void ShowPoint(CranePositionResult point)
        {
            SelectedPointRenderer.Render(_renderingService, _cachedElements, point);
        }

        private void UpdateResultPanel(CranePlacementResult result)
        {
            PanelResults.Visibility = Visibility.Visible;

            TxtScore.Text = $"Fitness: {result.FitnessScore:F2}";
            TxtCoverage.Text = $"Total Coverage: {result.CoverageRatio:P0}";

            if (result.Positions.Any())
                UpdateResultPanelForPoint(result.Positions[0]);
        }

        private void UpdateResultPanelForPoint(CranePositionResult point)
        {
            var summary = _workflow.CreatePointSummary(point);

            TxtPosition.Text = summary.PositionText;
            TxtCountL.Text = summary.LiftableCount.ToString();
            TxtCountNL.Text = summary.NonLiftableCount.ToString();
            TxtCountUR.Text = summary.UnreachableCount.ToString();
            TxtCoverage.Text = summary.CoverageText;
        }

        private void ClearResultPanel()
        {
            PanelResults.Visibility = Visibility.Collapsed;
            PointsList.ItemsSource = null;
        }

        private CraneOptimizerDialogOptions CaptureOptions()
        {
            return new CraneOptimizerDialogOptions(
                TxtPopulation.Text,
                TxtGenerations.Text,
                TxtMutRate.Text,
                TxtMutSigma.Text,
                TxtGridStep.Text,
                TxtConverge.Text,
                TxtPenNL.Text,
                TxtPenUR.Text);
        }

        private void RefreshSceneCache()
        {
            _cachedElements = _sceneService.GetElements() ?? new List<PrecastElement>();
        }

        private void BtnAdvancedToggle_Click(object sender, RoutedEventArgs e)
        {
            _isAdvancedPanelOpen = !_isAdvancedPanelOpen;

            AdvancedColumn.Width = _isAdvancedPanelOpen
                ? new GridLength(AdvancedPanelWidth)
                : new GridLength(0);

            PanelAdvanced.Visibility = Visibility.Visible;

            Width = _isAdvancedPanelOpen
                ? ExpandedDialogWidth
                : CompactDialogWidth;

            BtnAdvancedToggle.Content = _isAdvancedPanelOpen
                ? "Hide Advanced"
                : "Advanced Options";
        }

        private void CboCrane_SelectionChanged(object sender, SelectionChangedEventArgs e) { }
        private void RbExclusion_Checked(object sender, RoutedEventArgs e) { }

        private void BtnCancelRun_Click(object sender, RoutedEventArgs e)
        {
            if (!_isRunning)
                return;

            BtnCancel.IsEnabled = false;
            BtnApply.IsEnabled = false;
            TxtStatus.Text = "Cancel requested...";
            _runCts?.Cancel();
        }

        private void BtnApply_Click(object sender, RoutedEventArgs e)
        {
            if (_currentResult == null)
                return;

            LatestResult = _currentResult;
            DialogResult = true;
            Close();
        }

        private void BtnIncludeAll_Click(object sender, RoutedEventArgs e) { }
        private void BtnExcludeAll_Click(object sender, RoutedEventArgs e) { }
        private void BtnNoSlabs_Click(object sender, RoutedEventArgs e) { }
        private void BtnNoStairs_Click(object sender, RoutedEventArgs e) { }
        private void BtnNoRoofs_Click(object sender, RoutedEventArgs e) { }
    }
}
