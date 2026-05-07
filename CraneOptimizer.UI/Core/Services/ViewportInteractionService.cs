using System.Collections.Generic;
using System.Linq;
using System.Windows.Input;
using HelixToolkit.Wpf;
using CraneOptimizer.UI.Core.Interfaces;

namespace CraneOptimizer.UI.Core.Services
{
    public class ViewportInteractionService : IViewportInteractionService
    {
        private readonly Stack<IInteractionMode> _modeStack = new Stack<IInteractionMode>();
        private IInteractionMode? _defaultMode;

        public IInteractionMode CurrentMode => _modeStack.Count > 0 ? _modeStack.Peek() : _defaultMode!;

        public void PushMode(IInteractionMode mode)
        {
            _modeStack.Push(mode);
        }

        public void PopMode()
        {
            if (_modeStack.Count > 0)
                _modeStack.Pop();
        }

        public void ResetToDefaultMode(IInteractionMode defaultMode)
        {
            _defaultMode = defaultMode;
            _modeStack.Clear();
        }

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
