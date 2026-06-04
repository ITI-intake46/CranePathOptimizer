using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Models;
using CraneOptimizer.Domain.Schemas;

namespace CraneOptimizer.UI.Features.F2_IfcViewer
{
    /// <summary>
    /// Contract for resolving the visualization status of an element.
    /// Allows swapping the color strategy for different visualization modes.
    /// </summary>
    public interface IElementColorStrategy
    {
        ElementVisualizationStatus GetStatus(PrecastElement element, LiftStatus? liftStatus, bool isSelected);
    }
}
