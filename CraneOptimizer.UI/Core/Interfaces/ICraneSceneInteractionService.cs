using CraneOptimizer.Domain.Common;
using CraneOptimizer.Domain.Entities;
using CraneOptimizer.UI.Core.Models;

namespace CraneOptimizer.UI.Core.Interfaces
{
    public interface ICraneSceneInteractionService
    {
        SceneCrane? SelectedSceneCrane { get; }
        bool IsDraggingCrane { get; }
        DomainPoint3D? DragOffset { get; }

        event Action? StateChanged;

        void SelectSceneCrane(SceneCrane? crane);
        void PlaceCrane(Crane crane, DomainPoint3D point);
        double EvaluateFitness(SceneCrane crane);
        void StartDrag(DomainPoint3D clickPoint);
        void DragCrane(DomainPoint3D worldPoint);
        void EndDrag();
    }
}
