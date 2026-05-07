using System.Windows.Input;
using System.Windows.Media.Media3D;
using HelixToolkit.Wpf;
using CraneOptimizer.UI.Core.Interfaces;
using CraneOptimizer.UI.Features.F2_IfcViewer.ViewModels;

namespace CraneOptimizer.UI.Features.F2_IfcViewer.Interaction
{
    public class IfcPlacementMode : IInteractionMode
    {
        private readonly IfcViewerViewModel _vm;
        private readonly IViewportHitTestService _hitTest;
        private readonly IViewportInteractionService _interactionService;
        private readonly Action _updateCameraClipping;

        public IfcPlacementMode(
            IfcViewerViewModel vm,
            IViewportHitTestService hitTest,
            IViewportInteractionService interactionService,
            Action updateCameraClipping)
        {
            _vm = vm;
            _hitTest = hitTest;
            _interactionService = interactionService;
            _updateCameraClipping = updateCameraClipping;
        }

        public void OnMouseDown(HelixViewport3D viewport, MouseButtonEventArgs e)
        {
            if (e.ChangedButton != MouseButton.Left) return;

            var placementScreen = e.GetPosition(viewport);
            if (!_hitTest.TryGetPlacementPoint(viewport, placementScreen, out var placementPoint))
                return;

            if (_vm.PlacePendingIfc(placementPoint))
            {
                viewport.UpdateLayout();
                _updateCameraClipping();
                _interactionService.PopMode();
                e.Handled = true;
            }
        }

        public void OnMouseMove(HelixViewport3D viewport, MouseEventArgs e)
        {
        }

        public void OnMouseUp(HelixViewport3D viewport, MouseButtonEventArgs e)
        {
        }

        public void OnKeyDown(HelixViewport3D viewport, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                _vm.CancelPlacementCommand.Execute(null);
                _interactionService.PopMode();
                e.Handled = true;
            }
        }
    }
}
