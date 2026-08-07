using CraneOptimizer.UI.Features.F2_IfcViewer;

namespace CraneOptimizer.UI.Features.F8_GAOptimizer.Rendering
{
    public sealed class OptimizationResultViewportState
    {
        private HashSet<string> _liftableIds = new(StringComparer.Ordinal);
        private HashSet<string> _notLiftableIds = new(StringComparer.Ordinal);

        public bool IsActive { get; private set; }

        public void Apply(
            IEnumerable<string> liftableIds,
            IEnumerable<string> notLiftableIds)
        {
            _liftableIds = ToIdSet(liftableIds);
            _notLiftableIds = ToIdSet(notLiftableIds);
            _notLiftableIds.ExceptWith(_liftableIds);
            IsActive = true;
        }

        public void Clear()
        {
            _liftableIds.Clear();
            _notLiftableIds.Clear();
            IsActive = true;
        }

        public void Release()
        {
            _liftableIds.Clear();
            _notLiftableIds.Clear();
            IsActive = false;
        }

        public ElementVisualizationStatus Resolve(
            string elementId,
            bool isSelected,
            ElementVisualizationStatus underlyingStatus)
        {
            if (!IsActive)
                return underlyingStatus;

            if (isSelected)
                return ElementVisualizationStatus.Selected;

            if (_liftableIds.Contains(elementId))
                return ElementVisualizationStatus.Liftable;

            if (_notLiftableIds.Contains(elementId))
                return ElementVisualizationStatus.NotLiftable;

            return ElementVisualizationStatus.Default;
        }

        private static HashSet<string> ToIdSet(IEnumerable<string> identifiers)
        {
            ArgumentNullException.ThrowIfNull(identifiers);
            return identifiers
                .Where(identifier => !string.IsNullOrWhiteSpace(identifier))
                .ToHashSet(StringComparer.Ordinal);
        }
    }
}
