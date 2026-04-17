using CraneOptimizer.Domain.Common;
using CraneOptimizer.Domain.GreenAreaEntities;

namespace CraneOptimizer.UI.Core.Interfaces
{
    public interface IDrawingService
    {
        void Start();
        void AddPoint(DomainPoint3D point);
        CranePlacementZone? BuildZone();
    }
}
