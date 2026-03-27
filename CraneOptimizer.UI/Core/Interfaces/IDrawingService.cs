using CraneOptimizer.Domain.Common;
using CraneOptimizer.Domain.GreenAreaEntities;

namespace CraneOptimizer.UI.Core.Interfaces
{
    public interface IDrawingService
    {
        void Start();
        void AddPoint(Point3D point);
        CranePlacementZone? BuildZone();
    }
}
