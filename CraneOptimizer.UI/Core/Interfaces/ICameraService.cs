using System.Collections.Generic;
using CraneOptimizer.Domain.Schemas;

namespace CraneOptimizer.UI.Core.Interfaces
{
    public interface ICameraService
    {
        void ResetCamera();
        void ResetToDefaultView();
        void ZoomExtents();
        void ZoomToElements(IReadOnlyList<PrecastElement> elements);
        void ZoomToFrontView(IReadOnlyList<PrecastElement> elements, bool animate = true);
        void UpdateClipping(IReadOnlyList<PrecastElement>? elements);
    }
}
