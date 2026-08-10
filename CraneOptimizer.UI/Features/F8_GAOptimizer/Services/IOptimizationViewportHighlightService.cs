using CraneOptimizer.UI.Features.F8_GAOptimizer.DTOs;
using CraneOptimizer.UI.Features.F8_GAOptimizer.Rendering;

namespace CraneOptimizer.UI.Features.F8_GAOptimizer.Services;

public interface IOptimizationViewportHighlightService
{
    event Action? StateChanged;

    bool ShowOverallLiftStatus { get; }
    OptimizationViewportHighlightMode ActiveHighlightMode { get; }
    IReadOnlySet<string> EligibleLiftUnitIds { get; }
    IReadOnlySet<string> OverallLiftableIds { get; }
    IReadOnlySet<string> OverallNotLiftableIds { get; }
    IReadOnlySet<string> SelectedPositionLiftableIds { get; }
    CranePositionResult? SelectedPosition { get; }
    string? SelectedPositionId { get; }

    void LoadResult(CranePlacementResult result);
    void SelectPosition(CranePositionResult? position);
    void SetShowOverallLiftStatus(bool showOverallLiftStatus);
    void Clear();
}
