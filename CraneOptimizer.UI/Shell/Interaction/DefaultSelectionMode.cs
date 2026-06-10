using System;
using System.Collections.Generic;
using System.Windows.Input;
using HelixToolkit.Wpf;
using CraneOptimizer.Domain.Common;
using CraneOptimizer.UI.Core.Interfaces;
using CraneOptimizer.UI.Shell.ViewModels;
using CraneOptimizer.Domain.Schemas;
using CraneOptimizer.UI.Core.Models;

namespace CraneOptimizer.UI.Shell.Interaction
{
    public class DefaultSelectionMode : IInteractionMode
    {
        private readonly MainViewModel _vm;
        private readonly IViewportHitTestService _hitTest;
        private readonly IViewportSelectionService _viewportSelection;
        private readonly Action _updateCameraClipping;

        public DefaultSelectionMode(
            MainViewModel vm,
            IViewportHitTestService hitTest,
            IViewportSelectionService viewportSelection,
            Action updateCameraClipping)
        {
            _vm = vm ?? throw new ArgumentNullException(nameof(vm));
            _hitTest = hitTest ?? throw new ArgumentNullException(nameof(hitTest));
            _viewportSelection = viewportSelection ?? throw new ArgumentNullException(nameof(viewportSelection));
            _updateCameraClipping = updateCameraClipping ?? throw new ArgumentNullException(nameof(updateCameraClipping));
        }

        public void OnMouseDown(HelixViewport3D viewport, MouseButtonEventArgs e)
        {
            // Standard dragging logic (Crane)
            if (e.ChangedButton == MouseButton.Left)
            {
                var pickPos = e.GetPosition(viewport);
                if (_hitTest.TryGetWorldPoint(viewport, pickPos, out var pickWorld)
                    && _hitTest.TryGetHitCrane(viewport, pickPos, out var hitCrane))
                {
                    // Selecting the crane we are about to drag clears other selections
                    _viewportSelection.Select(hitCrane, false);
                    _vm.StartDrag(new DomainPoint3D(pickWorld.X, pickWorld.Y, pickWorld.Z));
                    e.Handled = true;
                    return;
                }
            }

            // Standard selection logic
            if (e.ChangedButton == MouseButton.Left)
            {
                e.Handled = HandleSelection(viewport, e);
            }
        }

        public void OnMouseMove(HelixViewport3D viewport, MouseEventArgs e)
        {
            if (_vm.IsDraggingCrane && e.LeftButton == MouseButtonState.Pressed)
            {
                var screen = e.GetPosition(viewport);
                if (!_hitTest.TryGetWorldPoint(viewport, screen, out var world))
                    return;

                _vm.DragCrane(new DomainPoint3D(world.X, world.Y, world.Z));
                e.Handled = true;
            }
        }

        public void OnMouseUp(HelixViewport3D viewport, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
            {
                _vm.EndDrag();
            }
        }

        public void OnKeyDown(HelixViewport3D viewport, KeyEventArgs e)
        {
            // Global keys can be handled here if they don't belong to a specific mode
        }

        private bool HandleSelection(HelixViewport3D viewport, MouseButtonEventArgs e)
        {
            var hitObj = _hitTest.GetHitObject(viewport, e.GetPosition(viewport));

            // SelectionHandler owns IFC element click, Ctrl-click, Tab cycle, and box select.
            if (hitObj is PrecastElement)
                return false;

            bool isCtrlHeld = Keyboard.Modifiers.HasFlag(ModifierKeys.Control);

            if (hitObj == null)
            {
                if (!isCtrlHeld)
                    _viewportSelection.Select(null, isCtrlHeld);

                _vm.Scene.SetSelectedZone(null);
                _vm.Scene.SetSelectedCrane(null);
                return false;
            }

            _viewportSelection.Select(hitObj, isCtrlHeld);

            if (hitObj is GreenAreaZone or SceneCrane)
                return true;

            return true;
        }
    }
}
