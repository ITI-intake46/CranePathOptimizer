using CraneOptimizer.UI.Features.F15_ObjectSnap.Models;
using HelixToolkit.Wpf;
using System;
using System.Windows;
using System.Windows.Media.Media3D;

namespace CraneOptimizer.UI.Features.F15_ObjectSnap.Services
{
    internal sealed class SnapAccumulator
    {
        private readonly Point _screenCursor;
        private readonly HelixViewport3D _viewport;
        private readonly double _snapRadiusPx;

        private Point3D? _bestWorld;
        private Point? _bestScreen;
        private double _bestDistance = double.MaxValue;
        private OsnapType _bestType = OsnapType.Endpoint;
        private OsnapSource _bestSource = OsnapSource.Zone;

        public SnapAccumulator(Point screenCursor, HelixViewport3D viewport, double snapRadiusPx)
        {
            _screenCursor = screenCursor;
            _viewport = viewport;
            _snapRadiusPx = snapRadiusPx;
        }

        public bool HasResult => _bestWorld.HasValue;
        public double BestDistance => _bestDistance;
        public OsnapSource? BestSource => _bestWorld.HasValue ? _bestSource : null;
        public Point ScreenCursor => _screenCursor;
        public double SnapRadiusPx => _snapRadiusPx;

        public void TryVertex(
            Point3D world,
            OsnapSource source,
            OsnapType type = OsnapType.Endpoint,
            Func<Point3D, Point, bool>? accept = null)
        {
            if (!OsnapProjection.TryProject(_viewport, world, out var screen))
                return;

            TryKnownScreenPoint(world, screen, source, type, accept);
        }

        public void TryKnownScreenPoint(
            Point3D world,
            Point screen,
            OsnapSource source,
            OsnapType type,
            Func<Point3D, Point, bool>? accept = null)
        {
            if (!IsFinite(world) || !IsFinite(screen))
                return;

            double distance = PixelDistance(_screenCursor, screen);
            if (distance > _snapRadiusPx || distance >= _bestDistance)
                return;

            if (accept != null && !accept(world, screen))
                return;

            _bestWorld = world;
            _bestScreen = screen;
            _bestDistance = distance;
            _bestType = type;
            _bestSource = source;
        }

        public void TryEdge(
            Point3D a,
            Point3D b,
            OsnapSource source,
            OsnapType type,
            Func<Point3D, Point, bool>? accept = null)
        {
            if (!OsnapProjection.TryProject(_viewport, a, out var sa) ||
                !OsnapProjection.TryProject(_viewport, b, out var sb))
                return;

            double dx = sb.X - sa.X;
            double dy = sb.Y - sa.Y;
            double lenSq = dx * dx + dy * dy;
            if (lenSq < 1e-6)
            {
                TryVertex(a, source, type);
                return;
            }

            double t = Math.Clamp(((_screenCursor.X - sa.X) * dx + (_screenCursor.Y - sa.Y) * dy) / lenSq, 0, 1);
            var screen = new Point(sa.X + t * dx, sa.Y + t * dy);
            var world = Lerp(a, b, t);
            TryKnownScreenPoint(world, screen, source, type, accept);
        }

        public void TryPerpendicular(
            Point3D a,
            Point3D b,
            OsnapSource source,
            Func<Point3D, Point, bool>? accept = null)
        {
            if (!OsnapProjection.TryProject(_viewport, a, out var sa) ||
                !OsnapProjection.TryProject(_viewport, b, out var sb))
                return;

            double dx = sb.X - sa.X;
            double dy = sb.Y - sa.Y;
            double lenSq = dx * dx + dy * dy;
            if (lenSq < 1)
                return;

            double t = ((_screenCursor.X - sa.X) * dx + (_screenCursor.Y - sa.Y) * dy) / lenSq;
            if (t < 0.05 || t > 0.95)
                return;

            var screen = new Point(sa.X + t * dx, sa.Y + t * dy);
            var world = Lerp(a, b, t);
            TryKnownScreenPoint(world, screen, source, OsnapType.Perpendicular, accept);
        }

        public OsnapResult? ToResult()
        {
            if (!_bestWorld.HasValue)
                return null;

            return new OsnapResult(_bestType, _bestWorld.Value, _bestScreen, _bestSource);
        }

        public static double PixelDistance(Point a, Point b)
        {
            double dx = a.X - b.X;
            double dy = a.Y - b.Y;
            return Math.Sqrt(dx * dx + dy * dy);
        }

        private static Point3D Lerp(Point3D a, Point3D b, double t)
            => new(
                a.X + t * (b.X - a.X),
                a.Y + t * (b.Y - a.Y),
                a.Z + t * (b.Z - a.Z));

        private static bool IsFinite(Point3D point)
            => double.IsFinite(point.X) && double.IsFinite(point.Y) && double.IsFinite(point.Z);

        private static bool IsFinite(Point point)
            => double.IsFinite(point.X) && double.IsFinite(point.Y);
    }
}
