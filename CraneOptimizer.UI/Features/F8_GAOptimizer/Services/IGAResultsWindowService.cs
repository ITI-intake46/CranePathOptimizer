using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Models;
using CraneOptimizer.UI.Features.F8_GAOptimizer.DTOs;

namespace CraneOptimizer.UI.Features.F8_GAOptimizer.Services
{
    public interface IGAResultsWindowService
    {
        void Show(
            CranePlacementResult result, 
            Action<CranePositionResult?> selectedPointChanged,
            Action<GAViewMode> viewModeChanged,
            GAViewMode initialViewMode);
    }
}
