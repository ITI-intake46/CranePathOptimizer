using System.Windows.Input;
using CraneOptimizer.Application.GAAnalysis;
using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Snapshots;
using CraneOptimizer.UI.Core.Base;
using CraneOptimizer.UI.Core.Commands;
using CraneOptimizer.UI.Features.F8_GAOptimizer.DTOs;
using CraneOptimizer.UI.Features.F8_GAOptimizer.Services;
using CraneOptimizer.UI.Features.F10_ProjectManagement.ViewModels;

namespace CraneOptimizer.UI.Features.F8_GAOptimizer.ViewModels;

public sealed class GaLiftAnalysisViewModel : ViewModelBase, IDisposable
{
    private readonly IGaLiftAnalysisJobCoordinator _coordinator;
    private readonly IGaLiftAnalysisPresentationActions _presentationActions;
    private readonly IGaLiftSummaryPresentationMapper _mapper;
    private GaResultSnapshotDto? _snapshot;
    private GaLiftAnalysisJobState? _job;
    private GaLiftAnalysisResult? _analysis;
    private GaLiftSummaryPresentationModel? _summary;
    private int _progressPercent;
    private bool _isPreparing;
    private bool _isJobPreparing;
    private bool _isMapping;
    private bool _hasGenerationError;
    private string? _feedbackText;
    private CancellationTokenSource? _presentationCts;
    private Task _presentationTask = Task.CompletedTask;
    private int _presentationVersion;
    private bool _disposed;

    public GaLiftAnalysisViewModel(
        IGaLiftAnalysisJobCoordinator coordinator,
        IGaLiftAnalysisPresentationActions presentationActions,
        IGaLiftSummaryPresentationMapper mapper)
    {
        _coordinator = coordinator;
        _presentationActions = presentationActions;
        _mapper = mapper;
        _coordinator.JobChanged += OnJobChanged;
        TryAgainCommand = new RelayCommand(TryAgain, () => _snapshot != null && HasGenerationError);
        OpenFolderCommand = new AsyncRelayCommand(OpenFolderAsync, () => Analysis != null);
        CopySummaryCommand = new RelayCommand(CopySummary, () => Summary != null);
        CloseCommand = new RelayCommand(() => CloseRequested?.Invoke(this, EventArgs.Empty));
    }

    public event EventHandler? CloseRequested;

    public ICommand TryAgainCommand { get; }
    public ICommand OpenFolderCommand { get; }
    public ICommand CopySummaryCommand { get; }
    public ICommand CloseCommand { get; }

    public GaLiftAnalysisResult? Analysis
    {
        get => _analysis;
        private set
        {
            _analysis = value;
            OnPropertyChanged();
            CommandManager.InvalidateRequerySuggested();
        }
    }

    public GaLiftSummaryPresentationModel? Summary
    {
        get => _summary;
        private set
        {
            _summary = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(IsSummaryVisible));
            CommandManager.InvalidateRequerySuggested();
        }
    }

    public int ProgressPercent
    {
        get => _progressPercent;
        private set
        {
            _progressPercent = value;
            OnPropertyChanged();
        }
    }

    public bool IsPreparing
    {
        get => _isPreparing;
        private set
        {
            _isPreparing = value;
            OnPropertyChanged();
        }
    }

    public bool HasGenerationError
    {
        get => _hasGenerationError;
        private set
        {
            _hasGenerationError = value;
            OnPropertyChanged();
            CommandManager.InvalidateRequerySuggested();
        }
    }

    public bool IsSummaryVisible => Summary != null;

    public string? FeedbackText
    {
        get => _feedbackText;
        private set
        {
            _feedbackText = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(HasFeedback));
        }
    }

    public bool HasFeedback => !string.IsNullOrWhiteSpace(FeedbackText);

    public void Initialize(GaResultSnapshotDto snapshot)
    {
        _snapshot = snapshot;
        Attach(_coordinator.StartOrGet(snapshot));
    }

    public void InitializeSaved(GaLiftAnalysisResult analysis)
    {
        Analysis = analysis;
        ProgressPercent = 100;
        _isJobPreparing = false;
        HasGenerationError = false;
        StartPresentationMapping(analysis);
    }

    private void TryAgain()
    {
        if (_snapshot == null)
            return;

        FeedbackText = null;
        HasGenerationError = false;
        Attach(_coordinator.Restart(_snapshot));
    }

    private async Task OpenFolderAsync()
    {
        if (Analysis == null)
            return;

        FeedbackText = await _presentationActions.TryOpenSummaryFolderAsync(Analysis)
            ? "Summary folder opened."
            : "The saved summary folder could not be found.";
    }

    private void CopySummary()
    {
        if (Summary == null)
            return;

        FeedbackText = _presentationActions.TryCopySummary(Summary.CopyText)
            ? "Lift Summary copied."
            : "Lift Summary could not be copied.";
    }

    private void Attach(GaLiftAnalysisJobState job)
    {
        _job = job;
        Apply(job);
    }

    private void OnJobChanged(object? sender, GaLiftAnalysisJobState state)
    {
        if (_job?.Key != state.Key || _disposed)
            return;

        var dispatcher = System.Windows.Application.Current?.Dispatcher;
        if (dispatcher == null || dispatcher.CheckAccess())
            Apply(state);
        else
            _ = dispatcher.InvokeAsync(() => Apply(state));
    }

    private void Apply(GaLiftAnalysisJobState state)
    {
        ProgressPercent = state.ProgressPercent;
        _isJobPreparing = !state.IsTerminal;
        UpdatePreparing();
        HasGenerationError = state.Status == GaLiftAnalysisJobStatus.Failed;

        if (state.Result == null)
            return;

        if (ReferenceEquals(Analysis, state.Result) && Summary != null)
            return;

        Analysis = state.Result;
        StartPresentationMapping(state.Result);
    }

    private void StartPresentationMapping(GaLiftAnalysisResult analysis)
    {
        _presentationCts?.Cancel();
        _presentationCts?.Dispose();
        _presentationCts = new CancellationTokenSource();
        int version = ++_presentationVersion;
        Summary = null;
        _isMapping = true;
        UpdatePreparing();
        _presentationTask = MapAndApplyAsync(
            analysis,
            version,
            _presentationCts.Token);
    }

    private async Task MapAndApplyAsync(
        GaLiftAnalysisResult analysis,
        int version,
        CancellationToken cancellationToken)
    {
        try
        {
            GaLiftSummaryPresentationModel summary = await _mapper
                .MapAsync(analysis, cancellationToken);
            if (_disposed || version != _presentationVersion || cancellationToken.IsCancellationRequested)
                return;
            Summary = summary;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            System.Diagnostics.Debug.WriteLine("[Lift Summary Mapping Cancelled]");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Lift Summary Mapping Failed] {ex}");
            if (!_disposed && version == _presentationVersion)
                HasGenerationError = true;
        }
        finally
        {
            if (!_disposed && version == _presentationVersion)
            {
                _isMapping = false;
                UpdatePreparing();
            }
        }
    }

    private void UpdatePreparing()
    {
        IsPreparing = _isJobPreparing || _isMapping;
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        _presentationCts?.Cancel();
        _presentationCts?.Dispose();
        _coordinator.JobChanged -= OnJobChanged;
    }
}
