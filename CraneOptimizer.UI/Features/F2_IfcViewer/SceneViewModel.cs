using CraneOptimizer.Domain.GreenAreaEntities;
using CraneOptimizer.Domain.IfcEntities;
using CraneOptimizer.UI.Core.Base;
using System.Collections.ObjectModel;

namespace CraneOptimizer.UI.Features.F2_IfcViewer
{
    /// <summary>
    /// Holds scene state (elements + zones).
    /// Acts as bridge between UI and Application layer.
    /// </summary>
    public class SceneViewModel : ViewModelBase
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