using CraneOptimizer.Domain.GreenAreaEntities;
using CraneOptimizer.Domain.IfcEntities;

namespace CraneOptimizer.UI.Core.Interfaces
{
    public interface ISceneService
    {
        List<PrecastElement> GetElements();
        List<CranePlacementZone> GetZones();
        void SetElements(List<PrecastElement> elements);
        void AddZone(CranePlacementZone zone);
        void ClearZones();
        void ClearElements();
    }
}
