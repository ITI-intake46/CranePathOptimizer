using CraneOptimizer.Domain.Common;
using CraneOptimizer.Domain.Entities;
using CraneOptimizer.Domain.Interfaces;
using CraneOptimizer.Domain.Schemas;
using CraneOptimizer.UI.Core.Interfaces;
using CraneOptimizer.UI.Features.F3_CraneSetup.ViewModels;
using CraneOptimizer.UI.Features.F8_GAOptimizer.DTOs;
using CraneOptimizer.UI.Features.F8_GAOptimizer.Rendering;
using CraneOptimizer.UI.Features.F8_GAOptimizer.Services;
using CraneOptimizer.UI.Features.F8_GAOptimizer.Workflows;
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
        private readonly IIfcReaderRepository? _ifcReader;
        private readonly CraneSetupViewModel _craneSetup;
        private readonly CraneOptimizerDialogWorkflow _workflow;
        private readonly DomainPoint3D? _origin;
        private readonly DomainPoint3D? _placementOffset;

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
            ISceneService sceneService,
            CraneSetupViewModel craneSetup,
            IIfcReaderRepository? ifcReader = null,
            DomainPoint3D? origin = null,
            DomainPoint3D? placementOffset = null)
        {
            InitializeComponent();

            _renderingService = renderingService;
            _dialogService = dialogService;
            _sceneService = sceneService;
            _ifcReader = ifcReader;
            _craneSetup = craneSetup;        
            _workflow = new CraneOptimizerDialogWorkflow(placementService, craneRepository);
            _origin = origin;
            _placementOffset = placementOffset;

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
            LatestResult = null;
            _currentResult = null;

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

                TxtStatus.Text = result.Positions.Any()
                    ? "Optimization completed."
                    : "Optimization completed with no valid positions.";
                ProgBar.IsIndeterminate = false;
                ProgBar.Value = 100;

                BtnApply.IsEnabled = result.Positions.Any();
            }
            catch (OperationCanceledException)
            {
                LatestResult = null;
                _currentResult = null;
                ClearResultPanel();
                SelectedPointRenderer.Clear(_renderingService);
                TxtStatus.Text = "Optimization cancelled.";
                ProgBar.IsIndeterminate = false;
                ProgBar.Value = 0;
            }
            catch (Exception ex)
            {
                LatestResult = null;
                _currentResult = null;
                ClearResultPanel();
                SelectedPointRenderer.Clear(_renderingService);
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
            SelectedPointRenderer.Render(
                _renderingService,
                _cachedElements,
                point,
                _ifcReader,
                _origin,
                _placementOffset);
        }

        private void UpdateResultPanel(CranePlacementResult result)
        {
            PanelResults.Visibility = Visibility.Visible;

            TxtScore.Text = $"Fitness: {result.FitnessScore:F2}";
            TxtCoverage.Text = $"Total Coverage: {result.CoverageRatio:P0}";

            if (!result.Positions.Any())
            {
                TxtPosition.Text = "No valid positions found.";
                TxtCountL.Text = "0";
                TxtCountNL.Text = "0";
                TxtCountUR.Text = "0";
                return;
            }

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
            TxtScore.Text = string.Empty;
            TxtPosition.Text = string.Empty;
            TxtCountL.Text = string.Empty;
            TxtCountNL.Text = string.Empty;
            TxtCountUR.Text = string.Empty;
            TxtCoverage.Text = string.Empty;
            TxtStopReason.Text = string.Empty;
        }

        private CraneOptimizerDialogOptions CaptureOptions()
        {
            return new CraneOptimizerDialogOptions(
                TxtCoverageWeight.Text,
                TxtRelocationWeight.Text,
                TxtSafetyFactor.Text,
                TxtRiggingLength.Text,
                TxtMaxCranePositions.Text,
                TxtEarlyStopping.Text,
                TxtGridStep.Text,
                TxtRadiusPenaltyWeight.Text,
                TxtBalancePenaltyWeight.Text,
                TxtOverweightPenaltyWeight.Text,
                TxtHeightPenaltyWeight.Text,
                TxtDevMutationRate.Text,
                TxtDevMutationSigma.Text,
                TxtDevCrossoverRate.Text,
                TxtDevRandomSeed.Text,
                TxtDevConvergence.Text);
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

            PanelAdvanced.Visibility = _isAdvancedPanelOpen
                ? Visibility.Visible
                : Visibility.Collapsed;

            Width = _isAdvancedPanelOpen
                ? ExpandedDialogWidth
                : CompactDialogWidth;

            BtnAdvancedToggle.Content = _isAdvancedPanelOpen
                ? "Hide Advanced"
                : "Advanced Options";
        }

        private void BtnCancelRun_Click(object sender, RoutedEventArgs e)
        {
            if (!_isRunning)
                return;

            BtnCancel.IsEnabled = false;
            BtnApply.IsEnabled = false;
            TxtStatus.Text = "Cancel requested...";
            _runCts?.Cancel();
        }

        // ============================
        // HELPERS
        // ============================
        private double CalculateMarkerRadius() => 200;
        private double CalculateMarkerZ(double r) => 500;

        private void CboCrane_SelectionChanged(object sender, SelectionChangedEventArgs e) { }
        private void RbExclusion_Checked(object sender, RoutedEventArgs e) { }
        public CranePositionResult? AppliedPosition { get; private set; }

        private void BtnApply_Click(object sender, RoutedEventArgs e)
        {
            if (_currentResult == null || PointsList.SelectedItem is not CraneOptimizerPointListItem item)
            {
                _dialogService.ShowMessage("Please select a point to apply.");
                return;
            }

            AppliedPosition = item.Point;

            // Lock the crane position to the selected GA result
            var lockedPosition = new DomainPoint3D(
                item.Point.X,
                item.Point.Y,
                0
            );

            _craneSetup.LockPosition(lockedPosition);

            Close();
        }
        private void BtnIncludeAll_Click(object sender, RoutedEventArgs e) { }
        private void BtnExcludeAll_Click(object sender, RoutedEventArgs e) { }
        private void BtnNoSlabs_Click(object sender, RoutedEventArgs e) { }
        private void BtnNoStairs_Click(object sender, RoutedEventArgs e) { }
        private void BtnNoRoofs_Click(object sender, RoutedEventArgs e) { }
        //private void BtnApply_Click(object sender, RoutedEventArgs e)
        //{
        //    if (_currentResult == null)
        //        return;

        //    LatestResult = _currentResult;
        //    DialogResult = true;
        //    Close();
        //}
    }
}
