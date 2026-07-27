using CraneOptimizer.Domain.Entities;
using CraneOptimizer.Domain.Schemas;
using CraneOptimizer.UI.Core.Models;

namespace CraneOptimizer.UI.Core.Interfaces
{
    public interface ISceneService
    {
        // =========================
        // ELEMENTS
        // =========================
        List<PrecastElement> GetElements();
        IReadOnlyList<PrecastElement> GetElementSnapshot() => GetElements();
        void SetElements(List<PrecastElement> elements);
        void ClearElements();

        List<PrecastElement> GetSelectedElements();
        void SetSelectedElements(List<PrecastElement> elements);
        void ClearSelectedElements();

        event Action<List<PrecastElement>>? ElementsChanged;
        event Action<List<PrecastElement>>? ElementsSelectionChanged;

        // =========================
        // ZONES
        // =========================
        List<GreenAreaZone> GetZones();
        IReadOnlyList<GreenAreaZone> GetZoneSnapshot() => GetZones();

        void AddZone(GreenAreaZone zone);
        void RemoveZone(GreenAreaZone zone);
        void ApplyZoneChanges(IReadOnlyList<Guid> removeZoneIds, IReadOnlyList<GreenAreaZone> addZones)
        {
            var removeIds = removeZoneIds.ToHashSet();
            foreach (var zone in GetZones().Where(zone => removeIds.Contains(zone.ZoneId)).ToList())
                RemoveZone(zone);
            foreach (var zone in addZones)
            {
                if (GetZones().All(existing => existing.ZoneId != zone.ZoneId))
                    AddZone(zone);
            }
        }
        void RemoveSelectedZone();
        void ClearZones();
        void UpdateZone(GreenAreaZone zone);

        GreenAreaZone? SelectedZone { get; }
        void SetSelectedZone(GreenAreaZone? zone);
        void ClearZoneSelection();

        event Action<GreenAreaZone>? ZoneAdded;
        event Action<GreenAreaZone>? ZoneRemoved;
        event Action<GreenAreaZone>? ZoneUpdated;
        event Action? ZonesCleared;
        event Action? ZonesChanged
        {
            add { }
            remove { }
        }
        event Action<GreenAreaZone?>? ZoneSelected;

        // =========================
        // CRANES (SceneCrane)
        // =========================
        List<SceneCrane> GetCranes();

        void AddCrane(SceneCrane crane);
        void RemoveCrane(SceneCrane crane);
        void ClearCranes();

        void MoveCrane(SceneCrane crane, double x, double y, double z);
        void RotateCrane(SceneCrane crane, double angle);
        void UpdateCrane(SceneCrane crane);

        SceneCrane? SelectedCrane { get; }
        void SetSelectedCrane(SceneCrane? crane);
        void ClearCraneSelection();

        event Action<SceneCrane>? CraneAdded;
        event Action<SceneCrane>? CraneRemoved;
        event Action? CranesCleared;
        event Action<SceneCrane>? CraneMoved;
        event Action<SceneCrane>? CraneRotated;
        event Action<SceneCrane>? CraneUpdated;
        event Action<SceneCrane?>? CraneSelected;

        // =========================
        // DXF (Drawings / Site Geometry)
        // =========================
        List<SceneDxf> GetDxfEntities();

        void AddDxfEntity(SceneDxf entity);
        void RemoveDxfEntity(SceneDxf entity);
        void RemoveSelectedDxfEntity();
        void ClearDxfEntities();
        void UpdateDxfEntity(SceneDxf entity);

        SceneDxf? SelectedDxfEntity { get; }
        List<SceneDxf> GetSelectedDxfEntities() =>
            SelectedDxfEntity == null ? new List<SceneDxf>() : new List<SceneDxf> { SelectedDxfEntity };
        void SetSelectedDxfEntity(SceneDxf? entity);
        void SetSelectedDxfEntities(List<SceneDxf> entities) =>
            SetSelectedDxfEntity(entities.LastOrDefault());
        void ClearDxfSelection();

        event Action<SceneDxf>? DxfEntityAdded;
        event Action<SceneDxf>? DxfEntityRemoved;
        event Action<SceneDxf>? DxfEntityUpdated;
        event Action? DxfEntitiesCleared;
        event Action<SceneDxf?>? DxfEntitySelected;
        event Action<List<SceneDxf>>? DxfEntitiesSelectionChanged
        {
            add { }
            remove { }
        }

        // =========================
        // GENERIC SELECTION
        // =========================
        object? GetSelectedObject();
        void SetSelectedObject(object? obj);

        event Action<object?>? SelectionChanged;
    }
}
