using System.Windows.Input;
using HelixToolkit.Wpf;

namespace CraneOptimizer.UI.Core.Interfaces
{
    public interface IViewportInteractionService
    {
        IInteractionMode CurrentMode { get; set; }
        
        void HandleMouseDown(HelixViewport3D viewport, MouseButtonEventArgs e);
        void HandleMouseMove(HelixViewport3D viewport, MouseEventArgs e);
        void HandleMouseUp(HelixViewport3D viewport, MouseButtonEventArgs e);
        void HandleKeyDown(HelixViewport3D viewport, KeyEventArgs e);
    }
}
