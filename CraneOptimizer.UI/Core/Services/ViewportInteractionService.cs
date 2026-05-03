using System.Windows.Input;
using HelixToolkit.Wpf;
using CraneOptimizer.UI.Core.Interfaces;

namespace CraneOptimizer.UI.Core.Services
{
    public class ViewportInteractionService : IViewportInteractionService
    {
        public IInteractionMode CurrentMode { get; set; } = null!;

        public void HandleMouseDown(HelixViewport3D viewport, MouseButtonEventArgs e)
        {
            CurrentMode?.OnMouseDown(viewport, e);
        }

        public void HandleMouseMove(HelixViewport3D viewport, MouseEventArgs e)
        {
            CurrentMode?.OnMouseMove(viewport, e);
        }

        public void HandleMouseUp(HelixViewport3D viewport, MouseButtonEventArgs e)
        {
            CurrentMode?.OnMouseUp(viewport, e);
        }

        public void HandleKeyDown(HelixViewport3D viewport, KeyEventArgs e)
        {
            CurrentMode?.OnKeyDown(viewport, e);
        }
    }
}
