using System;
using System.Collections.Generic;
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
    ///   • Plus at most one DXF entity (SceneService.SelectedDxfEntity is singular)
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

        /// <summary>Clear all selections across all object types.</summary>
        void ClearSelection();

        /// <summary>Fired after every selection change with the new full target list.</summary>
        event Action<IReadOnlyList<SelectionTarget>>? SelectionChanged;

        /// <summary>Returns a multi-line diagnostic string for debugging.</summary>
        string GetDiagnostics();
    }
}
