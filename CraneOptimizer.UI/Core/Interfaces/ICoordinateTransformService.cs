using CraneOptimizer.Domain.Common;

namespace CraneOptimizer.UI.Core.Interfaces
{
    public interface ICoordinateTransformService
    {
        DomainPoint3D IfcToScene(DomainPoint3D ifcPoint);
        DomainPoint3D SceneToIfc(DomainPoint3D scenePoint);
        DomainPoint3D ViewportToScene(DomainPoint3D viewportPoint);

        double IfcLengthToScene(double ifcLength);
        double SceneLengthToIfc(double sceneLength);
    }
}
