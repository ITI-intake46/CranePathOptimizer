using CraneOptimizer.Domain.Common;
using CraneOptimizer.Domain.Schemas;

namespace CraneOptimizer.UI.Core.Interfaces
{
    public interface IDrawingService
    {
        void Start();
        void AddPoint(DomainPoint3D point);
        GreenAreaZone? BuildZone();
    }
}
