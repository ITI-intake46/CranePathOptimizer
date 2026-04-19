using CraneOptimizer.Domain.Entities;
using CraneOptimizer.Domain.Schemas;
using CraneOptimizer.UI.Core.Interfaces;
using System.Collections.ObjectModel;

namespace CraneOptimizer.UI.Core.Services
{
    public class SceneService : ISceneService
    {
        // =========================
        // Internal State
        // =========================
        private Crane? _crane;
        private object? _selectedObject;

        // =========================
        // Collections
        // =========================
        public ObservableCollection<PrecastElement> Elements { get; } = new();
        public ObservableCollection<GreenAreaZone> Zones { get; } = new();

        // =========================
        // Events
        // =========================
        public event Action<List<PrecastElement>>? ElementsChanged;
        public event Action<GreenAreaZone>? ZoneAdded;
        public event Action? ZonesCleared;
        public event Action<Crane?>? CraneChanged;
        public event Action<object?>? SelectionChanged;

        // =========================
        // Elements
        // =========================
        public void SetElements(List<PrecastElement> elements)
        {
            Elements.Clear();
            Elements.AddRange(elements);

            ElementsChanged?.Invoke(elements);
        }

        public List<PrecastElement> GetElements() => Elements.ToList();

        public void ClearElements()
        {
            Elements.Clear();
            ElementsChanged?.Invoke(new List<PrecastElement>());
        }

        // =========================
        // Zones
        // =========================
        public void AddZone(GreenAreaZone zone)
        {
            Zones.Add(zone);
            ZoneAdded?.Invoke(zone);
        }

        public List<GreenAreaZone> GetZones() => Zones.ToList();

        public void ClearZones()
        {
            Zones.Clear();
            ZonesCleared?.Invoke();
        }

        // =========================
        // Crane
        // =========================
        public Crane? GetCrane() => _crane;

        public void SetCrane(Crane crane)
        {
            _crane = crane;
            CraneChanged?.Invoke(_crane);
        }

        public void ClearCrane()
        {
            _crane = null;
            CraneChanged?.Invoke(null);
        }

        // =========================
        // Selection
        // =========================
        public object? GetSelectedObject() => _selectedObject;

        public void SetSelectedObject(object? obj)
        {
            _selectedObject = obj;
            SelectionChanged?.Invoke(obj);
        }
    }

    // =========================
    // Extensions
    // =========================
    public static class ObservableCollectionExtensions
    {
        public static void AddRange<T>(this ObservableCollection<T> collection, IEnumerable<T> items)
        {
            foreach (var item in items)
                collection.Add(item);
        }
    }
}