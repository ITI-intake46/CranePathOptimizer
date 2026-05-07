using CraneOptimizer.Domain.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Media.Media3D;

namespace CraneOptimizer.UI.Features.F7_Zones
{
    public enum SnapType
    {
        None,
        Point,
        Orthogonal,
        Grid
    }

    public class SnapResult
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

    public sealed class SnapSystem
    {
        private double _pointThreshold = 0.5;
        private double _gridSize = 1.0;

        public SnapType LastSnapType { get; private set; } = SnapType.None;
        public DomainPoint3D? LastSnapPoint { get; private set; }

        public void UpdateScale(double sceneSize)
        {
            _pointThreshold = Math.Max(0.25, sceneSize * 0.018);
            double raw = sceneSize / 20.0;
            _gridSize = raw <= 0 ? 1.0 : NiceNumber(raw);
        }

        public SnapResult Snap(
        DomainPoint3D raw,
        IEnumerable<DomainPoint3D> targets,
        DomainPoint3D? lastPoint,
        bool shiftDown,
        bool gridEnabled)
        {
            var allTargets = targets.ToList();
            double bestDist = double.MaxValue;
            DomainPoint3D bestPoint = raw;
            SnapType bestType = SnapType.None;

            // Point Snap
            foreach (var t in allTargets)
            {
                double d = Distance3D(raw, t);
                if (d <= _pointThreshold && d < bestDist)
                {
                    bestDist = d;
                    bestPoint = t;
                    bestType = SnapType.Point;
                }
            }

            if (bestType == SnapType.Point)
            {
                LastSnapType = bestType;
                LastSnapPoint = bestPoint;
                return new SnapResult(bestPoint, bestType);
            }

            // Orthogonal (SHIFT)
            if (shiftDown && lastPoint != null)
            {
                var locked = OrthoLock(raw, lastPoint); // lastPoint is already DomainPoint3D?
                if (gridEnabled) locked = GridSnap(locked);

                LastSnapType = SnapType.Orthogonal;
                LastSnapPoint = locked;
                return new SnapResult(locked, SnapType.Orthogonal);
            }

            // Grid
            if (gridEnabled)
            {
                var gridPt = GridSnap(raw);
                LastSnapType = SnapType.Grid;
                LastSnapPoint = gridPt;
                return new SnapResult(gridPt, SnapType.Grid);
            }

            LastSnapType = SnapType.None;
            LastSnapPoint = null;
            return new SnapResult(raw, SnapType.None);
        }
        private DomainPoint3D GridSnap(DomainPoint3D p)
        {
            double x = Math.Round(p.X / _gridSize) * _gridSize;
            double y = Math.Round(p.Y / _gridSize) * _gridSize;
            return new DomainPoint3D(x, y, p.Z);
        }

        private static DomainPoint3D OrthoLock(DomainPoint3D raw, DomainPoint3D anchor)
        {
            double dx = Math.Abs(raw.X - anchor.X);
            double dy = Math.Abs(raw.Y - anchor.Y);
            return dx >= dy
                ? new DomainPoint3D(raw.X, anchor.Y, anchor.Z)
                : new DomainPoint3D(anchor.X, raw.Y, anchor.Z);
        }

        private static double Distance3D(DomainPoint3D a, DomainPoint3D b)
        {
            double dx = a.X - b.X, dy = a.Y - b.Y, dz = a.Z - b.Z;
            return Math.Sqrt(dx * dx + dy * dy + dz * dz);
        }

        private static double NiceNumber(double v)
        {
            if (v <= 0) return 1.0;
            double[] steps = { 0.5, 1, 2, 5, 10, 20, 50, 100 };
            foreach (var s in steps)
                if (v <= s) return s;
            return Math.Ceiling(v);
        }
    }
}