using System.Windows.Input;
using HelixToolkit.Wpf;

namespace CraneOptimizer.UI.Core.Interfaces
{
    public interface IViewportInteractionService
    {
        IInteractionMode CurrentMode { get; }
        
        void PushMode(IInteractionMode mode);
        void PopMode();
        void ResetToDefaultMode(IInteractionMode defaultMode);

        void HandleMouseDown(HelixViewport3D viewport, MouseButtonEventArgs e);
        void HandleMouseMove(HelixViewport3D viewport, MouseEventArgs e);
        void HandleMouseUp(HelixViewport3D viewport, MouseButtonEventArgs e);
        void HandleKeyDown(HelixViewport3D viewport, KeyEventArgs e);
    }
}
