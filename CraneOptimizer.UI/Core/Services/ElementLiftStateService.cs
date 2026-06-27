using CraneOptimizer.UI.Core.Interfaces;
using CraneOptimizer.UI.Core.Models;

namespace CraneOptimizer.UI.Core.Services
{
    public sealed class ElementLiftStateService : IElementLiftStateService
    {
        private readonly Dictionary<string, ElementLiftStateDto> _states = new(StringComparer.Ordinal);

        public event Action? StateChanged;

        public bool HasCurrentState { get; private set; }

        public ElementLiftStateDto? GetState(string? elementId)
        {
            if (string.IsNullOrWhiteSpace(elementId))
                return null;

            return _states.TryGetValue(elementId, out var state) ? state : null;
        }

        public void SetStates(IEnumerable<ElementLiftStateDto> states)
        {
            _states.Clear();

            foreach (var state in states)
            {
                if (!string.IsNullOrWhiteSpace(state.ElementId))
                    _states[state.ElementId] = state;
            }

            HasCurrentState = true;
            StateChanged?.Invoke();
        }

        public void Clear()
        {
            if (!HasCurrentState && _states.Count == 0)
                return;

            _states.Clear();
            HasCurrentState = false;
            StateChanged?.Invoke();
        }
    }
}
