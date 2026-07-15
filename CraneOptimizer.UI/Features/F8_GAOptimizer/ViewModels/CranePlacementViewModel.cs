using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Models;
using CraneOptimizer.Domain.Common;
using CraneOptimizer.Domain.Entities;
using CraneOptimizer.UI.Core.Base;
using CraneOptimizer.UI.Core.Commands;
using CraneOptimizer.UI.Core.Interfaces;
using CraneOptimizer.UI.Core.Models;
using CraneOptimizer.UI.Features.F8_GAOptimizer.DTOs;
using CraneOptimizer.UI.Features.F8_GAOptimizer.Rendering;
using CraneOptimizer.UI.Features.F8_GAOptimizer.Services;
using CraneOptimizer.UI.Features.F12_Reports.Services;
using CraneOptimizer.Application.Reports.Definitions;
using CraneOptimizer.Application.Reports.DTOs;
using CraneOptimizer.Application.Reports.Services;
using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Snapshots;
using CraneOptimizer.UI.Features.F10_ProjectManagement.ViewModels;
using System.Collections.ObjectModel;
using System.Windows.Input;

namespace CraneOptimizer.UI.Features.F8_GAOptimizer.ViewModels
{
    public class CranePlacementViewModel : ViewModelBase
    {
        private readonly IRenderingService _renderingService;
        private readonly ICraneOptimizerDialogService _optimizerDialogService;
        private readonly IGAResultsWindowService _resultsWindowService;
        private readonly IGADiagnosticsWindowService _diagnosticsWindowService;
        private readonly IReportBuilderDialogService _reportBuilderDialogService;
        private readonly IViewportInteractionService _interaction;
        private readonly IViewportHitTestService _hitTest;
        private readonly IElementLiftStateService _liftStateService;
        private readonly IGaResultSnapshotProvider _snapshotProvider;
        private readonly IGaSnapshotToUiResultMapper _snapshotToUiMapper;
        private readonly IGaCurrentResultStore _currentResultStore;
        private readonly AsyncRelayCommand _optimizeCommand;
        private readonly AsyncRelayCommand _showResultsCommand;
        private readonly RelayCommand _showDiagnosticsCommand;
        private readonly AsyncRelayCommand _openAllElementsReportCommand;
        private readonly AsyncRelayCommand _openLiftingSummaryReportCommand;
        private readonly AsyncRelayCommand _refreshSavedRunsCommand;

        private bool _isPlacing;
        private bool _isDiscoveringSavedRuns;
        private bool _isLoadingSavedResult;
        private DomainPoint3D? _currentPosition;
        private Crane? _selectedCrane;
        private CranePlacementResult? _result;
        private GaRunDescriptorViewModel? _selectedSavedRun;
        private string _savedRunStatusText = "No project active.";
        private string? _savedRunWarning;
        private string _activeResultSource = "None";
        private string? _activeOptimizationRunId;
        private string? _activeResultClassification;
        private GaProjectActivationContext? _activationContext;
        private CancellationTokenSource? _activationCts;
        private int _activationGeneration;
        private bool _pendingSavedRunRefresh;

        public CranePlacementViewModel(
            IRenderingService renderingService,
            ICraneOptimizerDialogService optimizerDialogService,
            IGAResultsWindowService resultsWindowService,
            IGADiagnosticsWindowService diagnosticsWindowService,
            IReportBuilderDialogService reportBuilderDialogService,
            IViewportInteractionService interaction,
            IViewportHitTestService hitTest,
            IElementLiftStateService liftStateService,
            IGaResultSnapshotProvider snapshotProvider,
            IGaSnapshotToUiResultMapper snapshotToUiMapper,
            IGaCurrentResultStore currentResultStore)
        {
            _renderingService = renderingService;
            _optimizerDialogService = optimizerDialogService;
            _resultsWindowService = resultsWindowService;
            _diagnosticsWindowService = diagnosticsWindowService;
            _reportBuilderDialogService = reportBuilderDialogService;
            _interaction = interaction;
            _hitTest = hitTest;
            _liftStateService = liftStateService;
            _snapshotProvider = snapshotProvider;
            _snapshotToUiMapper = snapshotToUiMapper;
            _currentResultStore = currentResultStore;

            _optimizeCommand = new AsyncRelayCommand(ExecuteOptimizeAsync);
            _showResultsCommand = new AsyncRelayCommand(() => OpenResultsAsync(CancellationToken.None), CanShowResults);
            _showDiagnosticsCommand = new RelayCommand(OpenDiagnostics);
            _openAllElementsReportCommand = new AsyncRelayCommand(OpenAllElementsReportAsync);
            _openLiftingSummaryReportCommand = new AsyncRelayCommand(OpenLiftingSummaryReportAsync, CanShowResults);
            _refreshSavedRunsCommand = new AsyncRelayCommand(RefreshSavedRunsCommandAsync, () => _activationContext != null && !IsDiscoveringSavedRuns);
            StartPlacementCommand = new RelayCommand(StartPlacement);
        }

        public bool IsPlacing
        {
            get => _isPlacing;
            set
            {
                _isPlacing = value;
                OnPropertyChanged();
            }
        }

        public DomainPoint3D? CurrentPosition
        {
            get => _currentPosition;
            set
            {
                _currentPosition = value;
                OnPropertyChanged();
            }
        }

        public Crane? SelectedCrane
        {
            get => _selectedCrane;
            set
            {
                _selectedCrane = value;
                OnPropertyChanged();
            }
        }

        public CranePlacementResult? Result
        {
            get => _result;
            set
            {
                if (ReferenceEquals(_result, value))
                    return;

                _result = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(HasResult));
                OnPropertyChanged(nameof(HasLiveResult));
                OnPropertyChanged(nameof(HasAvailableResult));
                CommandManager.InvalidateRequerySuggested();
            }
        }

        private GAViewMode _currentGAViewMode = GAViewMode.FinalAssignment;
        public GAViewMode CurrentGAViewMode
        {
            get => _currentGAViewMode;
            set
            {
                if (_currentGAViewMode == value) return;
                _currentGAViewMode = value;
                OnPropertyChanged();
                RenderElementsWithCurrentStatus?.Invoke();
            }
        }

        private CranePositionResult? _selectedPosition;
        public CranePositionResult? SelectedPosition
        {
            get => _selectedPosition;
            set
            {
                if (ReferenceEquals(_selectedPosition, value))
                    return;

                _selectedPosition = value;
                OnPropertyChanged();
                PublishLiftStates(value);
            }
        }

        public ObservableCollection<GaRunDescriptorViewModel> SavedRuns { get; } = new();

        public GaRunDescriptorViewModel? SelectedSavedRun
        {
            get => _selectedSavedRun;
            set
            {
                if (ReferenceEquals(_selectedSavedRun, value))
                    return;

                _selectedSavedRun = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(SavedRunStatusText));
                CommandManager.InvalidateRequerySuggested();
            }
        }

        public bool IsDiscoveringSavedRuns
        {
            get => _isDiscoveringSavedRuns;
            private set
            {
                if (_isDiscoveringSavedRuns == value)
                    return;

                _isDiscoveringSavedRuns = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(SavedRunStatusText));
                _refreshSavedRunsCommand.RaiseCanExecuteChanged();
            }
        }

        public bool IsLoadingSavedResult
        {
            get => _isLoadingSavedResult;
            private set
            {
                if (_isLoadingSavedResult == value)
                    return;

                _isLoadingSavedResult = value;
                OnPropertyChanged();
                _showResultsCommand.RaiseCanExecuteChanged();
                _openLiftingSummaryReportCommand.RaiseCanExecuteChanged();
            }
        }

        public string SavedRunStatusText
        {
            get
            {
                if (IsDiscoveringSavedRuns)
                    return "Finding saved GA runs...";
                if (!string.IsNullOrWhiteSpace(_savedRunStatusText))
                    return _savedRunStatusText;
                return HasUsableSavedRun ? $"{SavedRuns.Count(run => run.IsUsableCompleted)} saved GA run(s) available." : "No completed GA result was found for this project.";
            }
            private set
            {
                _savedRunStatusText = value;
                OnPropertyChanged();
            }
        }

        public string? SavedRunWarning
        {
            get => _savedRunWarning;
            private set
            {
                _savedRunWarning = value;
                OnPropertyChanged();
            }
        }

        public string ActiveResultSource
        {
            get => _activeResultSource;
            private set
            {
                _activeResultSource = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(HasLiveResult));
                OnPropertyChanged(nameof(HasAvailableResult));
            }
        }

        public string? ActiveOptimizationRunId
        {
            get => _activeOptimizationRunId;
            private set
            {
                _activeOptimizationRunId = value;
                OnPropertyChanged();
            }
        }

        public string? ActiveResultClassification
        {
            get => _activeResultClassification;
            private set
            {
                _activeResultClassification = value;
                OnPropertyChanged();
            }
        }

        public bool HasLiveResult => Result != null && ActiveResultSource == "CurrentSession";
        public bool HasUsableSavedRun => SavedRuns.Any(run => run.IsUsableCompleted);
        public bool HasAvailableResult => HasLiveResult || HasUsableSavedRun;
        public bool HasResult => HasAvailableResult;

        public ICommand OptimizeCommand => _optimizeCommand;
        public ICommand ShowResultsCommand => _showResultsCommand;
        public ICommand ShowDiagnosticsCommand => _showDiagnosticsCommand;
        public ICommand OpenAllElementsReportCommand => _openAllElementsReportCommand;
        public ICommand OpenLiftingSummaryReportCommand => _openLiftingSummaryReportCommand;
        public ICommand RefreshSavedRunsCommand => _refreshSavedRunsCommand;
        public ICommand StartPlacementCommand { get; }
        public Action<DomainPoint3D>? PlaceCraneInScene { get; set; }
        public Action? RenderElementsWithCurrentStatus { get; set; }

        public bool RunOptimizationWorkflow()
        {
            var outcome = _optimizerDialogService.ShowDialog();
            return ApplyOptimizationOutcome(outcome);
        }

        private async Task ExecuteOptimizeAsync()
        {
            try
            {
                var outcome = _optimizerDialogService.ShowDialog();
                if (!ApplyOptimizationOutcome(outcome))
                    return;

                var context = _activationContext;
                if (context != null)
                    await RefreshSavedRunsAsync(context, _activationGeneration, _activationCts?.Token ?? CancellationToken.None);
            }
            catch (OperationCanceledException)
            {
                // Project switched or closed while the saved run index was refreshing.
            }
            catch (Exception ex)
            {
                SavedRunWarning = "The optimization completed, but the saved GA run list could not be refreshed.";
                System.Diagnostics.Debug.WriteLine($"[GA Run Refresh After Optimize Failed] Project={_activationContext?.ProjectGuid} Run={ActiveOptimizationRunId} Error={ex}");
            }
        }

        private bool ApplyOptimizationOutcome(CraneOptimizerDialogOutcome? outcome)
        {
            if (outcome?.Result == null || outcome.Result.Positions.Count == 0)
                return false;

            Result = outcome.Result;
            ActiveResultSource = outcome.Result.SnapshotSourceKind ?? "CurrentSession";
            ActiveOptimizationRunId = outcome.Result.OptimizationRunId;
            ActiveResultClassification = "Current";
            SelectedPosition = outcome.AppliedPosition ?? outcome.Result.Positions[0];
            RenderElementsWithCurrentStatus?.Invoke();
            RaiseResultAvailabilityChanged();
            return true;
        }

        private void StartPlacement()
        {
            // Re-arm placement reliably. If the same mode is already active,
            // keep state but avoid stacking duplicate modes.
            IsPlacing = true;
            if (_interaction.CurrentMode is Interaction.CranePlacementMode)
                return;

            if (PlaceCraneInScene != null)
            {
                _interaction.PushMode(new Interaction.CranePlacementMode(this, _hitTest, _interaction, PlaceCraneInScene));
            }
        }

        private bool CanShowResults()
        {
            return !IsLoadingSavedResult && HasAvailableResult;
        }

        public async Task ActivateProjectAsync(
            GaProjectActivationContext context,
            CancellationToken cancellationToken = default)
        {
            _activationCts?.Cancel();
            _activationCts?.Dispose();
            _activationCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            _activationContext = context;
            var generation = ++_activationGeneration;
            _pendingSavedRunRefresh = false;

            Result = null;
            SelectedPosition = null;
            ActiveResultSource = "None";
            ActiveOptimizationRunId = null;
            ActiveResultClassification = null;
            SavedRuns.Clear();
            SelectedSavedRun = null;
            SavedRunWarning = null;
            SavedRunStatusText = "Finding saved GA runs...";
            RaiseResultAvailabilityChanged();

            await RefreshSavedRunsAsync(context, generation, _activationCts.Token);
        }

        public async Task RefreshSavedRunsAsync(CancellationToken cancellationToken = default)
        {
            if (_activationContext == null)
                return;

            await RefreshSavedRunsAsync(_activationContext, _activationGeneration, cancellationToken);
        }

        public void DeactivateProject(string projectGuid)
        {
            _activationCts?.Cancel();
            if (!string.IsNullOrWhiteSpace(projectGuid))
                _currentResultStore.Remove(projectGuid);

            _activationContext = null;
            _pendingSavedRunRefresh = false;
            SavedRuns.Clear();
            SelectedSavedRun = null;
            Result = null;
            SelectedPosition = null;
            ActiveResultSource = "None";
            ActiveOptimizationRunId = null;
            ActiveResultClassification = null;
            SavedRunWarning = null;
            SavedRunStatusText = "No project active.";
            _liftStateService.Clear();
            RaiseResultAvailabilityChanged();
        }

        private async Task RefreshSavedRunsAsync(
            GaProjectActivationContext context,
            int generation,
            CancellationToken cancellationToken)
        {
            if (IsDiscoveringSavedRuns)
            {
                _pendingSavedRunRefresh = true;
                return;
            }

            do
            {
                _pendingSavedRunRefresh = false;
                try
                {
                    IsDiscoveringSavedRuns = true;
                    var previousSelection = SelectedSavedRun?.OptimizationRunId;
                    var descriptors = await Task
                        .Run(() => _snapshotProvider.ListRunsAsync(context, cancellationToken), cancellationToken)
                        .ConfigureAwait(true);
                    if (generation != _activationGeneration || _activationContext?.ProjectGuid != context.ProjectGuid)
                        return;

                    SavedRuns.Clear();
                    foreach (var descriptor in descriptors)
                        SavedRuns.Add(new GaRunDescriptorViewModel(descriptor));

                    SelectedSavedRun = SelectSavedRun(previousSelection);
                    SavedRunWarning = SelectedSavedRun?.Descriptor.Warnings.FirstOrDefault();
                    SavedRunStatusText = HasUsableSavedRun
                        ? $"{SavedRuns.Count(run => run.IsUsableCompleted)} saved GA run(s) available."
                        : "No completed GA result was found for this project.";
                }
                catch (OperationCanceledException)
                {
                    // Expected during project switching.
                }
                catch (Exception ex)
                {
                    SavedRunWarning = "Saved GA runs could not be discovered for this project.";
                    SavedRunStatusText = "Saved GA run discovery failed.";
                    System.Diagnostics.Debug.WriteLine($"[GA Run Discovery Failed] Project={context.ProjectGuid} Error={ex}");
                }
                finally
                {
                    IsDiscoveringSavedRuns = false;
                    RaiseResultAvailabilityChanged();
                }
            }
            while (_pendingSavedRunRefresh
                   && generation == _activationGeneration
                   && _activationContext?.ProjectGuid == context.ProjectGuid
                   && !cancellationToken.IsCancellationRequested);
        }

        private GaRunDescriptorViewModel? SelectSavedRun(string? previousSelection)
        {
            if (!string.IsNullOrWhiteSpace(previousSelection))
            {
                var preserved = SavedRuns.FirstOrDefault(run => run.OptimizationRunId == previousSelection && run.IsUsableCompleted);
                if (preserved != null)
                    return preserved;
            }

            if (!string.IsNullOrWhiteSpace(ActiveOptimizationRunId))
            {
                var active = SavedRuns.FirstOrDefault(run => run.OptimizationRunId == ActiveOptimizationRunId && run.IsUsableCompleted);
                if (active != null)
                    return active;
            }

            return SavedRuns.FirstOrDefault(run => run.IsUsableCompleted);
        }

        private async Task OpenResultsAsync(CancellationToken cancellationToken)
        {
            if (!CanShowResults())
                return;

            try
            {
                IsLoadingSavedResult = true;

                if (HasLiveResult && Result != null)
                {
                    ShowResultWindow(Result);
                    return;
                }

                var context = _activationContext;
                var selected = SelectedSavedRun ?? SelectSavedRun(null);
                if (context == null || selected == null)
                {
                    SavedRunWarning = "No completed GA result was found for this project.";
                    return;
                }

                var resolved = await _snapshotProvider.ResolveSelectedAsync(context, selected.Descriptor, cancellationToken).ConfigureAwait(true);
                if (resolved.Snapshot == null)
                {
                    SavedRunWarning = resolved.Warnings.FirstOrDefault() ?? "The selected GA result could not be loaded.";
                    return;
                }

                var mapped = _snapshotToUiMapper.Map(
                    resolved.Snapshot,
                    new GaSnapshotUiMappingContext(
                        resolved.SourceKind,
                        resolved.Classification,
                        resolved.SourceKind != "CurrentSession",
                        resolved.Classification is "Current" or "CurrentByIfcHash" or "CurrentProjectUnverified"));

                Result = mapped;
                ActiveOptimizationRunId = mapped.OptimizationRunId;
                ActiveResultSource = resolved.SourceKind;
                ActiveResultClassification = resolved.Classification;
                CurrentGAViewMode = GAViewMode.FinalAssignment;
                SelectedPosition = mapped.Positions.OrderBy(position => position.SetupIndex).FirstOrDefault();
                SavedRunWarning = resolved.Warnings.FirstOrDefault();
                RenderElementsWithCurrentStatus?.Invoke();
                ShowResultWindow(mapped);
            }
            catch (OperationCanceledException)
            {
                // Project switched or closed.
            }
            catch (Exception ex)
            {
                SavedRunWarning = "The selected GA result could not be opened.";
                System.Diagnostics.Debug.WriteLine($"[GA Result Open Failed] Project={_activationContext?.ProjectGuid} Run={SelectedSavedRun?.OptimizationRunId} Error={ex}");
            }
            finally
            {
                IsLoadingSavedResult = false;
                RaiseResultAvailabilityChanged();
            }
        }

        private void ShowResultWindow(CranePlacementResult result)
        {
            _resultsWindowService.Show(result,
                point =>
                {
                    SelectedPosition = point;
                    if (!result.IsReadOnlyRestoredResult)
                        RenderElementsWithCurrentStatus?.Invoke();
                },
                viewMode =>
                {
                    CurrentGAViewMode = viewMode;
                },
                CurrentGAViewMode);
        }

        private async Task RefreshSavedRunsCommandAsync()
        {
            if (_activationContext == null)
                return;

            await RefreshSavedRunsAsync(_activationContext, _activationGeneration, _activationCts?.Token ?? CancellationToken.None);
        }

        private void OpenDiagnostics()
        {
            _diagnosticsWindowService.Show(CurrentGAViewMode);
        }

        private async Task OpenAllElementsReportAsync()
        {
            await _reportBuilderDialogService.ShowReportBuilderAsync(
                ReportFamily.ElementList,
                ReportTemplateCatalog.BasicElementList,
                "All Elements");
        }

        private async Task OpenLiftingSummaryReportAsync()
        {
            if (!CanShowResults())
                return;

            var selectedRun = SelectedSavedRun ?? SelectSavedRun(null);
            if (selectedRun != null)
                SelectedSavedRun = selectedRun;

            await _reportBuilderDialogService.ShowReportBuilderAsync(
                ReportFamily.LiftingResult,
                ReportTemplateCatalog.GaResultReport,
                "All Submitted Lift Units",
                selectedRun == null
                    ? null
                    : new GaReportRunSelection
                    {
                        ProjectGuid = selectedRun.Descriptor.ProjectGuid,
                        OptimizationRunId = selectedRun.Descriptor.OptimizationRunId,
                        SourceKind = selectedRun.Descriptor.SourceKind,
                        Classification = selectedRun.Descriptor.Classification,
                        SnapshotPath = selectedRun.Descriptor.SnapshotPath
                    });

        }

        private void RaiseResultAvailabilityChanged()
        {
            OnPropertyChanged(nameof(HasResult));
            OnPropertyChanged(nameof(HasLiveResult));
            OnPropertyChanged(nameof(HasUsableSavedRun));
            OnPropertyChanged(nameof(HasAvailableResult));
            OnPropertyChanged(nameof(SavedRunStatusText));
            _showResultsCommand.RaiseCanExecuteChanged();
            _openLiftingSummaryReportCommand.RaiseCanExecuteChanged();
            _refreshSavedRunsCommand.RaiseCanExecuteChanged();
            CommandManager.InvalidateRequerySuggested();
        }

        private void PublishLiftStates(CranePositionResult? position)
        {
            if (position == null)
            {
                _liftStateService.Clear();
                return;
            }

            _liftStateService.SetStates(position.ElementResults.Select(result => new ElementLiftStateDto(
                result.ElementId,
                GetLiftStateDisplay(result.Status),
                position.X,
                position.Y,
                result.Radius,
                result.RequiredCapacity)));
        }

        private static string GetLiftStateDisplay(LiftStatus status)
        {
            return status switch
            {
                LiftStatus.Liftable => "Liftable",
                LiftStatus.OutOfRadius => "Out of radius",
                LiftStatus.Overweight => "Overweight",
                LiftStatus.HeightFail => "Height fail",
                LiftStatus.SpecialCase => "Special case",
                LiftStatus.NotCharted => "Not charted",
                _ => status.ToString()
            };
        }

        public void Place(DomainPoint3D point)
        {
            if (!IsPlacing || SelectedCrane == null)
                return;

            CurrentPosition = point;

            SelectedPointRenderer.Clear(_renderingService);
            SelectedPointRenderer.RenderPlacementPoint(_renderingService, point);

            IsPlacing = false;
        }
    }
}
