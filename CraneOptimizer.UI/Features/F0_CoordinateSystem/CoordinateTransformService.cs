using CraneOptimizer.Domain.Common;
using CraneOptimizer.UI.Core.Interfaces;

namespace CraneOptimizer.UI.Features.F0_CoordinateSystem
{
    public class CoordinateTransformService : ICoordinateTransformService
    {
        private readonly CoordinateService _coordinateService;

        public CoordinateTransformService(CoordinateService coordinateService)
        {
            _coordinateService = coordinateService;
        }

        // IFC is treated as millimeters from xBIM extraction.
        // Scene is meters (engineering canonical runtime space).
        public DomainPoint3D IfcToScene(DomainPoint3D ifcPoint) => _coordinateService.NormalizePoint(ifcPoint);
        public DomainPoint3D SceneToIfc(DomainPoint3D scenePoint) => _coordinateService.DenormalizePoint(scenePoint);

        // Viewport world points are already in scene coordinates (meters),
        // assuming all geometry is rendered from scene space.
        public DomainPoint3D ViewportToScene(DomainPoint3D viewportPoint) => viewportPoint;

        public double IfcLengthToScene(double ifcLength) => _coordinateService.NormalizeLength(ifcLength);
        public double SceneLengthToIfc(double sceneLength) => _coordinateService.DenormalizeLength(sceneLength);
    }
}
