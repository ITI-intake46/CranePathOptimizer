using CraneOptimizer.UI.Core.Interfaces;
using CraneOptimizer.UI.Core.Models;
using HelixToolkit.Wpf;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Media3D;

namespace CraneOptimizer.UI.Features.F11_DXF.Interaction
{
    public sealed class DxfSelectionMode : IInteractionMode
    {
        private readonly IViewportSelectionService _selection;
        private readonly Action _exit;

        public DxfSelectionMode(IViewportSelectionService selection, Action exit)
        {
            _selection = selection;
            _exit = exit;
        }

        public void OnMouseDown(HelixViewport3D viewport, MouseButtonEventArgs e)
        {
            if (e.ChangedButton != MouseButton.Left)
                return;

            var source = FindDxfSource(viewport, e.GetPosition(viewport.Viewport));
            var isCtrlHeld = Keyboard.Modifiers.HasFlag(ModifierKeys.Control);
            _selection.SelectDxf(source, isCtrlHeld);
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
            if (e.Key != Key.Escape)
                return;

            _exit();
            e.Handled = true;
        }

        private static SceneDxf? FindDxfSource(HelixViewport3D viewport, Point point)
        {
            try
            {
                foreach (var hit in viewport.Viewport.FindHits(point))
                {
                    if (hit.Visual is not ModelVisual3D visual)
                        continue;

                    if (visual.GetValue(FrameworkElement.TagProperty) is SceneDxf source)
                        return source;
                }
            }
            catch
            {
                // A transient camera/layout change can invalidate a hit-test frame.
            }

            return null;
        }
    }
}
