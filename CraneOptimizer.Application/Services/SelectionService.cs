using CraneOptimizer.Domain.Schemas;
using System;

namespace CraneOptimizer.Application.Services
{
    public sealed class SelectionService : ISelectionService
    {
        private PrecastElement? _current;
        public PrecastElement? SelectedElement => _current;
        public event Action<PrecastElement?> SelectionChanged = delegate { };

        public void Select(PrecastElement? element)
        {
            if (_current == element) return;
            _current = element;
            SelectionChanged(_current);
        }
    }
}
