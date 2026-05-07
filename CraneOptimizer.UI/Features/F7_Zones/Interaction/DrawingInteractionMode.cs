using System.Windows.Input;
using HelixToolkit.Wpf;
using CraneOptimizer.UI.Core.Interfaces;
using CraneOptimizer.UI.Features.F7_Zones.ViewModels;
using CraneOptimizer.Domain.Common;

namespace CraneOptimizer.UI.Features.F7_Zones.Interaction
{
    public class DrawingInteractionMode : IInteractionMode
    {
        private readonly DrawingViewModel _vm;
        private readonly IViewportHitTestService _hitTest;
        private readonly IViewportInteractionService _interactionService;

        public DrawingInteractionMode(
            DrawingViewModel vm, 
            IViewportHitTestService hitTest,
            IViewportInteractionService interactionService)
        {
            _vm = vm;
            _hitTest = hitTest;
            _interactionService = interactionService;
        }

        public void OnMouseDown(HelixViewport3D viewport, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Right)
            {
                _vm.HandleRightClick();
                e.Handled = true;
                return;
            }

            if (e.ChangedButton != MouseButton.Left) return;

            var screen = e.GetPosition(viewport);
            if (!_hitTest.TryGetWorldPoint(viewport, screen, out var world)) return;

            if (e.ClickCount > 1)
            {
                _vm.HandleDoubleClick();
                e.Handled = true;
                return;
            }

            _vm.HandleViewportLeftClick(new DomainPoint3D(world.X, world.Y, world.Z));
            e.Handled = true;
        }

        public void OnMouseMove(HelixViewport3D viewport, MouseEventArgs e)
        {
            var screen = e.GetPosition(viewport);
            if (!_hitTest.TryGetWorldPoint(viewport, screen, out var world)) return;

            _vm.SetHoverPoint(new DomainPoint3D(world.X, world.Y, world.Z));
        }

        public void OnMouseUp(HelixViewport3D viewport, MouseButtonEventArgs e)
        {
        }

        public void OnKeyDown(HelixViewport3D viewport, KeyEventArgs e)
        {
            _vm.HandleKeyDown(e);
            
            // If the tool is no longer active, pop the mode
            if (!_vm.IsDrawing && !_vm.IsMeasuring)
            {
                _interactionService.PopMode();
            }
        }
    }
}
