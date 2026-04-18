using CraneOptimizer.Domain.Schemas;

namespace CraneOptimizer.UI.Core.Interfaces
{
    public interface ISceneService
    {
        List<PrecastElement> GetElements();
        List<GreenAreaZone> GetZones();
        void SetElements(List<PrecastElement> elements);
        void AddZone(GreenAreaZone zone);
        void ClearZones();
        void ClearElements();
    }
}
