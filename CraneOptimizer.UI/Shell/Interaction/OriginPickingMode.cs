using System.Windows.Input;
using HelixToolkit.Wpf;
using CraneOptimizer.UI.Core.Interfaces;
using CraneOptimizer.UI.Shell.ViewModels;
using CraneOptimizer.Domain.Common;

namespace CraneOptimizer.UI.Shell.Interaction
{
    public class OriginPickingMode : IInteractionMode
    {
        private readonly MainViewModel _vm;
        private readonly IViewportHitTestService _hitTest;
        private readonly IViewportInteractionService _interactionService;
        private readonly Action _updateCameraClipping;

        public OriginPickingMode(
            MainViewModel vm,
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

            var originScreen = e.GetPosition(viewport);
            if (!_hitTest.TryGetWorldPoint(viewport, originScreen, out var origin))
                return;

            _vm.SetOrigin(new DomainPoint3D(origin.X, origin.Y, origin.Z));
            _updateCameraClipping();
            _interactionService.PopMode();
            e.Handled = true;
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
                _vm.IsPickingOrigin = false;
                _interactionService.PopMode();
                e.Handled = true;
            }
        }
    }
}
