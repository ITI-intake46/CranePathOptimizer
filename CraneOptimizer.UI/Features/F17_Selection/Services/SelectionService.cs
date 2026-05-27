using CraneOptimizer.Domain.Schemas;
using CraneOptimizer.UI.Core.Interfaces;
using CraneOptimizer.UI.Features.F16_Filter.Services;
using System.Windows;

namespace CraneOptimizer.UI.Features.F17_Selection.Services
{
    /// <summary>
    /// Singleton selection store.
    ///
    /// ⚡ PERFORMANCE IS ALWAYS FIRST:
    ///   • Uses HashSet for O(1) add/remove/contains.
    ///   • Snapshot copy for thread-safe enumeration.
    ///   • Raises SelectionChanged only when the set *actually* changes.
    ///   • Keeps selection independent from visibility filters so isolate/hide
    ///     actions do not destroy the user's selection set.
    /// </summary>
    public sealed class SelectionService : ISelectionService
    {
        private readonly HashSet<PrecastElement> _selected =
            new(ReferenceEqualityComparer.Instance);

        private readonly ISceneService _sceneService;
        private bool _isSyncingScene;

        public SelectionService(
            IElementVisibilityFilterService filterService,
            ISceneService sceneService)
        {
            _sceneService = sceneService;

            // ⚡ One O(n) pass per filter change — negligible on typical model sizes.
            // This keeps the invariant: hidden elements are never selected.
            _sceneService.ElementsSelectionChanged += OnSceneElementsSelectionChanged;
        }

        // ──────────────────────────────────────────────────────────────────
        // ISelectionService
        // ──────────────────────────────────────────────────────────────────

        public event Action? SelectionChanged;

        public IReadOnlyCollection<PrecastElement> SelectedElements => _selected;

        public void Add(PrecastElement element)
        {
            if (_selected.Add(element))
                RaiseSelectionChanged();
        }

        public void Remove(PrecastElement element)
        {
            if (_selected.Remove(element))
                RaiseSelectionChanged();
        }

        public void Toggle(PrecastElement element)
        {
            if (!_selected.Remove(element))
                _selected.Add(element);

            RaiseSelectionChanged();
        }

        /// <summary>
        /// Batch-add — fires exactly ONE SelectionChanged even when adding hundreds
        /// of elements (e.g. box selection).
        /// </summary>
        public void AddRange(IEnumerable<PrecastElement> elements)
        {
            var before = _selected.Count;
            foreach (var element in elements)
                _selected.Add(element);

            if (_selected.Count != before)
                RaiseSelectionChanged();
        }

        public void Set(IEnumerable<PrecastElement> elements)
        {
            var incoming = new HashSet<PrecastElement>(
                elements,
                ReferenceEqualityComparer.Instance);

            if (_selected.Count == incoming.Count && _selected.SetEquals(incoming))
                return;

            _selected.Clear();
            foreach (var element in incoming)
                _selected.Add(element);

            RaiseSelectionChanged();
        }

        public void Clear()
        {
            if (_selected.Count == 0)
                return;

            _selected.Clear();
            RaiseSelectionChanged();
        }

        // ──────────────────────────────────────────────────────────────────
        // Filter integration — auto-remove hidden elements
        // ──────────────────────────────────────────────────────────────────

        private void OnSceneElementsSelectionChanged(List<PrecastElement> selectedElements)
        {
            if (_isSyncingScene)
                return;

            var incoming = new HashSet<PrecastElement>(
                selectedElements,
                ReferenceEqualityComparer.Instance);

            if (_selected.Count == incoming.Count && _selected.SetEquals(incoming))
                return;

            _selected.Clear();
            foreach (var element in incoming)
                _selected.Add(element);

            SelectionChanged?.Invoke();
        }

        // ──────────────────────────────────────────────────────────────────

        private void RaiseSelectionChanged()
        {
            // Always marshal to UI thread so subscribers can safely update
            // bindings without explicit Dispatcher.Invoke calls.
            // Use fully qualified name to avoid collision with CraneOptimizer.Application namespace.
            var app = System.Windows.Application.Current;
            if (app?.Dispatcher?.CheckAccess() == false)
                app.Dispatcher.Invoke(RaiseCore);
            else
                RaiseCore();
        }

        private void RaiseCore()
        {
            _isSyncingScene = true;
            try
            {
                _sceneService.SetSelectedElements(_selected.ToList());
            }
            finally
            {
                _isSyncingScene = false;
            }

            SelectionChanged?.Invoke();
        }
    }
}
