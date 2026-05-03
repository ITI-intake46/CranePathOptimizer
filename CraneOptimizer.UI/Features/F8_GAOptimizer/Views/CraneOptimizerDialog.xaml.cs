using CraneOptimizer.Domain.Common;
using CraneOptimizer.Domain.Entities;
using CraneOptimizer.Domain.Interfaces;
using CraneOptimizer.Domain.Schemas;
using CraneOptimizer.UI.Core.Interfaces;
using CraneOptimizer.UI.Core.Models;
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

        /// <summary>
        /// The single SceneCrane that lives in the scene while the dialog is open.
        /// Moves to each selected point; persists in the scene after the dialog closes.
        /// </summary>
        private SceneCrane? _previewSceneCrane;

        public CranePlacementResult? LatestResult { get; private set; }
        public CranePositionResult? AppliedPosition { get; private set; }
        public Crane? AppliedCrane { get; private set; }

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
            Closed += OnClosed;
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
            // Color all elements by their GA status for this position
            SelectedPointRenderer.Render(
                _renderingService,
                _cachedElements,
                point,
                _ifcReader,
                _origin,
                _placementOffset);

            // Move the persistent scene crane to this GA position (creates it if needed)
            if (CboCrane.SelectedItem is Crane crane)
                MoveOrCreateSceneCrane(crane, point);
        }

        /// <summary>
        /// Ensures exactly one crane exists in the scene and positions it at
        /// the specified GA candidate point.  On the first call the crane is
        /// created (any existing cranes are cleared first).  On subsequent
        /// calls the same instance is moved so there is never a duplicate.
        /// </summary>
        private void MoveOrCreateSceneCrane(Crane crane, CranePositionResult point)
        {
            var activeSpec = crane.Specifications
                ?.OrderByDescending(s => s.MaxRadius)
                .FirstOrDefault();

            double boomLength = activeSpec != null
                ? Math.Clamp(activeSpec.MaxBoomLength * 0.6,
                             activeSpec.MinBoomLength,
                             activeSpec.MaxBoomLength)
                : 20;

            var position = new DomainPoint3D(point.X, point.Y, 0);
            var pivotOffset = new DomainPoint3D(0, 0, Math.Max(1.2, crane.VehicleHeight));

            // Check whether our tracked crane is still in the scene
            bool craneIsInScene = _previewSceneCrane != null &&
                                  _sceneService.GetCranes().Contains(_previewSceneCrane);

            if (craneIsInScene)
            {
                // Move the existing crane — no duplicate is created
                _previewSceneCrane!.CraneDefinition    = crane;
                _previewSceneCrane.ActiveSpecification = activeSpec;
                _previewSceneCrane.Position            = position;
                _previewSceneCrane.PivotOffset         = pivotOffset;
                _previewSceneCrane.BoomLength          = boomLength;
                _previewSceneCrane.BoomAngle           = 45;
                _sceneService.UpdateCrane(_previewSceneCrane);
            }
            else
            {
                // First time (or the crane was externally removed): clear any
                // stale cranes and add a fresh one so there is always exactly one.
                _sceneService.ClearCranes();

                _previewSceneCrane = new SceneCrane
                {
                    CraneDefinition    = crane,
                    ActiveSpecification = activeSpec,
                    Position           = position,
                    Rotation           = 0,
                    PivotOffset        = pivotOffset,
                    BoomLength         = boomLength,
                    BoomAngle          = 45,
                    IsValidPlacement   = true
                };

                _sceneService.AddCrane(_previewSceneCrane);
                _sceneService.SetSelectedCrane(_previewSceneCrane);
            }
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

        private void BtnApply_Click(object sender, RoutedEventArgs e)
        {
            if (_currentResult == null || PointsList.SelectedItem is not CraneOptimizerPointListItem item)
            {
                _dialogService.ShowMessage("Please select a point to apply.");
                return;
            }

            AppliedPosition = item.Point;
            AppliedCrane   = CboCrane.SelectedItem as Crane;

            // Lock the crane position into CraneSetupViewModel (for compatibility)
            _craneSetup.LockPosition(new DomainPoint3D(item.Point.X, item.Point.Y, 0));

            // The scene crane is already at the correct position (moved by ShowPoint).
            // Just close — the crane persists in the scene.
            Close();
        }

        private void OnClosed(object? sender, EventArgs e)
        {
            // Only remove the sphere marker; the scene crane stays at its last position.
            SelectedPointRenderer.Clear(_renderingService);
        }

        private void BtnIncludeAll_Click(object sender, RoutedEventArgs e) { }
        private void BtnExcludeAll_Click(object sender, RoutedEventArgs e) { }
        private void BtnNoSlabs_Click(object sender, RoutedEventArgs e) { }
        private void BtnNoStairs_Click(object sender, RoutedEventArgs e) { }
        private void BtnNoRoofs_Click(object sender, RoutedEventArgs e) { }
    }
}
