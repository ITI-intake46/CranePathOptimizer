using CraneOptimizer.Domain.Schemas;
using CraneOptimizer.UI.Core.Interfaces;
using CraneOptimizer.UI.Core.Models;
using System.Collections.Immutable;
using System.Collections.ObjectModel;
using CraneOptimizer.Domain.Entities;

namespace CraneOptimizer.UI.Core.Services
{
    public class SceneService : ISceneService
    {
        private object? _selectedObject;

        public ObservableCollection<PrecastElement> Elements { get; } = new();
        private readonly object _elementSnapshotLock = new();
        private IReadOnlyList<PrecastElement> _elementSnapshot =
            Array.Empty<PrecastElement>();
        public ObservableCollection<GreenAreaZone> Zones { get; } = new();
        private readonly object _zoneSnapshotLock = new();
        private readonly Dictionary<Guid, GreenAreaZone> _zonesById = new();
        private ImmutableList<GreenAreaZone> _zoneSnapshot =
            ImmutableList<GreenAreaZone>.Empty;
        private int _zoneChangeBatchDepth;
        private bool _hasBatchedZoneChanges;
        public ObservableCollection<SceneCrane> Cranes { get; } = new();

        private readonly List<PrecastElement> _selectedElements = new();
        public ObservableCollection<SceneDxf> DxfEntities { get; } = new();

        private readonly List<SceneDxf> _selectedDxfEntities = new();
        public SceneDxf? SelectedDxfEntity => _selectedDxfEntities.LastOrDefault();
        // =========================
        // ELEMENTS
        // =========================
        public event Action<List<PrecastElement>>? ElementsChanged;
        public event Action<List<PrecastElement>>? ElementsSelectionChanged;

        public void SetElements(List<PrecastElement> elements)
        {
            lock (_elementSnapshotLock)
                _elementSnapshot = elements.AsReadOnly();
            Elements.Clear();
            Elements.AddRange(elements);
            ElementsChanged?.Invoke(elements);
        }

        public List<PrecastElement> GetElements() => Elements.ToList();

        public IReadOnlyList<PrecastElement> GetElementSnapshot()
        {
            lock (_elementSnapshotLock)
                return _elementSnapshot;
        }

        public void ClearElements()
        {
            lock (_elementSnapshotLock)
                _elementSnapshot = Array.Empty<PrecastElement>();
            Elements.Clear();
            ElementsChanged?.Invoke(new List<PrecastElement>());
        }

        public List<PrecastElement> GetSelectedElements() => _selectedElements.ToList();

        public void SetSelectedElements(List<PrecastElement> elements)
        {
            if (elements.Count == 1 && _selectedObject == elements[0]) return;
            if (elements.Count == 0 && _selectedObject == null) return;

            _selectedElements.Clear();
            _selectedElements.AddRange(elements);
            
            if (elements.Count == 1) _selectedObject = elements[0];
            else if (elements.Count == 0) _selectedObject = null;
            else _selectedObject = elements; // Multi-selection

            ElementsSelectionChanged?.Invoke(GetSelectedElements());
            SelectionChanged?.Invoke(_selectedObject);
        }

        public void ClearSelectedElements()
        {
            bool clearedGenericSelection =
                _selectedObject is PrecastElement ||
                _selectedObject is List<PrecastElement>;
            _selectedElements.Clear();
            if (clearedGenericSelection)
                _selectedObject = null;
            ElementsSelectionChanged?.Invoke(new List<PrecastElement>());
            if (clearedGenericSelection)
                SelectionChanged?.Invoke(null);
        }

        // =========================
        // ZONES
        // =========================
        public event Action<GreenAreaZone>? ZoneAdded;
        public event Action<GreenAreaZone>? ZoneRemoved;
        public event Action? ZonesCleared;
        public event Action? ZonesChanged;
        public event Action<GreenAreaZone?>? ZoneSelected;
        public event Action<GreenAreaZone>? ZoneUpdated;

        public GreenAreaZone? SelectedZone { get; private set; }

        public void AddZone(GreenAreaZone zone)
        {
            lock (_zoneSnapshotLock)
            {
                if (_zonesById.ContainsKey(zone.ZoneId))
                    return;
                _zonesById[zone.ZoneId] = zone;
                _zoneSnapshot = _zoneSnapshot.Add(zone);
            }
            Zones.Add(zone);
            ZoneAdded?.Invoke(zone);
            NotifyZonesChanged();
        }

        public List<GreenAreaZone> GetZones() => Zones.ToList();

        public virtual IReadOnlyList<GreenAreaZone> GetZoneSnapshot()
        {
            lock (_zoneSnapshotLock)
                return _zoneSnapshot;
        }

        public void RemoveZone(GreenAreaZone zone)
            => RemoveZoneById(zone.ZoneId);

        private void RemoveZoneById(Guid zoneId)
        {
            GreenAreaZone? indexedZone;
            lock (_zoneSnapshotLock)
            {
                _zonesById.Remove(zoneId, out indexedZone);
                _zoneSnapshot = _zoneSnapshot.RemoveAll(
                    zone => zone.ZoneId == zoneId);
            }

            List<GreenAreaZone> collectionZones = Zones
                .Where(item => item.ZoneId == zoneId)
                .ToList();
            if (indexedZone == null && collectionZones.Count == 0)
                return;

            foreach (GreenAreaZone collectionZone in collectionZones)
                Zones.Remove(collectionZone);

            if (SelectedZone?.ZoneId == zoneId)
                SetSelectedZone(null);

            ZoneRemoved?.Invoke(indexedZone ?? collectionZones[0]);
            NotifyZonesChanged();
        }
        public void ApplyZoneChanges(IReadOnlyList<Guid> removeZoneIds, IReadOnlyList<GreenAreaZone> addZones)
        {
            _zoneChangeBatchDepth++;
            try
            {
                foreach (Guid zoneId in removeZoneIds)
                    RemoveZoneById(zoneId);

                foreach (GreenAreaZone zone in addZones)
                    AddZone(zone);
            }
            finally
            {
                _zoneChangeBatchDepth--;
                if (_zoneChangeBatchDepth == 0 && _hasBatchedZoneChanges)
                {
                    _hasBatchedZoneChanges = false;
                    ZonesChanged?.Invoke();
                }
            }
        }
        public void UpdateZone(GreenAreaZone zone)
        {
            if (!Zones.Contains(zone)) return;

            // Update the zone properties as needed
            ZoneUpdated?.Invoke(zone);
            NotifyZonesChanged();
        }
        public void RemoveSelectedZone()
        {
            if (SelectedZone != null)
                RemoveZone(SelectedZone);
        }

        public void ClearZones()
        {
            lock (_zoneSnapshotLock)
            {
                _zonesById.Clear();
                _zoneSnapshot =
                    ImmutableList<GreenAreaZone>.Empty;
            }
            Zones.Clear();
            SetSelectedZone(null);
            ZonesCleared?.Invoke();
            NotifyZonesChanged();
        }

        private void NotifyZonesChanged()
        {
            if (_zoneChangeBatchDepth > 0)
            {
                _hasBatchedZoneChanges = true;
                return;
            }

            ZonesChanged?.Invoke();
        }

        public void SetSelectedZone(GreenAreaZone? zone)
        {
            if (SelectedZone == zone) return;

            SelectedZone = zone;
            _selectedObject = zone;
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
            _selectedObject = crane;
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
        public event Action<List<SceneDxf>>? DxfEntitiesSelectionChanged;
        public List<SceneDxf> GetDxfEntities() => DxfEntities.ToList();
        public List<SceneDxf> GetSelectedDxfEntities() => _selectedDxfEntities.ToList();

        public void AddDxfEntity(SceneDxf entity)
        {
            DxfEntities.Add(entity);
            DxfEntityAdded?.Invoke(entity);
        }

        public void RemoveDxfEntity(SceneDxf entity)
        {
            if (!DxfEntities.Contains(entity)) return;

            DxfEntities.Remove(entity);

            if (_selectedDxfEntities.Remove(entity))
                RaiseDxfSelectionChanged();

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
            SetSelectedDxfEntities(entity == null
                ? new List<SceneDxf>()
                : new List<SceneDxf> { entity });
        }

        public void SetSelectedDxfEntities(List<SceneDxf> entities)
        {
            var distinct = entities
                .Where(DxfEntities.Contains)
                .DistinctBy(entity => entity.Id)
                .ToList();

            if (_selectedDxfEntities.Select(entity => entity.Id)
                .SequenceEqual(distinct.Select(entity => entity.Id)))
                return;

            _selectedDxfEntities.Clear();
            _selectedDxfEntities.AddRange(distinct);
            RaiseDxfSelectionChanged();
        }

        public void ClearDxfSelection()
        {
            SetSelectedDxfEntity(null);
        }

        private void RaiseDxfSelectionChanged()
        {
            var selected = _selectedDxfEntities.ToList();
            var primary = selected.LastOrDefault();
            _selectedObject = primary;
            DxfEntitySelected?.Invoke(primary);
            DxfEntitiesSelectionChanged?.Invoke(selected);
            SelectionChanged?.Invoke(primary);
        }

        // =========================
        // GENERIC SELECTION
        // =========================
        public event Action<object?>? SelectionChanged;

        public object? GetSelectedObject() => _selectedObject;

        public void SetSelectedObject(object? obj)
        {
            if (_selectedObject == obj) return;

            if (obj is GreenAreaZone zone)
                SetSelectedZone(zone);
            else if (obj is SceneCrane crane)
                SetSelectedCrane(crane);
            else if (obj is PrecastElement element)
                SetSelectedElements(new List<PrecastElement> { element });
            else if (obj is SceneDxf dxf)
                SetSelectedDxfEntity(dxf);
            else if (obj == null)
            {
                _selectedObject = null;
                SelectedZone = null;
                SelectedCrane = null;
                _selectedElements.Clear();
                _selectedDxfEntities.Clear();
                
                ZoneSelected?.Invoke(null);
                CraneSelected?.Invoke(null);
                ElementsSelectionChanged?.Invoke(new List<PrecastElement>());
                DxfEntitySelected?.Invoke(null);
                DxfEntitiesSelectionChanged?.Invoke(new List<SceneDxf>());
                SelectionChanged?.Invoke(null);
            }
            else
            {
                _selectedObject = obj;
                SelectionChanged?.Invoke(obj);
            }
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
