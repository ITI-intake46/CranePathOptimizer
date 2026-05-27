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
        private readonly Action _updateCameraClipping;

        public DefaultSelectionMode(MainViewModel vm, IViewportHitTestService hitTest, Action updateCameraClipping)
        {
            _vm = vm;
            _hitTest = hitTest;
            _updateCameraClipping = updateCameraClipping;
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
                    _vm.SelectedSceneCrane = hitCrane;
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

            // F17 owns element selection so Ctrl-click, Tab cycling, and box
            // selection all pass through one batched selection service.
            if (hitObj is PrecastElement)
                return false;

            _vm.Scene.SetSelectedObject(hitObj);

            if (hitObj == null)
            {
                _vm.Scene.SetSelectedZone(null);
                _vm.Scene.SetSelectedCrane(null);
                return false;
            }

            if (hitObj is GreenAreaZone zone)
            {
                _vm.Scene.SetSelectedZone(zone);
                return true;
            }

            if (hitObj is SceneCrane crane)
            {
                _vm.Scene.SetSelectedCrane(crane);
                return true;
            }

            return true;
        }
    }
}
