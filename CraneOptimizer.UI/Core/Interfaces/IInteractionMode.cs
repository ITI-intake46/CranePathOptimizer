using System.Windows.Input;
using HelixToolkit.Wpf;

namespace CraneOptimizer.UI.Core.Interfaces
{
    public interface IInteractionMode
    {
        void OnMouseDown(HelixViewport3D viewport, MouseButtonEventArgs e);
        void OnMouseMove(HelixViewport3D viewport, MouseEventArgs e);
        void OnMouseUp(HelixViewport3D viewport, MouseButtonEventArgs e);
        void OnKeyDown(HelixViewport3D viewport, KeyEventArgs e);
    }
}
