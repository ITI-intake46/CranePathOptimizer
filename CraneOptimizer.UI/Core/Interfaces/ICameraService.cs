using System.Collections.Generic;
using CraneOptimizer.Domain.Schemas;

namespace CraneOptimizer.UI.Core.Interfaces
{
    public interface ICameraService
    {
        void ResetCamera();
        void ZoomExtents();
        void UpdateClipping(IReadOnlyList<PrecastElement>? elements);
    }
}
