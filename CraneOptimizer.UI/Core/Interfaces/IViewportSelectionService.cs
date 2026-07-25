using System;
using System.Collections.Generic;
using CraneOptimizer.Domain.Schemas;
using CraneOptimizer.UI.Core.Models;

namespace CraneOptimizer.UI.Core.Interfaces
{
    /// <summary>
    /// Central coordinator for all viewport selection operations.
    /// All UI click handlers and programmatic selection paths MUST go through this service.
    /// The service is the single source of truth; it updates ISceneService downward — never the reverse.
    /// </summary>
    /// <remarks>
    /// Multi-selection scope (Ctrl+Click):
    ///   • Multiple IFC elements (list)
    ///   • Plus at most one crane (SceneService.SelectedCrane is singular)
    ///   • Plus at most one zone — green or red (SceneService.SelectedZone is singular)
    ///   • Plus multiple complete DXF overlays
    ///
    /// Normal click (no Ctrl): clears ALL selections, then selects only the clicked target.
    /// Empty-space click (no Ctrl): clears ALL selections.
    /// Empty-space click (Ctrl held): no change.
    /// </remarks>
    public interface IViewportSelectionService
    {
        /// <summary>Current selection targets (read from ISceneService state).</summary>
        IReadOnlyList<SelectionTarget> GetSelectedTargets();

        /// <summary>
        /// Execute a selection operation for a viewport click.
        /// </summary>
        /// <param name="hitObj">The raw hit object from the hit-test service, or null for empty space.</param>
        /// <param name="isCtrlHeld">True when the Ctrl modifier key is held down.</param>
                void Select(object? hitObj, bool isCtrlHeld);

        /// <summary>
        /// Selects or toggles a set of elements (e.g., via box selection).
        /// </summary>
        void SelectElements(IEnumerable<PrecastElement> elements, bool isCtrlHeld);

        /// <summary>
        /// Replaces the active elements selection.
        /// </summary>
        void SetElements(IEnumerable<PrecastElement> elements);

        /// <summary>
        /// Toggles a single element's selection state.
        /// </summary>
        void ToggleElement(PrecastElement element);

        /// <summary>Select or toggle one complete DXF overlay without changing other object-type selections.</summary>
        void SelectDxf(SceneDxf? dxf, bool isCtrlHeld);

        /// <summary>Replace only the DXF overlay selection.</summary>
        void SetDxfs(IEnumerable<SceneDxf> dxfs);

        /// <summary>
        /// Clears all selection state.
        /// </summary>
        void Clear();

        /// <summary>Clear all selections across all object types.</summary>
        void ClearSelection();

        /// <summary>Fired after every selection change with the new full target list.</summary>
        event Action<IReadOnlyList<SelectionTarget>>? SelectionChanged;

        /// <summary>Returns a multi-line diagnostic string for debugging.</summary>
        string GetDiagnostics();
    }
}
