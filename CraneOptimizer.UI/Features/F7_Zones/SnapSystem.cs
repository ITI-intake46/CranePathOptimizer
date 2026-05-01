using CraneOptimizer.Domain.Common;

namespace CraneOptimizer.UI.Features.F7_Zones
{
    // ── Snap result types ────────────────────────────────────────────────────
    public enum SnapType { None, Point, Orthogonal, Grid }

    public sealed class SnapResult
    {
        public DomainPoint3D Point { get; }
        public SnapType Type { get; }
        public bool Snapped => Type != SnapType.None;

        public SnapResult(DomainPoint3D point, SnapType type)
        {
            Point = point;
            Type = type;
        }
    }

    // ── Snap engine ──────────────────────────────────────────────────────────
    /// <summary>
    /// Self-contained snap engine living entirely inside F7_Zones.
    /// Priority order: Point snap → Orthogonal lock (SHIFT) → Grid → None.
    /// </summary>
    public sealed class SnapSystem
    {
        private double _pointThreshold = 0.5;
        private double _gridSize = 1.0;   // metres

        // Call once per rendered frame with the current scene extents.
        public void UpdateScale(double sceneSize)
        {
            _pointThreshold = System.Math.Max(0.25, sceneSize * 0.018);
            // Grid cell = ~1/20th of scene, rounded to a "nice" number.
            double raw = sceneSize / 20.0;
            _gridSize = raw <= 0 ? 1.0 : NiceNumber(raw);
        }

        /// <summary>
        /// Resolve the final snapped point given raw cursor position.
        /// </summary>
        /// <param name="raw">Raw world-space cursor position.</param>
        /// <param name="targets">Candidate snap targets (zone vertices, measure points, …).</param>
        /// <param name="lastPoint">Last committed point (used for orthogonal lock).</param>
        /// <param name="shiftDown">When true, orthogonal angle lock is active.</param>
        /// <param name="gridEnabled">When true, grid snap is a fallback.</param>
        public SnapResult Snap(
            DomainPoint3D raw,
            IEnumerable<DomainPoint3D> targets,
            DomainPoint3D? lastPoint,
            bool shiftDown,
            bool gridEnabled)
        {
            // 1. Point snap — highest priority, unaffected by shift.
            foreach (var t in targets)
            {
                if (Distance3D(raw, t) <= _pointThreshold)
                    return new SnapResult(t, SnapType.Point);
            }

            // 2. Orthogonal lock (SHIFT held).
            if (shiftDown && lastPoint != null)
            {
                var locked = OrthoLock(raw, lastPoint);
                if (gridEnabled) locked = GridSnap(locked);
                return new SnapResult(locked, SnapType.Orthogonal);
            }

            // 3. Grid snap (fallback when enabled).
            if (gridEnabled)
                return new SnapResult(GridSnap(raw), SnapType.Grid);

            return new SnapResult(raw, SnapType.None);
        }

        // ── Helpers ──────────────────────────────────────────────────────────

        private DomainPoint3D GridSnap(DomainPoint3D p)
        {
            double x = System.Math.Round(p.X / _gridSize) * _gridSize;
            double y = System.Math.Round(p.Y / _gridSize) * _gridSize;
            return new DomainPoint3D(x, y, p.Z);
        }

        private static DomainPoint3D OrthoLock(DomainPoint3D raw, DomainPoint3D anchor)
        {
            double dx = System.Math.Abs(raw.X - anchor.X);
            double dy = System.Math.Abs(raw.Y - anchor.Y);
            return dx >= dy
                ? new DomainPoint3D(raw.X, anchor.Y, anchor.Z)   // lock to horizontal
                : new DomainPoint3D(anchor.X, raw.Y, anchor.Z);   // lock to vertical
        }

        private static double Distance3D(DomainPoint3D a, DomainPoint3D b)
        {
            double dx = a.X - b.X, dy = a.Y - b.Y, dz = a.Z - b.Z;
            return System.Math.Sqrt(dx * dx + dy * dy + dz * dz);
        }

        // Round to 0.5 / 1 / 2 / 5 / 10 / 20 / 50 …
        private static double NiceNumber(double v)
        {
            if (v <= 0) return 1.0;
            double[] steps = { 0.5, 1, 2, 5, 10, 20, 50, 100 };
            foreach (var s in steps)
                if (v <= s) return s;
            return System.Math.Ceiling(v);
        }
    }
}