using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using CraneOptimizer.Domain.Schemas;
using CraneOptimizer.Domain.Schemas.Enums;
using CraneOptimizer.UI.Core.Interfaces;
using CraneOptimizer.UI.Core.Models;

namespace CraneOptimizer.UI.Core.Services
{
    /// <summary>
    /// Central coordinator for all viewport selection.
    /// This service is the SINGLE SOURCE OF TRUTH for selection state.
    /// It writes downward into ISceneService — ISceneService never calls back up.
    ///
    /// A boolean guard (_isUpdating) prevents any re-entrant calls that could
    /// arise if a downstream event handler inadvertently triggers Select() again.
    /// </summary>
    public sealed class ViewportSelectionService : IViewportSelectionService
    {
        private readonly ISceneService _scene;

        /// <summary>Re-entrancy guard to prevent selection recursion.</summary>
        private bool _isUpdating;

        public event Action<IReadOnlyList<SelectionTarget>>? SelectionChanged;

        public ViewportSelectionService(ISceneService scene)
        {
            _scene = scene ?? throw new ArgumentNullException(nameof(scene));
        }

        // -----------------------------------------------------------------
        // Read current selection state from ISceneService
        // -----------------------------------------------------------------
        public IReadOnlyList<SelectionTarget> GetSelectedTargets()
        {
            var list = new List<SelectionTarget>();

            foreach (var el in _scene.GetSelectedElements())
            {
                list.Add(new SelectionTarget(
                    SelectionKind.IfcElement,
                    el.ElementID,
                    el.ElementMark ?? el.ElementID,
                    el));
            }

            if (_scene.SelectedZone != null)
            {
                var zone = _scene.SelectedZone;
                var kind = zone.Type == ZoneType.Forbidden
                    ? SelectionKind.RedArea
                    : SelectionKind.GreenArea;
                list.Add(new SelectionTarget(
                    kind,
                    zone.ZoneId.ToString(),
                    zone.Name ?? $"Zone {zone.ZoneId}",
                    zone));
            }

            if (_scene.SelectedCrane != null)
            {
                var crane = _scene.SelectedCrane;
                list.Add(new SelectionTarget(
                    SelectionKind.Crane,
                    crane.Id.ToString(),
                    crane.CraneDefinition?.Name ?? $"Crane {crane.Id}",
                    crane));
            }

            foreach (var dxf in _scene.GetSelectedDxfEntities())
            {
                list.Add(new SelectionTarget(
                    SelectionKind.DxfEntity,
                    dxf.Id.ToString(),
                    dxf.Layer ?? $"DXF {dxf.Id}",
                    dxf));
            }

            return list;
        }

        // -----------------------------------------------------------------
        // Primary entry point — called by click handlers
        // -----------------------------------------------------------------
        public void Select(object? hitObj, bool isCtrlHeld)
        {
            if (_isUpdating)
            {
                Debug.WriteLine("[ViewportSelection] BLOCKED re-entrant Select() call.");
                return;
            }

            _isUpdating = true;
            try
            {
                SelectCore(hitObj, isCtrlHeld);
            }
            finally
            {
                _isUpdating = false;
            }
        }

        public void SelectElements(IEnumerable<PrecastElement> elements, bool isCtrlHeld)
        {
            if (_isUpdating) return;
            _isUpdating = true;
            try
            {
                if (isCtrlHeld)
                {
                    var currentElements = _scene.GetSelectedElements();
                    var currentSet = new HashSet<PrecastElement>(currentElements, ReferenceEqualityComparer.Instance);
                    
                    foreach (var el in elements)
                    {
                        if (currentSet.Contains(el))
                        {
                            currentElements.RemoveAll(x => ReferenceEquals(x, el));
                        }
                        else
                        {
                            currentElements.Add(el);
                        }
                    }
                    _scene.SetSelectedElements(currentElements);
                }
                else
                {
                    _scene.SetSelectedElements(elements.ToList());
                    _scene.SetSelectedZone(null);
                    _scene.SetSelectedCrane(null);
                    _scene.SetSelectedDxfEntity(null);
                }
                NotifyChanged();
            }
            finally
            {
                _isUpdating = false;
            }
        }

        public void SetElements(IEnumerable<PrecastElement> elements)
        {
            if (_isUpdating) return;
            _isUpdating = true;
            try
            {
                _scene.SetSelectedElements(elements.ToList());
                _scene.SetSelectedZone(null);
                _scene.SetSelectedCrane(null);
                _scene.SetSelectedDxfEntity(null);
                NotifyChanged();
            }
            finally
            {
                _isUpdating = false;
            }
        }

        public void ToggleElement(PrecastElement element)
        {
            if (_isUpdating) return;
            _isUpdating = true;
            try
            {
                var currentElements = _scene.GetSelectedElements();
                var existing = currentElements.FirstOrDefault(x => ReferenceEquals(x, element));
                if (existing != null)
                {
                    currentElements.Remove(existing);
                }
                else
                {
                    currentElements.Add(element);
                }
                _scene.SetSelectedElements(currentElements);
                NotifyChanged();
            }
            finally
            {
                _isUpdating = false;
            }
        }

        public void SelectDxf(SceneDxf? dxf, bool isCtrlHeld)
        {
            if (_isUpdating) return;
            _isUpdating = true;
            try
            {
                if (dxf == null)
                {
                    if (!isCtrlHeld)
                        _scene.SetSelectedDxfEntities(new List<SceneDxf>());
                }
                else if (isCtrlHeld)
                {
                    var selected = _scene.GetSelectedDxfEntities();
                    var existing = selected.FirstOrDefault(entity => entity.Id == dxf.Id);
                    if (existing == null)
                        selected.Add(dxf);
                    else
                        selected.Remove(existing);
                    _scene.SetSelectedDxfEntities(selected);
                }
                else
                {
                    _scene.SetSelectedDxfEntities(new List<SceneDxf> { dxf });
                }

                NotifyChanged();
            }
            finally
            {
                _isUpdating = false;
            }
        }

        public void SetDxfs(IEnumerable<SceneDxf> dxfs)
        {
            if (_isUpdating) return;
            _isUpdating = true;
            try
            {
                _scene.SetSelectedDxfEntities(dxfs.ToList());
                NotifyChanged();
            }
            finally
            {
                _isUpdating = false;
            }
        }

        public void Clear()
        {
            ClearSelection();
        }

        public void ClearSelection()
        {
            if (_isUpdating)
            {
                Debug.WriteLine("[ViewportSelection] BLOCKED re-entrant ClearSelection() call.");
                return;
            }

            _isUpdating = true;
            try
            {
                ClearAllSceneSelection();
                NotifyChanged();
            }
            finally
            {
                _isUpdating = false;
            }
        }

        public string GetDiagnostics()
        {
            var targets = GetSelectedTargets();
            var sb = new StringBuilder();
            sb.AppendLine($"[ViewportSelectionService] Selected: {targets.Count}");
            for (int i = 0; i < targets.Count; i++)
            {
                sb.AppendLine($"  [{i + 1}] {targets[i]}");
            }
            return sb.ToString();
        }

        // -----------------------------------------------------------------
        // Core selection logic
        // -----------------------------------------------------------------

        private void SelectCore(object? hitObj, bool isCtrlHeld)
        {
            // --- Empty-space click ---
            if (hitObj == null)
            {
                if (!isCtrlHeld)
                {
                    ClearAllSceneSelection();
                    NotifyChanged();
                }
                // Ctrl + empty-space: do nothing
                return;
            }

            var target = SelectionTarget.FromHitObject(hitObj);
            if (target == null)
            {
                Debug.WriteLine($"[ViewportSelection] Unrecognized hit object type: {hitObj.GetType().Name}");
                return;
            }

            if (isCtrlHeld)
            {
                ToggleTarget(target);
            }
            else
            {
                SetSingleTarget(target);
            }

            NotifyChanged();
        }

        /// <summary>
        /// Normal click: clear everything, then select only the clicked target.
        /// </summary>
        private void SetSingleTarget(SelectionTarget target)
        {
            switch (target.Kind)
            {
                case SelectionKind.IfcElement:
                    var el = (PrecastElement)target.UnderlyingObject;
                    _scene.SetSelectedZone(null);
                    _scene.SetSelectedCrane(null);
                    _scene.SetSelectedDxfEntity(null);
                    _scene.SetSelectedElements(new List<PrecastElement> { el });
                    break;

                case SelectionKind.GreenArea:
                case SelectionKind.RedArea:
                    var zone = (GreenAreaZone)target.UnderlyingObject;
                    _scene.ClearSelectedElements();
                    _scene.SetSelectedCrane(null);
                    _scene.SetSelectedDxfEntity(null);
                    _scene.SetSelectedZone(zone);
                    break;

                case SelectionKind.Crane:
                    var crane = (SceneCrane)target.UnderlyingObject;
                    _scene.ClearSelectedElements();
                    _scene.SetSelectedZone(null);
                    _scene.SetSelectedDxfEntity(null);
                    _scene.SetSelectedCrane(crane);
                    break;

                case SelectionKind.DxfEntity:
                    var dxf = (SceneDxf)target.UnderlyingObject;
                    _scene.ClearSelectedElements();
                    _scene.SetSelectedZone(null);
                    _scene.SetSelectedCrane(null);
                    _scene.SetSelectedDxfEntities(new List<SceneDxf> { dxf });
                    break;
            }
        }

        /// <summary>
        /// Ctrl+Click: toggle the clicked target without clearing others.
        /// IFC elements support true multi-select (list).
        /// Crane and zone are singular; DXF overlays support multi-selection.
        /// </summary>
        private void ToggleTarget(SelectionTarget target)
        {
            switch (target.Kind)
            {
                case SelectionKind.IfcElement:
                    var el = (PrecastElement)target.UnderlyingObject;
                    var currentElements = _scene.GetSelectedElements();
                    var existing = currentElements.FirstOrDefault(x =>
                        string.Equals(x.ElementID, el.ElementID, StringComparison.Ordinal));

                    if (existing != null)
                    {
                        currentElements.Remove(existing);
                    }
                    else
                    {
                        currentElements.Add(el);
                    }
                    _scene.SetSelectedElements(currentElements);
                    break;

                case SelectionKind.GreenArea:
                case SelectionKind.RedArea:
                    var zone = (GreenAreaZone)target.UnderlyingObject;
                    if (_scene.SelectedZone?.ZoneId == zone.ZoneId)
                        _scene.SetSelectedZone(null);
                    else
                        _scene.SetSelectedZone(zone);
                    break;

                case SelectionKind.Crane:
                    var crane = (SceneCrane)target.UnderlyingObject;
                    if (_scene.SelectedCrane?.Id == crane.Id)
                        _scene.SetSelectedCrane(null);
                    else
                        _scene.SetSelectedCrane(crane);
                    break;

                case SelectionKind.DxfEntity:
                    var dxf = (SceneDxf)target.UnderlyingObject;
                    var selectedDxfs = _scene.GetSelectedDxfEntities();
                    var selectedDxf = selectedDxfs.FirstOrDefault(entity => entity.Id == dxf.Id);
                    if (selectedDxf != null)
                        selectedDxfs.Remove(selectedDxf);
                    else
                        selectedDxfs.Add(dxf);
                    _scene.SetSelectedDxfEntities(selectedDxfs);
                    break;
            }
        }

        /// <summary>
        /// Clears all selection state in ISceneService without firing SelectionChanged.
        /// </summary>
        private void ClearAllSceneSelection()
        {
            _scene.ClearSelectedElements();
            _scene.SetSelectedZone(null);
            _scene.SetSelectedCrane(null);
            _scene.SetSelectedDxfEntity(null);
        }

        private void NotifyChanged()
        {
            var targets = GetSelectedTargets();
            Debug.WriteLine(GetDiagnostics());
            SelectionChanged?.Invoke(targets);
        }
    }
}
