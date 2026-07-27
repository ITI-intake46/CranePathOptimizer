using System;
using System.Collections.Generic;
using System.Windows.Input;
using HelixToolkit.Wpf;
using CraneOptimizer.Domain.Common;
using CraneOptimizer.UI.Core.Interfaces;
using CraneOptimizer.UI.Shell.ViewModels;
using CraneOptimizer.Domain.Schemas;
using CraneOptimizer.UI.Core.Models;
using CraneOptimizer.UI.Features.F7_Zones.ViewModels;

namespace CraneOptimizer.UI.Shell.Interaction
{
    public class InteractionRouter : IInteractionMode
    {
        private readonly MainViewModel _vm;
        private readonly IViewportHitTestService _hitTest;
        private readonly Action _updateCameraClipping;

        public InteractionRouter(MainViewModel vm, IViewportHitTestService hitTest, Action updateCameraClipping)
        {
            _vm = vm;
            _hitTest = hitTest;
            _updateCameraClipping = updateCameraClipping;
        }

        public void OnMouseDown(HelixViewport3D viewport, MouseButtonEventArgs e)
        {
            if (_vm.IfcViewer.IsPlacementMode)
            {
                if (e.ChangedButton != MouseButton.Left) return;

                var placementScreen = e.GetPosition(viewport);
                if (!_hitTest.TryGetPlacementPoint(viewport, placementScreen, out var placementPoint))
                    return;

                if (_vm.IfcViewer.PlacePendingIfc(placementPoint))
                {
                    e.Handled = true;
                }
                return;
            }

            if (_vm.CranePlacement.IsPlacing)
            {
                if (e.ChangedButton != MouseButton.Left) return;

                var cranePlacementScreen = e.GetPosition(viewport);
                if (!_hitTest.TryGetPlacementPoint(viewport, cranePlacementScreen, out var cranePoint)
                    && !_hitTest.TryGetWorldPoint(viewport, cranePlacementScreen, out cranePoint))
                    return;

                var point = new DomainPoint3D(cranePoint.X, cranePoint.Y, cranePoint.Z);
                _vm.CranePlacement.Place(point);

                // Only finalize placement when a crane is actually selected.
                if (_vm.CranePlacement.SelectedCrane != null || _vm.SelectedCrane != null)
                    _vm.PlaceSelectedCrane(point);

                e.Handled = true;
                return;
            }

            if (_vm.IsPickingOrigin)
            {
                if (e.ChangedButton != MouseButton.Left) return;

                var originScreen = e.GetPosition(viewport);
                if (!_hitTest.TryGetWorldPoint(viewport, originScreen, out var origin))
                    return;

                _vm.SetOrigin(new DomainPoint3D(origin.X, origin.Y, origin.Z));
                // Camera zoom extents has been deliberately removed here.
                _updateCameraClipping();
                e.Handled = true;
                return;
            }

            var drawingVm = _vm.Drawing;
            if (drawingVm != null && (drawingVm.IsDrawing || drawingVm.IsMeasuring))
            {
                // IfcViewerControl owns asynchronous zone click resolution so
                // the canonical IOsnapService result is committed exactly once.
                if (drawingVm.IsDrawing)
                    return;

                HandleDrawingInteraction(viewport, e, drawingVm);
                return;
            }

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

            if (e.ChangedButton == MouseButton.Left)
            {
                e.Handled = HandleSelection(viewport, e);
            }
        }

        private void HandleDrawingInteraction(HelixViewport3D viewport, MouseButtonEventArgs e, DrawingViewModel drawingVm)
        {
            if (e.ChangedButton == MouseButton.Right)
            {
                drawingVm.HandleRightClick();
                e.Handled = true;
                return;
            }

            if (e.ChangedButton != MouseButton.Left) return;

            var screen = e.GetPosition(viewport);
            if (!_hitTest.TryGetWorldPoint(viewport, screen, out var world)) return;

            if (e.ClickCount > 1)
            {
                drawingVm.HandleDoubleClick();
                e.Handled = true;
                return;
            }

            drawingVm.HandleViewportLeftClick(new DomainPoint3D(world.X, world.Y, world.Z));
            e.Handled = true;
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
                return;
            }

            // IfcViewerControl owns asynchronous zone hover resolution.
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
            if (_vm.IfcViewer.IsPlacementMode)
                return;

            _vm.Drawing.HandleKeyDown(e);
        }

        private bool HandleSelection(HelixViewport3D viewport, MouseButtonEventArgs e)
        {
            var hitObj = _hitTest.GetHitObject(viewport, e.GetPosition(viewport));

            if (hitObj == null)
            {
                _vm.Scene.SetSelectedZone(null);
                _vm.Scene.SetSelectedCrane(null);
                _vm.Scene.SetSelectedElements(new List<PrecastElement>());
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

            if (hitObj is PrecastElement element)
            {
                _vm.Scene.SetSelectedElements(new List<PrecastElement> { element });
                return true;
            }

            _vm.Scene.SetSelectedZone(null);
            return false;
        }
    }
}
