using CraneOptimizer.Domain.Common;
using CraneOptimizer.Domain.Diagnostics;
using CraneOptimizer.Domain.Entities;
using CraneOptimizer.Domain.Interfaces;
using CraneOptimizer.Domain.Schemas;
using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Interfaces;
using OptimizationScopeFilter = CraneOptimizer.Application.Optimization.GeneticAlgorithms.DTOs.OptimizationScopeFilter;
using CraneOptimizer.UI.Core.Interfaces;
using CraneOptimizer.UI.Core.Models;
using CraneOptimizer.UI.Features.F3_CraneSetup.ViewModels;
using CraneOptimizer.UI.Features.F8_GAOptimizer.DTOs;
using CraneOptimizer.UI.Features.F8_GAOptimizer.Rendering;
using CraneOptimizer.UI.Features.F8_GAOptimizer.Services;
using CraneOptimizer.UI.Features.F8_GAOptimizer.Workflows;
using System.Windows;
using System.Windows.Controls;
using CraneOptimizer.UI.Features.F8_GAOptimizer.ViewModels;
using CraneOptimizer.Application.Services;
using System.ComponentModel;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System;
using System.Collections.Generic;
using System.Linq;

namespace CraneOptimizer.UI.Features.F8_GAOptimizer.Views
{
    public partial class CraneOptimizerDialog : Window
    {
        private const double CompactDialogWidth = 560;
        private const double SidePanelWidth = 360;

        private readonly IRenderingService _renderingService;
        private readonly IDialogService _dialogService;
        private readonly ISceneService _sceneService;
        private readonly CraneSetupViewModel _craneSetup;
        private readonly ISceneRenderingManager _sceneRenderingManager;
        private readonly IImportReadinessService _readinessService;
        private readonly IOptimizationScopeEngine _scopeEngine;
        private readonly ICraneValidationService _craneValidation;
        private readonly CraneOptimizerDialogWorkflow _workflow;

        private CranePlacementResult? _currentResult;
        private CancellationTokenSource? _runCts;
        private bool _isRunning;
        private bool _isAdvancedPanelOpen;
        private bool _isFilterPanelOpen;
        private List<PrecastElement> _cachedElements = new();
        private List<ElementTypeFilterOption> _targetElementTypes = new();

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
            ISceneRenderingManager sceneRenderingManager,
            IImportReadinessService readinessService,
            IOptimizationScopeEngine scopeEngine,
            ICraneValidationService craneValidation)
        {
            InitializeComponent();

            _renderingService = renderingService;
            _dialogService = dialogService;
            _sceneService = sceneService;
            _craneSetup = craneSetup;
            _sceneRenderingManager = sceneRenderingManager;
            _readinessService = readinessService;
            _scopeEngine = scopeEngine;
            _craneValidation = craneValidation;
            _workflow = new CraneOptimizerDialogWorkflow(placementService, craneRepository);

            var vm = new CraneOptimizerDialogViewModel();
            vm.PropertyChanged += OnViewModelPropertyChanged;
            DataContext = vm;

            UpdateSidePanels();
            ContentRendered += OnContentRendered;
            Closed += OnClosed;
        }

        private async void OnContentRendered(object? sender, EventArgs e)
        {
            ContentRendered -= OnContentRendered;
            await System.Windows.Threading.Dispatcher.Yield(
                System.Windows.Threading.DispatcherPriority.Background);
            await LoadCranesAsync();
            RefreshSceneCache();
            TxtStatus.Text = "Ready";
            BtnRun.IsEnabled = true;
        }

        private async Task LoadCranesAsync()
        {
            var cranes = await _workflow.LoadCranesAsync();
            CboCrane.ItemsSource = cranes;
            RefreshLoadChartOperatingSetupOptions(CboCrane.SelectedItem as Crane);
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
                var options = CaptureOptions();

                // Refresh readiness summary to evaluate the latest selected target types/selection
                RefreshReadinessSummary();
                var vm = DataContext as CraneOptimizerDialogViewModel;

                // The only allowed run guard: no lift units at all.
                // Element Checker toggle, NeedsReview roles, and raw child diagnostics
                // do NOT block the GA run — feasibility is decided inside GA evaluation.
                if (vm?.ReadinessSummary != null && vm.ReadinessSummary.GaCandidateElements == 0)
                {
                    TxtStatus.Text = "No valid GA lift units found.";
                    ProgBar.IsIndeterminate = false;
                    ProgBar.Value = 0;

                    var explanation = new StringBuilder();
                    explanation.AppendLine("Optimization blocked: Zero valid GA lift units.");
                    explanation.AppendLine();
                    explanation.AppendLine("Details:");
                    foreach (var msg in vm.ReadinessSummary.Messages)
                    {
                        if (msg.Severity == "Error")
                        {
                            explanation.AppendLine($"- [{msg.Code}] {msg.Message}");
                            if (!string.IsNullOrEmpty(msg.RecommendedAction))
                                explanation.AppendLine($"  Recommended: {msg.RecommendedAction}");
                        }
                    }

                    _dialogService.ShowMessage(explanation.ToString());
                    return;
                }

                var settingsPreview = _workflow.BuildSettings(options);

                var result = await _workflow.RunOptimizationAsync(
                    selectedCrane,
                    options,
                    _runCts.Token);

                LatestResult = result;
                _currentResult = result;

                if (result.ReachabilityDiagnostics != null && result.ReachabilityDiagnostics.MaxHotSpotCoverage < 0.20)
                {
                    _dialogService.ShowMessage("The current allowed zones may not provide sufficient crane reach. Consider expanding or relocating the allowed zones.");
                }

                PointsList.ItemsSource = _workflow.CreatePointItems(result);

                if (PointsList.Items.Count > 0)
                    PointsList.SelectedIndex = 0;

                UpdateResultPanel(result);

                TxtStatus.Text = result.Positions.Any()
                    ? "Optimization completed."
                    : "Optimization completed with no valid positions.";

                var debugReportText = !string.IsNullOrWhiteSpace(result.DebugReportPath)
                    ? $"Debug report: {result.DebugReportPath}"
                    : !string.IsNullOrWhiteSpace(result.DebugReportError)
                        ? $"Debug report failed: {result.DebugReportError}"
                        : string.Empty;

                TxtStopReason.Text =
                    $"Capacity factors: DeratingRatio={settingsPreview.DeratingRatio:F2}, " +
                    $"SafetyFactor={settingsPreview.SafetyFactor:F2}, " +
                    $"Combined={(settingsPreview.DeratingRatio * settingsPreview.SafetyFactor):F2}" +
                    (string.IsNullOrWhiteSpace(debugReportText)
                        ? string.Empty
                        : $"{Environment.NewLine}{debugReportText}");
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
                _sceneRenderingManager.UpdateElementMaterials();
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
                _sceneRenderingManager.UpdateElementMaterials();
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
                new DomainPoint3D(point.X, point.Y, point.Z));

            _sceneRenderingManager.UpdateElementMaterials(point.ElementResults);

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

            var position = new DomainPoint3D(point.X, point.Y, point.Z);
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
                _previewSceneCrane.IsValidPlacement    = _craneValidation.ValidatePlacement(
                    _previewSceneCrane,
                    _sceneService.GetZones());
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
                };

                _previewSceneCrane.IsValidPlacement = _craneValidation.ValidatePlacement(
                    _previewSceneCrane,
                    _sceneService.GetZones());

                _sceneService.AddCrane(_previewSceneCrane);
                _sceneService.SetSelectedCrane(_previewSceneCrane);
            }
        }

        private void UpdateResultPanel(CranePlacementResult result)
        {
            PanelResults.Visibility = Visibility.Visible;

            double coveragePct = result.TotalElementCount > 0
                ? ((double)result.LiftedElementCount / result.TotalElementCount * 100.0)
                : 0.0;

            TxtScore.Text = $"Coverage: {coveragePct:F2}%";
            TxtCoverage.Text = $"Lifted: {result.LiftedElementCount} / {result.TotalElementCount} | GA Score: {result.FitnessScore:F2}";

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
            var vm = (CraneOptimizerDialogViewModel)DataContext;
            return new CraneOptimizerDialogOptions(
                TxtCoverageWeight.Text,
                TxtRelocationWeight.Text,
                TxtWireLength.Text,
                TxtAdditionalWt.Text,
                TxtDeratingRatio.Text,
                TxtSafetyFactor.Text,
                TxtMaxCranePositions.Text,
                TxtEarlyStopping.Text,
                TxtGridStep.Text,
                TxtRadiusPenaltyWeight.Text,
                TxtRadiusQualityPenaltyWeight.Text,
                TxtBalancePenaltyWeight.Text,
                TxtOverweightPenaltyWeight.Text,
                TxtHeightPenaltyWeight.Text,
                TxtRadiusStdDevPenaltyWeight.Text,
                TxtSimilarPositionPenaltyWeight.Text,
                TxtRelocationDistancePenaltyWeight.Text,
                TxtMinimumRelocationDistance.Text,
                null, // Retired DuplicatePositionDistance
                TxtMutationExplorationMultiplier.Text,
                TxtPositionSamplingAttempts.Text,
                null, // Retired MaxRadiusForNormalisation
                TxtPopulationSize.Text,
                TxtMaxGenerations.Text,
                TxtDevMutationRate.Text,
                TxtDevMutationSigma.Text,
                TxtDevCrossoverRate.Text,
                TxtDevRandomSeed.Text,
                TxtDevConvergence.Text,
                GetSelectedTargetElementTypes(),
                // New options added
                ChkEnableAdaptiveSigma.IsChecked == true ? "true" : "false",
                TxtSigmaCoolingExponent.Text,
                TxtMinimumMutationSigma.Text,
                ChkEnableConcurrentOptimization.IsChecked == true ? "true" : "false",
                null, // Retired SlewOffsetX
                null, // Retired SlewOffsetY
                null, // Retired SlewOffsetZ
                null, // Retired BoomHingeOffsetX
                null, // Retired BoomHingeOffsetY
                null, // Retired BoomHingeOffsetZ
                null, // Retired CraneFootprintRadius
                ChkEnableProfiling.IsChecked == true ? "true" : "false",
                null, // Retired ProfilingLogDirectory
                vm.EnableMultiStageDiscovery ? "true" : "false",
                vm.MultiStageCoverageTarget.ToString(System.Globalization.CultureInfo.InvariantCulture),
                vm.AdaptiveRelocationScaleFactor.ToString(System.Globalization.CultureInfo.InvariantCulture),
                vm.UseHungarianAssignment ? "true" : "false",
                vm.SelectedLoadChartOperatingSetupOption.NumberOfLinesConstraint,
                vm.SelectedLoadChartOperatingSetupOption.OperatingConditionConstraint,
                vm.SelectedGaModePresetOption.Mode,
                ChkEnableGroupedLifts.IsChecked == true ? "true" : "false");
        }

        private void RefreshSceneCache()
        {
            _cachedElements = _sceneService.GetElements() ?? new List<PrecastElement>();
            RefreshTargetElementTypes();
            RefreshReadinessSummary();
        }

        private void RefreshTargetElementTypes()
        {
            var previousSelection = _targetElementTypes
                .Where(option => option.IsSelected)
                .Select(option => option.TypeName)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            bool hadPreviousTypes = _targetElementTypes.Count > 0;

            var assemblyCounts = _cachedElements
                .Where(e => e.Role == IfcElementRole.LiftAssembly && e.IncludeInGa
                            && !string.IsNullOrWhiteSpace(e.ElementType))
                .GroupBy(e => e.ElementType.Trim(), StringComparer.OrdinalIgnoreCase)
                .Select(g => (TypeName: g.Key, Count: g.Count()))
                .OrderBy(x => x.TypeName, StringComparer.OrdinalIgnoreCase)
                .ToList();

            _targetElementTypes = assemblyCounts
                .Select(ac => new ElementTypeFilterOption(
                    ac.TypeName,
                    ac.Count,
                    !hadPreviousTypes || previousSelection.Contains(ac.TypeName)))
                .ToList();

            TargetElementTypesList.ItemsSource = _targetElementTypes;
            UpdateTargetElementSummary();
        }

        private IReadOnlyCollection<string> GetSelectedTargetElementTypes()
        {
            return _targetElementTypes
                .Where(option => option.IsSelected)
                .Select(option => option.TypeName)
                .ToList();
        }

        private void SetAllTargetElementTypes(bool isSelected)
        {
            foreach (var option in _targetElementTypes)
                option.IsSelected = isSelected;

            TargetElementTypesList.Items.Refresh();
            UpdateTargetElementSummary();
        }

        private void UpdateTargetElementSummary()
        {
            int selectedTypeCount = _targetElementTypes.Count(option => option.IsSelected);
            int selectedAssemblyCount = _targetElementTypes
                .Where(option => option.IsSelected)
                .Sum(option => option.Count);
            int totalAssemblyCount = _targetElementTypes.Sum(option => option.Count);

            TxtTargetElementSummary.Text = _targetElementTypes.Count == 0
                ? "No assembly types found"
                : $"{selectedTypeCount}/{_targetElementTypes.Count} types, {selectedAssemblyCount}/{totalAssemblyCount} assemblies";
        }

        private void BtnFilterToggle_Click(object sender, RoutedEventArgs e)
        {
            _isFilterPanelOpen = !_isFilterPanelOpen;
            UpdateSidePanels();
        }

        private void BtnAdvancedToggle_Click(object sender, RoutedEventArgs e)
        {
            _isAdvancedPanelOpen = !_isAdvancedPanelOpen;
            UpdateSidePanels();
        }

        private void UpdateSidePanels()
        {
            FilterColumn.Width = _isFilterPanelOpen
                ? new GridLength(SidePanelWidth)
                : new GridLength(0);

            PanelFilter.Visibility = _isFilterPanelOpen
                ? Visibility.Visible
                : Visibility.Collapsed;

            AdvancedColumn.Width = _isAdvancedPanelOpen
                ? new GridLength(SidePanelWidth)
                : new GridLength(0);

            PanelAdvanced.Visibility = _isAdvancedPanelOpen
                ? Visibility.Visible
                : Visibility.Collapsed;

            Width = CompactDialogWidth
                + (_isFilterPanelOpen ? SidePanelWidth : 0)
                + (_isAdvancedPanelOpen ? SidePanelWidth : 0);

            BtnFilterToggle.Content = _isFilterPanelOpen
                ? "Hide Filter"
                : "GA Filter Options";

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
        private double CalculateMarkerRadius() => 0.2;
        private double CalculateMarkerZ(double r) => 0.5;

        private void CboCrane_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            RefreshLoadChartOperatingSetupOptions(CboCrane.SelectedItem as Crane);
        }

        private void RefreshLoadChartOperatingSetupOptions(Crane? crane)
        {
            if (DataContext is CraneOptimizerDialogViewModel vm)
                vm.RefreshLoadChartOperatingSetupOptions(crane);
        }

        private void RbExclusion_Checked(object sender, RoutedEventArgs e) { }

        private void BtnApply_Click(object sender, RoutedEventArgs e)
        {
            if (_currentResult == null || PointsList.SelectedItem is not CraneOptimizerPointListItem item)
            {
                _dialogService.ShowMessage("Please select a point to apply.");
                return;
            }

            var selectedCrane = CboCrane.SelectedItem as Crane;

            if (selectedCrane != null)
                MoveOrCreateSceneCrane(selectedCrane, item.Point);

            if (_previewSceneCrane?.IsValidPlacement == false)
            {
                _dialogService.ShowMessage("Selected point is invalid: the physical crane footprint violates zone constraints.");
                return;
            }

            AppliedPosition = item.Point;
            AppliedCrane   = selectedCrane;

            // Sync position into CraneSetupViewModel (for compatibility with F3 UI)
            // Guard against null in case the service was not fully injected.
            _craneSetup?.LockPosition(new DomainPoint3D(item.Point.X, item.Point.Y, item.Point.Z));

            // The scene crane is already at the correct position (moved by ShowPoint).
            // Just close — the crane persists in the scene.
            Close();
        }

        private void OnClosed(object? sender, EventArgs e)
        {
            // Only remove the sphere marker; the scene crane stays at its last position.
            SelectedPointRenderer.Clear(_renderingService);
            _sceneRenderingManager.UpdateElementMaterials();
        }

        private void BtnIncludeAll_Click(object sender, RoutedEventArgs e) => SetAllTargetElementTypes(true);
        private void BtnExcludeAll_Click(object sender, RoutedEventArgs e) => SetAllTargetElementTypes(false);
        private void BtnNoSlabs_Click(object sender, RoutedEventArgs e) { }
        private void BtnNoStairs_Click(object sender, RoutedEventArgs e) { }
        private void BtnNoRoofs_Click(object sender, RoutedEventArgs e) { }

        private void TargetElementType_Changed(object sender, RoutedEventArgs e)
        {
            if (sender is CheckBox checkBox && checkBox.DataContext is ElementTypeFilterOption option)
                option.IsSelected = checkBox.IsChecked == true;

            UpdateTargetElementSummary();
            RefreshReadinessSummary();
        }

        private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(CraneOptimizerDialogViewModel.EnableGroupedLifts))
            {
                RefreshReadinessSummary();
            }
        }

        private List<PrecastElement> GetScopedElements(IReadOnlyList<PrecastElement> allElements)
        {
            var selectedElements = _sceneService.GetSelectedElements();
            var selectedIds = selectedElements != null && selectedElements.Count > 0
                ? selectedElements
                    .Select(e => e.ElementID)
                    .Where(id => !string.IsNullOrEmpty(id))
                    .ToHashSet()
                : null;

            var filter = new OptimizationScopeFilter
            {
                SelectedElementIds = selectedIds
            };

            return _scopeEngine.FilterElements(allElements.ToList(), filter);
        }

        private void RefreshReadinessSummary()
        {
            var vm = DataContext as CraneOptimizerDialogViewModel;
            if (vm == null) return;

            var allElements = _cachedElements;
            var scopedElements = GetScopedElements(allElements);
            var selectedTargetElementTypes = GetSelectedTargetElementTypes();

            // Always use the original single-arg BuildSummary — no service overload needed.
            var summary = _readinessService.BuildSummary(scopedElements);
            var counts = GaLiftUnitInputPreparation.CountLiftUnits(
                allElements,
                scopedElements,
                selectedTargetElementTypes);

            summary.GaCandidateElements = counts.FinalCandidateCount;

            System.Diagnostics.Debug.WriteLine(
                $"[GA_READINESS] ScopedCount={scopedElements.Count}, " +
                $"TargetTypes=[{string.Join(", ", selectedTargetElementTypes)}], " +
                $"GroupedCount={counts.GroupedAssemblyCount}, " +
                $"StandaloneCount={counts.StandaloneCount}, " +
                $"FinalCount={counts.FinalCandidateCount}");
            vm.ReadinessSummary = summary;
        }

        private sealed class ElementTypeFilterOption
        {
            public ElementTypeFilterOption(string typeName, int count, bool isSelected)
            {
                TypeName = typeName;
                Count = count;
                IsSelected = isSelected;
            }

            public string TypeName { get; }
            public int Count { get; }
            public bool IsSelected { get; set; }
            public string DisplayText => $"{TypeName} ({Count})";
        }
    }
}
