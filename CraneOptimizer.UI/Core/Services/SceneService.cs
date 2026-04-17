using CraneOptimizer.Domain.GreenAreaEntities;
using CraneOptimizer.Domain.IfcEntities;
using CraneOptimizer.UI.Core.Interfaces;
using System.Collections.ObjectModel;

namespace CraneOptimizer.UI.Core.Services
{
    /// <summary>
    /// Holds scene state (elements + zones).
    /// Acts as bridge between UI and Application layer.
    /// </summary>
    public class SceneService : ISceneService
    {
        public ObservableCollection<PrecastElement> Elements { get; } = new();
        public ObservableCollection<CranePlacementZone> Zones { get; } = new();

        // =========================
        // Events (IMPORTANT)
        // =========================
        public event Action<List<PrecastElement>>? ElementsChanged;
        public event Action<CranePlacementZone>? ZoneAdded;

        // =========================
        // Methods
        // =========================

        public void SetElements(List<PrecastElement> elements)
        {
            Elements.Clear();
            Elements.AddRange(elements);

            ElementsChanged?.Invoke(elements);
        }

        public void AddZone(CranePlacementZone zone)
        {
            Zones.Add(zone);
            ZoneAdded?.Invoke(zone);
        }

        public List<PrecastElement> GetElements() => Elements.ToList();

        public List<CranePlacementZone> GetZones() => Zones.ToList();
        
        public void ClearZones() => Zones.Clear();

        public void ClearElements() => Elements.Clear();
    }
    public static class ObservableCollectionExtensions
    {
        public static void AddRange<T>(this ObservableCollection<T> collection, IEnumerable<T> items)
        {
            foreach (var item in items)
                collection.Add(item);
        }
    }
}