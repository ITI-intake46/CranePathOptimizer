using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Models;
using CraneOptimizer.Domain.Schemas;

namespace CraneOptimizer.UI.Features.F2_IfcViewer
{
    /// <summary>
    /// Default strategy for resolving visualization status based on liftability and selection.
    /// </summary>
    public class LiftabilityColorStrategy : IElementColorStrategy
    {
        public ElementVisualizationStatus GetStatus(PrecastElement element, LiftStatus? liftStatus, bool isSelected)
        {
            if (isSelected)
                return ElementVisualizationStatus.Selected;

            if (liftStatus == null)
                return ElementVisualizationStatus.Default;

            return liftStatus.Value switch
            {
                LiftStatus.Liftable => ElementVisualizationStatus.Liftable,
                LiftStatus.Overweight => ElementVisualizationStatus.Overweight,
                LiftStatus.OutOfRadius => ElementVisualizationStatus.OutOfRadius,
                LiftStatus.HeightFail => ElementVisualizationStatus.HeightIssue,
                _ => ElementVisualizationStatus.Default
            };
        }
    }
}
