using CraneOptimizer.Domain.Schemas;

namespace CraneOptimizer.UI.Features.F17_Selection.Services
{
    /// <summary>
    /// Always-on, zero-overhead element selection service.
    /// ⚡ PERFORMANCE IS ALWAYS FIRST — selection state changes never trigger
    /// synchronous rendering; consumers subscribe to SelectionChanged and
    /// schedule their own dirty-flag pass.
    /// </summary>
    public interface ISelectionService
    {
        /// <summary>Raised after the selection set changes, on the UI thread.</summary>
        event Action? SelectionChanged;

        /// <summary>Current selection — snapshot safe to enumerate without locking.</summary>
        IReadOnlyCollection<PrecastElement> SelectedElements { get; }

        void Add(PrecastElement element);

        /// <summary>Toggles element in/out of the selection set.</summary>
        void Toggle(PrecastElement element);

        /// <summary>
        /// Batch-add. Triggers only ONE SelectionChanged event regardless of how
        /// many elements are added.
        /// </summary>
        void AddRange(IEnumerable<PrecastElement> elements);

        /// <summary>Replaces the current selection in one SelectionChanged event.</summary>
        void Set(IEnumerable<PrecastElement> elements);

        void Clear();
    }
}
