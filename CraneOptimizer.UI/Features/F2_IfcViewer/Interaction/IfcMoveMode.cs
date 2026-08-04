using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Media3D;
using HelixToolkit.Wpf;
using CraneOptimizer.UI.Core.Interfaces;
using CraneOptimizer.UI.Features.F15_ObjectSnap.Models;
using CraneOptimizer.UI.Features.F2_IfcViewer.ViewModels;

namespace CraneOptimizer.UI.Features.F2_IfcViewer.Interaction
{
    public sealed class IfcMoveMode : IInteractionMode
    {
        private readonly IfcViewerViewModel _vm;
        private readonly IViewportHitTestService _hitTest;
        private readonly IViewportInteractionService _interactionService;

        public IfcMoveMode(
            IfcViewerViewModel vm,
            IViewportHitTestService hitTest,
            IViewportInteractionService interactionService)
        {
            _vm = vm;
            _hitTest = hitTest;
            _interactionService = interactionService;
        }

        public bool TryCaptureRawClick(
            HelixViewport3D viewport,
            Point screenPoint,
            out Point3D worldPoint)
        {
            return _vm.IfcMoveStep == IfcMoveStep.SelectBasePoint
                ? _hitTest.TryGetIfcHitPoint(viewport, screenPoint, out worldPoint)
                : _hitTest.TryGetPlacementPoint(viewport, screenPoint, out worldPoint);
        }

        public void CommitResolvedClick(Point3D? rawPoint, OsnapResult? snapResult)
        {
            switch (_vm.ActiveIfcMoveMethod)
            {
                case IfcMoveMethod.Origin:
                {
                    Point3D? destination = snapResult?.WorldPoint ?? rawPoint;
                    if (destination.HasValue && _vm.MovePlacedIfc(destination.Value))
                        _interactionService.PopMode();
                    break;
                }

                case IfcMoveMethod.PickedPoint
                    when _vm.IfcMoveStep == IfcMoveStep.SelectBasePoint:
                {
                    Point3D? basePoint = snapResult?.Source == OsnapSource.Ifc
                        ? snapResult.WorldPoint
                        : rawPoint;
                    if (basePoint.HasValue)
                        _vm.SetIfcMoveBasePoint(basePoint.Value);
                    break;
                }

                case IfcMoveMethod.PickedPoint
                    when _vm.IfcMoveStep == IfcMoveStep.SelectDestination:
                {
                    Point3D? destination = snapResult?.WorldPoint ?? rawPoint;
                    if (destination.HasValue &&
                        _vm.SelectedIfcBasePoint.HasValue &&
                        _vm.MovePlacedIfcFromPoint(
                            _vm.SelectedIfcBasePoint.Value,
                            destination.Value))
                    {
                        _interactionService.PopMode();
                    }
                    break;
                }
            }
        }

        public OsnapResult? FilterSnapResult(OsnapResult? snapResult)
        {
            if (_vm.ActiveIfcMoveMethod == IfcMoveMethod.PickedPoint &&
                _vm.IfcMoveStep == IfcMoveStep.SelectBasePoint &&
                snapResult?.Source != OsnapSource.Ifc)
            {
                return null;
            }

            return snapResult;
        }

        public void UpdateSnapPreview(OsnapResult? snapResult)
        {
            if (_vm.ActiveIfcMoveMethod == IfcMoveMethod.PickedPoint &&
                _vm.IfcMoveStep == IfcMoveStep.SelectDestination &&
                snapResult != null)
            {
                _vm.SetIfcMovePreviewPoint(snapResult.WorldPoint);
            }
        }

        public void OnMouseDown(HelixViewport3D viewport, MouseButtonEventArgs e)
        {
            if (e.ChangedButton != MouseButton.Right)
                return;

            Cancel();
            e.Handled = true;
        }

        public void OnMouseMove(HelixViewport3D viewport, MouseEventArgs e)
        {
            if (_vm.ActiveIfcMoveMethod != IfcMoveMethod.PickedPoint ||
                _vm.IfcMoveStep != IfcMoveStep.SelectDestination)
            {
                return;
            }

            Point screenPoint = e.GetPosition(viewport);
            if (_hitTest.TryGetPlacementPoint(viewport, screenPoint, out Point3D worldPoint))
                _vm.SetIfcMovePreviewPoint(worldPoint);
        }

        public void OnMouseUp(HelixViewport3D viewport, MouseButtonEventArgs e)
        {
        }

        public void OnKeyDown(HelixViewport3D viewport, KeyEventArgs e)
        {
            if (e.Key != Key.Escape)
                return;

            Cancel();
            e.Handled = true;
        }

        private void Cancel()
        {
            _vm.CancelIfcMovement();
        }
    }
}
