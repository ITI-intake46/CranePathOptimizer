using CraneOptimizer.Domain.Entities;
using CraneOptimizer.Domain.Schemas;
using CraneOptimizer.UI.Core.Interfaces;
using CraneOptimizer.UI.Core.Models;
using System.Collections.ObjectModel;

namespace CraneOptimizer.UI.Core.Services
{
    public class SceneService : ISceneService
    {
        private object? _selectedObject;

        public ObservableCollection<PrecastElement> Elements { get; } = new();
        public ObservableCollection<GreenAreaZone> Zones { get; } = new();
        public ObservableCollection<SceneCrane> Cranes { get; } = new();

        private readonly List<PrecastElement> _selectedElements = new();
        public ObservableCollection<SceneDxf> DxfEntities { get; } = new();

        public SceneDxf? SelectedDxfEntity { get; private set; }
        // =========================
        // ELEMENTS
        // =========================
        public event Action<List<PrecastElement>>? ElementsChanged;
        public event Action<List<PrecastElement>>? ElementsSelectionChanged;

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

        public List<PrecastElement> GetSelectedElements() => _selectedElements.ToList();

        public void SetSelectedElements(List<PrecastElement> elements)
        {
            _selectedElements.Clear();
            _selectedElements.AddRange(elements);
            ElementsSelectionChanged?.Invoke(GetSelectedElements());
        }

        public void ClearSelectedElements()
        {
            _selectedElements.Clear();
            ElementsSelectionChanged?.Invoke(new List<PrecastElement>());
        }

        // =========================
        // ZONES
        // =========================
        public event Action<GreenAreaZone>? ZoneAdded;
        public event Action<GreenAreaZone>? ZoneRemoved;
        public event Action? ZonesCleared;
        public event Action<GreenAreaZone?>? ZoneSelected;
        public event Action<GreenAreaZone?>? ZoneUpdated;

        public GreenAreaZone? SelectedZone { get; private set; }

        public void AddZone(GreenAreaZone zone)
        {
            Zones.Add(zone);
            ZoneAdded?.Invoke(zone);
        }

        public List<GreenAreaZone> GetZones() => Zones.ToList();

        public void RemoveZone(GreenAreaZone zone)
        {
            if (!Zones.Contains(zone)) return;

            Zones.Remove(zone);

            if (SelectedZone == zone)
                SetSelectedZone(null);

            ZoneRemoved?.Invoke(zone);
        }
        public void UpdateZone(GreenAreaZone zone)
        {
            if (!Zones.Contains(zone)) return;

            // Update the zone properties as needed
            ZoneUpdated?.Invoke(zone);
        }
        public void RemoveSelectedZone()
        {
            if (SelectedZone != null)
                RemoveZone(SelectedZone);
        }

        public void ClearZones()
        {
            Zones.Clear();
            SetSelectedZone(null);
            ZonesCleared?.Invoke();
        }

        public void SetSelectedZone(GreenAreaZone? zone)
        {
            if (SelectedZone == zone) return;

            SelectedZone = zone;
            ZoneSelected?.Invoke(zone);
            SelectionChanged?.Invoke(zone);
        }

        public void ClearZoneSelection()
        {
            SetSelectedZone(null);
        }

        // =========================
        // CRANES (SceneCrane)
        // =========================
        public event Action<SceneCrane>? CraneAdded;
        public event Action<SceneCrane>? CraneRemoved;
        public event Action? CranesCleared;
        public event Action<SceneCrane>? CraneMoved;
        public event Action<SceneCrane>? CraneRotated;
        public event Action<SceneCrane>? CraneUpdated;
        public event Action<SceneCrane?>? CraneSelected;

        public SceneCrane? SelectedCrane { get; private set; }

        public List<SceneCrane> GetCranes() => Cranes.ToList();

        public void AddCrane(SceneCrane crane)
        {
            Cranes.Add(crane);
            CraneAdded?.Invoke(crane);
        }

        public void RemoveCrane(SceneCrane crane)
        {
            if (!Cranes.Contains(crane)) return;

            Cranes.Remove(crane);

            if (SelectedCrane == crane)
                SetSelectedCrane(null);

            CraneRemoved?.Invoke(crane);
        }

        public void ClearCranes()
        {
            Cranes.Clear();
            SetSelectedCrane(null);
            CranesCleared?.Invoke();
        }

        public void MoveCrane(SceneCrane crane, double x, double y, double z)
        {
            if (!Cranes.Contains(crane)) return;

            crane.Position = new Domain.Common.DomainPoint3D(x, y, z);
            CraneMoved?.Invoke(crane);
        }

        public void RotateCrane(SceneCrane crane, double angle)
        {
            if (!Cranes.Contains(crane)) return;

            crane.Rotation = angle;
            CraneRotated?.Invoke(crane);
        }

        public void UpdateCrane(SceneCrane crane)
        {
            if (!Cranes.Contains(crane)) return;

            CraneUpdated?.Invoke(crane);
        }

        public void SetSelectedCrane(SceneCrane? crane)
        {
            if (SelectedCrane == crane) return;

            SelectedCrane = crane;
            CraneSelected?.Invoke(crane);
            SelectionChanged?.Invoke(crane);
        }

        public void ClearCraneSelection()
        {
            SetSelectedCrane(null);
        }

        // =========================
        // DXF (Scene Geometry)
        // =========================
        public event Action<SceneDxf>? DxfEntityAdded;
        public event Action<SceneDxf>? DxfEntityRemoved;
        public event Action<SceneDxf>? DxfEntityUpdated;
        public event Action? DxfEntitiesCleared;
        public event Action<SceneDxf?>? DxfEntitySelected;
        public List<SceneDxf> GetDxfEntities() => DxfEntities.ToList();

        public void AddDxfEntity(SceneDxf entity)
        {
            DxfEntities.Add(entity);
            DxfEntityAdded?.Invoke(entity);
        }

        public void RemoveDxfEntity(SceneDxf entity)
        {
            if (!DxfEntities.Contains(entity)) return;

            DxfEntities.Remove(entity);

            if (SelectedDxfEntity == entity)
                SetSelectedDxfEntity(null);

            DxfEntityRemoved?.Invoke(entity);
        }

        public void RemoveSelectedDxfEntity()
        {
            if (SelectedDxfEntity != null)
                RemoveDxfEntity(SelectedDxfEntity);
        }

        public void ClearDxfEntities()
        {
            DxfEntities.Clear();
            SetSelectedDxfEntity(null);
            DxfEntitiesCleared?.Invoke();
        }

        public void UpdateDxfEntity(SceneDxf entity)
        {
            if (!DxfEntities.Contains(entity)) return;

            DxfEntityUpdated?.Invoke(entity);

            if (SelectedDxfEntity == entity)
                DxfEntitySelected?.Invoke(entity);
        }

        public void SetSelectedDxfEntity(SceneDxf? entity)
        {
            if (SelectedDxfEntity == entity) return;

            SelectedDxfEntity = entity;

            DxfEntitySelected?.Invoke(entity);
            SelectionChanged?.Invoke(entity);
        }

        public void ClearDxfSelection()
        {
            SetSelectedDxfEntity(null);
        }

        // =========================
        // GENERIC SELECTION
        // =========================
        public event Action<object?>? SelectionChanged;

        public object? GetSelectedObject() => _selectedObject;

        public void SetSelectedObject(object? obj)
        {
            _selectedObject = obj;
            SelectionChanged?.Invoke(obj);
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