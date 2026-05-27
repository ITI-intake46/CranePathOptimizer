using System.Collections.Generic;
using CraneOptimizer.Domain.Schemas;

namespace CraneOptimizer.UI.Core.Interfaces
{
    public interface ICameraService
    {
        bool IsTopPlanViewActive { get; set; }
        void ResetCamera();
        void ResetToDefaultView();
        void ZoomExtents();
        void ZoomToElements(IReadOnlyList<PrecastElement> elements);
        void ZoomToFrontView(IReadOnlyList<PrecastElement> elements, bool animate = true);
        void ZoomToTopView(IReadOnlyList<PrecastElement> elements, bool animate = true);
        void UpdateClipping(IReadOnlyList<PrecastElement>? elements);
    }
}
