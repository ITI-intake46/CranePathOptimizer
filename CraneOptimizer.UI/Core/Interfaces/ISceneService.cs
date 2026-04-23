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

        void AddZone(GreenAreaZone zone);
        void RemoveZone(GreenAreaZone zone);
        void RemoveSelectedZone();
        void ClearZones();

        GreenAreaZone? SelectedZone { get; }
        void SetSelectedZone(GreenAreaZone? zone);
        void ClearZoneSelection();

        event Action<GreenAreaZone>? ZoneAdded;
        event Action<GreenAreaZone>? ZoneRemoved;
        event Action<GreenAreaZone>? ZoneUpdated;
        event Action? ZonesCleared;
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
        // GENERIC SELECTION
        // =========================
        object? GetSelectedObject();
        void SetSelectedObject(object? obj);

        event Action<object?>? SelectionChanged;
    }
}