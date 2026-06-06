using CraneOptimizer.Domain.Schemas;
using System.Windows;

namespace CraneOptimizer.UI.Features.F17_Selection.Services
{
    /// <summary>
    /// Caches the ray-cast hit list under the cursor so that Tab-key cycling
    /// never re-issues a GPU pick.
    /// ⚡ PERFORMANCE IS ALWAYS FIRST — one ray-cast per hover position, reused
    /// until the cursor moves more than <see cref="MovementThresholdPx"/> pixels.
    /// </summary>
    public sealed class HoverHitCache
    {
        /// <summary>Pixel threshold beyond which the cache is considered stale.</summary>
        public const double MovementThresholdPx = 2.0;

        private List<PrecastElement> _candidates = new();
        private Point _lastPosition = new(double.NaN, double.NaN);

        /// <summary>Current Tab-cycle index within <see cref="Candidates"/>.</summary>
        public int CycleIndex { get; private set; } = 0;

        /// <summary>Read-only view of cached candidates (deepest-first order).</summary>
        public IReadOnlyList<PrecastElement> Candidates => _candidates;

        /// <summary>Last known cursor position in viewport coordinates.</summary>
        public Point LastCursorPosition => _lastPosition;

        /// <summary>True once at least one viewport cursor position has been observed.</summary>
        public bool HasLastCursorPosition => !double.IsNaN(_lastPosition.X) && !double.IsNaN(_lastPosition.Y);

        /// <summary>True when the cache holds at least one candidate.</summary>
        public bool HasCandidates => _candidates.Count > 0;

        /// <summary>
        /// Called on MouseMove. Invalidates the cache if the cursor moved beyond threshold.
        /// </summary>
        public void OnMouseMove(Point currentPosition)
        {
            if (IsStale(currentPosition))
                Invalidate();

            _lastPosition = currentPosition;
        }

        /// <summary>
        /// Populates the cache with a fresh ray-cast result.
        /// Should only be called once per click/hover position.
        /// </summary>
        public void SetCandidates(Point position, IEnumerable<PrecastElement> hits)
        {
            _lastPosition = position;
            _candidates = hits.ToList();
            CycleIndex = 0;
        }

        /// <summary>
        /// Moves the cycle cursor to the supplied element when it exists in the
        /// current hit list, so the next Tab press walks behind the active pick.
        /// </summary>
        public void AlignTo(PrecastElement element)
        {
            for (var i = 0; i < _candidates.Count; i++)
            {
                if (ReferenceEquals(_candidates[i], element))
                {
                    CycleIndex = i;
                    return;
                }
            }
        }

        /// <summary>
        /// Returns the currently cycled element (the one Tab would select).
        /// Returns null if cache is empty.
        /// </summary>
        public PrecastElement? CurrentCandidate =>
            _candidates.Count > 0 ? _candidates[CycleIndex % _candidates.Count] : null;

        /// <summary>
        /// Advances the cycle index and returns the next candidate.
        /// No GPU work — pure list walk.
        /// </summary>
        public PrecastElement? CycleNext()
        {
            if (_candidates.Count == 0)
                return null;

            CycleIndex = (CycleIndex + 1) % _candidates.Count;
            return _candidates[CycleIndex];
        }

        /// <summary>
        /// Recedes the cycle index and returns the previous candidate.
        /// </summary>
        public PrecastElement? CyclePrevious()
        {
            if (_candidates.Count == 0)
                return null;

            CycleIndex = (CycleIndex - 1 + _candidates.Count) % _candidates.Count;
            return _candidates[CycleIndex];
        }

        /// <summary>Clears all cached candidates and resets the cycle index.</summary>
        public void Invalidate()
        {
            _candidates.Clear();
            CycleIndex = 0;
        }

        private bool IsStale(Point newPosition) =>
            !double.IsNaN(_lastPosition.X) &&
            (Math.Abs(newPosition.X - _lastPosition.X) > MovementThresholdPx ||
             Math.Abs(newPosition.Y - _lastPosition.Y) > MovementThresholdPx);
    }
}
