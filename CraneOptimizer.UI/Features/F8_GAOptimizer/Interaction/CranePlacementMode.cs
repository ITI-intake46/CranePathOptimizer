using System.Windows.Input;
using HelixToolkit.Wpf;
using CraneOptimizer.UI.Core.Interfaces;
using CraneOptimizer.UI.Features.F8_GAOptimizer.ViewModels;
using CraneOptimizer.Domain.Common;

namespace CraneOptimizer.UI.Features.F8_GAOptimizer.Interaction
{
    public class CranePlacementMode : IInteractionMode
    {
        private readonly CranePlacementViewModel _vm;
        private readonly IViewportHitTestService _hitTest;
        private readonly IViewportInteractionService _interactionService;
        private readonly Action<DomainPoint3D> _placeCraneInScene;

        public CranePlacementMode(
            CranePlacementViewModel vm,
            IViewportHitTestService hitTest,
            IViewportInteractionService interactionService,
            Action<DomainPoint3D> placeCraneInScene)
        {
            _vm = vm;
            _hitTest = hitTest;
            _interactionService = interactionService;
            _placeCraneInScene = placeCraneInScene;
        }

        public void OnMouseDown(HelixViewport3D viewport, MouseButtonEventArgs e)
        {
            if (e.ChangedButton != MouseButton.Left) return;

            var screen = e.GetPosition(viewport);
            if (!_hitTest.TryGetWorldPoint(viewport, screen, out var worldPoint))
                return;

            var point = new DomainPoint3D(worldPoint.X, worldPoint.Y, worldPoint.Z);
            _vm.Place(point);
            _placeCraneInScene(point);
            
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
                _vm.IsPlacing = false;
                _interactionService.PopMode();
                e.Handled = true;
            }
        }
    }
}
