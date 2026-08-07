using CraneOptimizer.UI.Core.Interfaces;
using CraneOptimizer.UI.Features.F8_GAOptimizer.DTOs;
using CraneOptimizer.UI.Features.F8_GAOptimizer.Rendering;

namespace CraneOptimizer.UI.Features.F8_GAOptimizer.Services;

public sealed class OptimizationViewportHighlightService : IOptimizationViewportHighlightService
{
    private static readonly IReadOnlySet<string> EmptyIds =
        new HashSet<string>(StringComparer.Ordinal);

    private readonly ISceneRenderingManager? _sceneRenderingManager;
    private CranePlacementResult? _activeResult;
    private OptimizationLiftStatusData? _liftStatusData;
    private CranePositionResult? _selectedPosition;
    private bool _showOverallLiftStatus;

    public OptimizationViewportHighlightService(
        ISceneRenderingManager? sceneRenderingManager = null)
    {
        _sceneRenderingManager = sceneRenderingManager;
    }

    public event Action? StateChanged;

    public bool ShowOverallLiftStatus => _showOverallLiftStatus;

    public OptimizationViewportHighlightMode ActiveHighlightMode { get; private set; }

    public IReadOnlySet<string> EligibleLiftUnitIds =>
        _liftStatusData?.EligibleLiftUnitIds ?? EmptyIds;

    public IReadOnlySet<string> OverallLiftableIds =>
        _liftStatusData?.OverallLiftableIds ?? EmptyIds;

    public IReadOnlySet<string> OverallNotLiftableIds =>
        _liftStatusData?.OverallNotLiftableIds ?? EmptyIds;

    public IReadOnlySet<string> SelectedPositionLiftableIds =>
        _liftStatusData?.GetLiftableIds(_selectedPosition) ?? EmptyIds;

    public CranePositionResult? SelectedPosition => _selectedPosition;

    public string? SelectedPositionId => _selectedPosition?.PositionId;

    public void LoadResult(CranePlacementResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        if (!ReferenceEquals(_activeResult, result) || _liftStatusData == null)
        {
            _activeResult = result;
            _liftStatusData = OptimizationLiftStatusResolver.Resolve(result);
            _selectedPosition = result.Positions?.FirstOrDefault();
        }

        ApplyCurrentHighlight();
        StateChanged?.Invoke();
    }

    public void SelectPosition(CranePositionResult? position)
    {
        if (ReferenceEquals(_selectedPosition, position))
            return;

        _selectedPosition = position;

        if (!_showOverallLiftStatus)
            ApplyCurrentHighlight();

        StateChanged?.Invoke();
    }

    public void SetShowOverallLiftStatus(bool showOverallLiftStatus)
    {
        if (_showOverallLiftStatus == showOverallLiftStatus)
            return;

        _showOverallLiftStatus = showOverallLiftStatus;
        ApplyCurrentHighlight();
        StateChanged?.Invoke();
    }

    public void Clear()
    {
        _activeResult = null;
        _liftStatusData = null;
        _selectedPosition = null;
        _showOverallLiftStatus = false;
        ActiveHighlightMode = OptimizationViewportHighlightMode.None;
        _sceneRenderingManager?.ClearOptimizationResultHighlight();
        StateChanged?.Invoke();
    }

    private void ApplyCurrentHighlight()
    {
        if (_liftStatusData == null)
        {
            ActiveHighlightMode = OptimizationViewportHighlightMode.None;
            _sceneRenderingManager?.ClearOptimizationResultHighlight();
            return;
        }

        if (!_showOverallLiftStatus && _selectedPosition == null)
        {
            ActiveHighlightMode = OptimizationViewportHighlightMode.None;
            _sceneRenderingManager?.ClearOptimizationResultHighlight();
            return;
        }

        OptimizationLiftStatusSelection selection = _liftStatusData.CreateSelection(
            _showOverallLiftStatus,
            _selectedPosition);
        ActiveHighlightMode = _showOverallLiftStatus
            ? OptimizationViewportHighlightMode.Overall
            : OptimizationViewportHighlightMode.SelectedPosition;
        _sceneRenderingManager?.ApplyOptimizationResultHighlight(
            selection.LiftableVisualElementIds,
            selection.NotLiftableVisualElementIds);
    }
}
