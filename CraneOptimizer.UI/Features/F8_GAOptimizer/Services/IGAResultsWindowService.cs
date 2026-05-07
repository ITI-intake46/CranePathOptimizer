using CraneOptimizer.UI.Features.F8_GAOptimizer.DTOs;

namespace CraneOptimizer.UI.Features.F8_GAOptimizer.Services
{
    public interface IGAResultsWindowService
    {
        void Show(CranePlacementResult result, Action<CranePositionResult?> selectedPointChanged);
    }
}
