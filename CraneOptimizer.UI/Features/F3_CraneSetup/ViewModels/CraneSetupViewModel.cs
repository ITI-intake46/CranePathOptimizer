using CraneOptimizer.Domain.Common;
using CraneOptimizer.Domain.Entities;
using CraneOptimizer.Domain.Schemas;
using CraneOptimizer.UI.Core.Base;
using CraneOptimizer.UI.Core.Commands;
using CraneOptimizer.UI.Core.Interfaces;   // ← this one was missing
using System.Windows.Input;
using System.Windows.Media.Media3D;

namespace CraneOptimizer.UI.Features.F3_CraneSetup.ViewModels
{
    public class CraneSetupViewModel : ViewModelBase
    {
        // ── Dependencies (injected via constructor) ───────────────────────
        private readonly IRenderingService _render;
        private readonly ISceneService _scene;

        // ── Dragging state ────────────────────────────────────────────────
        // This stores the visual that is currently in the viewport.
        // We keep a reference so we can move it (change its Transform) on drag.
        private ModelVisual3D? _craneVisual;

        // Whether the user is currently dragging the crane
        private bool _isDragging;

        // The last known valid position (so we can revert if zone check fails)
        private DomainPoint3D _lastValidPosition = new(0, 0, 0);

        // ── Bound Properties ──────────────────────────────────────────────
        private Crane? _selectedCrane;
        public Crane? SelectedCrane
        {
            get => _selectedCrane;
            set
            {
                _selectedCrane = value;
                OnPropertyChanged();
                // When crane changes, re-render it
                OnCraneChanged(value);
            }
        }

        private DomainPoint3D? _currentPosition;
        public DomainPoint3D? CurrentPosition
        {
            get => _currentPosition;
            set { _currentPosition = value; OnPropertyChanged(); }
        }

        private bool _isDraggingActive;
        public bool IsDraggingActive
        {
            get => _isDraggingActive;
            set { _isDraggingActive = value; OnPropertyChanged(); }
        }

        // ── Constructor ───────────────────────────────────────────────────
        public CraneSetupViewModel(IRenderingService render, ISceneService scene)
        {
            _render = render;
            _scene = scene;

            // Listen to scene changes: when a new crane is set via ISceneService,
            // update our local SelectedCrane automatically.

        }
        // In CraneSetupViewModel.UpdateDrag(), add colour feedback:

        public void UpdateDrag(DomainPoint3D worldPoint, List<GreenAreaZone> zones)
        {
            if (!_isDragging) return;

            bool hasZones = zones != null && zones.Count > 0;
            bool isInside = !hasZones || GreenZoneConstraint.IsInsideAnyZone(worldPoint, zones);

            // Visual feedback: update status for the View to display
            IsPositionValid = isInside;

            var constrained = GreenZoneConstraint.Clamp(worldPoint, zones!);
            MoveCraneVisualTo(constrained);
            CurrentPosition = constrained;
        }

        // Add this property to CraneSetupViewModel:
        private bool _isPositionValid = true;
        public bool IsPositionValid
        {
            get => _isPositionValid;
            set { _isPositionValid = value; OnPropertyChanged(); }
        }

        // ── Called when the selected crane changes ────────────────────────
        private void OnCraneChanged(Crane? crane)
        {
            // Remove any existing crane visual from the viewport
            _render.ClearLayer("Crane");
            _render.ClearLayer("CraneRadius");
            _craneVisual = null;

            if (crane == null) return;

            // Build and render the crane model
            // We pass [crane] as a single-item list to Render()
            _render.Render(new[] { crane }, new CraneVisualBuilder(visual => RegisterCraneVisual(visual)),  // pass the callback
            "Crane");

            // Also render the max-radius circle
            var spec = crane.Specifications.FirstOrDefault();
            if (spec != null)
            {
                _render.Render(
                    new[] { crane },
                    new CraneRadiusBuilder(),
                    "CraneRadius"
                );
            }

            // Store the default position
            _lastValidPosition = new DomainPoint3D(0, 0, 0);
            CurrentPosition = _lastValidPosition;
        }

        // ─────────────────────────────────────────────────────────────────
        // DRAG API — called by the View's mouse event handlers
        // ─────────────────────────────────────────────────────────────────

        /// <summary>
        /// Call this from MouseDown when the user clicks on the crane.
        /// </summary>
        public void BeginDrag()
        {
            if (_selectedCrane == null) return;
            _isDragging = true;
            IsDraggingActive = true;
        }

        /// <summary>
        /// Call this from MouseMove. worldPoint is the 3D position on
        /// the ground plane where the mouse is currently pointing.
        /// </summary>


        /// <summary>
        /// Call this from MouseUp. Finalises the position.
        /// </summary>
        public void EndDrag(List<GreenAreaZone> zones)
        {
            if (!_isDragging) return;

            _isDragging = false;
            IsDraggingActive = false;

            if (CurrentPosition != null)
            {
                // Final validation
                var final = GreenZoneConstraint.Clamp(CurrentPosition, zones);
                MoveCraneVisualTo(final);
                _lastValidPosition = final;
                CurrentPosition = final;
            }
        }

        // ─────────────────────────────────────────────────────────────────
        // PRIVATE HELPERS
        // ─────────────────────────────────────────────────────────────────

        /// <summary>
        /// Moves the crane's 3D visual to a new X,Y position on the ground plane.
        /// This does NOT rebuild the mesh — it only changes the Transform.
        /// This is what makes dragging feel smooth and fast.
        /// </summary>
        private void MoveCraneVisualTo(DomainPoint3D pos)
        {
            if (_craneVisual == null) return;

            // TranslateTransform3D offsets the entire visual in 3D space
            _craneVisual.Transform = new TranslateTransform3D(pos.X, pos.Y, pos.Z);
        }

        /// <summary>
        /// Called by CraneVisualBuilder after it creates the visual,
        /// so we can store a reference for drag operations.
        /// </summary>
        public void RegisterCraneVisual(ModelVisual3D visual)
        {
            _craneVisual = visual;
        }

    }
}