using CraneOptimizer.Domain.GreenAreaEntities;
using CraneOptimizer.Domain.IfcEntities;
using HelixToolkit.Wpf;
using DomainPoint = CraneOptimizer.Domain.Common.Point3D;

namespace CraneOptimizer.UI.Core.Interfaces
{
    /// <summary>
    /// Responsible for ALL 3D rendering (Helix).
    /// UI must not directly deal with Helix internals.
    /// </summary>
    public interface IRenderingService
    {
        void Initialize(HelixViewport3D viewport);

        void RenderElements(IEnumerable<PrecastElement> elements);

        void RenderZone(CranePlacementZone zone);
        void RenderBestPosition(double x, double y);
        void UpdatePreview(List<DomainPoint> points);
        void ClearPreview();
    }
}