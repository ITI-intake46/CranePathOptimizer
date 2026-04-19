using CraneOptimizer.Domain.Entities;
using CraneOptimizer.Domain.Schemas;

namespace CraneOptimizer.UI.Core.Interfaces
{
    public interface ISceneService
    {
        // =========================
        // Elements
        // =========================
        List<PrecastElement> GetElements();
        void SetElements(List<PrecastElement> elements);
        void ClearElements();

        event Action<List<PrecastElement>>? ElementsChanged;

        // =========================
        // Zones
        // =========================
        List<GreenAreaZone> GetZones();
        void AddZone(GreenAreaZone zone);
        void ClearZones();

        event Action<GreenAreaZone>? ZoneAdded;
        event Action? ZonesCleared;

        // =========================
        // Crane 
        // =========================
        Crane? GetCrane();
        void SetCrane(Crane crane);
        void ClearCrane();

        event Action<Crane?>? CraneChanged;

        // =========================
        // Selection 
        // =========================
        object? GetSelectedObject();
        void SetSelectedObject(object? obj);

        event Action<object?>? SelectionChanged;
    }
}
