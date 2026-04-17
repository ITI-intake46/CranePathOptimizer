using CraneOptimizer.Domain.Common;
using CraneOptimizer.Domain.Entities;
using CraneOptimizer.Domain.GreenAreaEntities;
using CraneOptimizer.Domain.IfcEntities;
using HelixToolkit.Wpf;

public interface IRenderingService
{
    void Initialize(HelixViewport3D viewport);

    void RenderElements(IEnumerable<PrecastElement> elements);

    void RenderZone(CranePlacementZone zone);

    void RenderCrane(Crane crane);

    void RenderBestPosition(double x, double y);

    void UpdatePreview(List<DomainPoint3D> points);

    void ClearPreview();
}