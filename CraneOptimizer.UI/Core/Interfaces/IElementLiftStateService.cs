using CraneOptimizer.UI.Core.Models;

namespace CraneOptimizer.UI.Core.Interfaces
{
    public interface IElementLiftStateService
    {
        event Action? StateChanged;

        bool HasCurrentState { get; }

        ElementLiftStateDto? GetState(string? elementId);

        void SetStates(IEnumerable<ElementLiftStateDto> states);

        void Clear();
    }
}
