using CraneOptimizer.UI.Features.F15_ObjectSnap.Models;
using HelixToolkit.Wpf;
using System;
using System.Windows;
using System.Windows.Media.Media3D;

namespace CraneOptimizer.UI.Features.F15_ObjectSnap.Services
{
    internal static class IfcSnapResolver
    {
        private const double VisibilityDepthTolerance = 15.0;
        private const double VisibilityProbeOffsetPx = 2.0;

        public static void TryEndpoints(
            SnapAccumulator snap,
            IfcSnapScreenIndex index)
        {
            foreach (var point in index.QueryEndpoints(snap.ScreenCursor, snap.SnapRadiusPx))
            {
                if (!IsVisibleFromCamera(snap, point.World, point.Screen, point.ViewDepth))
                    continue;

                snap.TryKnownScreenPoint(
                    point.World,
                    point.Screen,
                    OsnapSource.Ifc,
                    OsnapType.Endpoint,
                    viewDepth: point.ViewDepth);
            }
        }

        public static void TryMidpoints(
            SnapAccumulator snap,
            IfcSnapScreenIndex index)
        {
            foreach (var point in index.QueryMidpoints(snap.ScreenCursor, snap.SnapRadiusPx))
            {
                if (!IsVisibleFromCamera(snap, point.World, point.Screen, point.ViewDepth))
                    continue;

                snap.TryKnownScreenPoint(
                    point.World,
                    point.Screen,
                    OsnapSource.Ifc,
                    OsnapType.Midpoint,
                    viewDepth: point.ViewDepth);
            }
        }

        public static void TryNearest(
            SnapAccumulator snap,
            IfcSnapScreenIndex index)
        {
            foreach (var segment in index.QuerySegments(snap.ScreenCursor, snap.SnapRadiusPx))
            {
                if (!TryProjectedSegmentSnap(snap, segment, trimEnds: false, out var world, out var screen, out var viewDepth))
                    continue;

                if (!IsVisibleFromCamera(snap, world, screen, viewDepth))
                    continue;

                snap.TryKnownScreenPoint(
                    world,
                    screen,
                    OsnapSource.Ifc,
                    OsnapType.Nearest,
                    viewDepth: viewDepth);
            }
        }

        public static void TryPerpendicular(
            SnapAccumulator snap,
            IfcSnapScreenIndex index)
        {
            foreach (var segment in index.QuerySegments(snap.ScreenCursor, snap.SnapRadiusPx))
            {
                if (!TryProjectedSegmentSnap(snap, segment, trimEnds: true, out var world, out var screen, out var viewDepth))
                    continue;

                if (!IsVisibleFromCamera(snap, world, screen, viewDepth))
                    continue;

                snap.TryKnownScreenPoint(
                    world,
                    screen,
                    OsnapSource.Ifc,
                    OsnapType.Perpendicular,
                    viewDepth: viewDepth);
            }
        }

        private static bool TryProjectedSegmentSnap(
            SnapAccumulator snap,
            IfcSnapScreenIndex.SnapSegment segment,
            bool trimEnds,
            out Point3D world,
            out Point screen,
            out double? viewDepth)
        {
            world = default;
            screen = default;
            viewDepth = null;

            double dx = segment.ScreenB.X - segment.ScreenA.X;
            double dy = segment.ScreenB.Y - segment.ScreenA.Y;
            double lenSq = dx * dx + dy * dy;
            if (lenSq < 1)
                return false;

            double t = ((snap.ScreenCursor.X - segment.ScreenA.X) * dx + (snap.ScreenCursor.Y - segment.ScreenA.Y) * dy) / lenSq;
            if (trimEnds && (t < 0.05 || t > 0.95))
                return false;

            t = Math.Clamp(t, 0.0, 1.0);

            screen = new Point(segment.ScreenA.X + t * dx, segment.ScreenA.Y + t * dy);
            world = Lerp(segment.WorldA, segment.WorldB, t);
            viewDepth = segment.InterpolateDepth(t);
            return true;
        }

        private static Point3D Lerp(Point3D a, Point3D b, double t)
            => new(
                a.X + t * (b.X - a.X),
                a.Y + t * (b.Y - a.Y),
                a.Z + t * (b.Z - a.Z));

        private static bool IsVisibleFromCamera(
            SnapAccumulator snap,
            Point3D world,
            Point screen,
            double? candidateDepth)
        {
            if (!candidateDepth.HasValue)
                return true;

            if (!TryNearestSceneHitDepth(snap.Viewport, screen, out double nearestDepth))
                return true;

            return candidateDepth.Value <= nearestDepth + VisibilityDepthTolerance;
        }

        private static bool TryNearestSceneHitDepth(HelixViewport3D viewport, Point screen, out double depth)
        {
            depth = default;

            foreach (var probe in ProbePoints(screen))
            {
                try
                {
                    var hit = viewport.FindNearestPoint(probe);
                    if (!hit.HasValue)
                        continue;

                    if (OsnapProjection.TryGetCameraDepth(viewport, hit.Value, out depth))
                        return true;
                }
                catch
                {
                    // A failed hit-test should not disable snapping on visible silhouette edges.
                }
            }

            return false;
        }

        private static Point[] ProbePoints(Point center)
            => new[]
            {
                center,
                new Point(center.X - VisibilityProbeOffsetPx, center.Y),
                new Point(center.X + VisibilityProbeOffsetPx, center.Y),
                new Point(center.X, center.Y - VisibilityProbeOffsetPx),
                new Point(center.X, center.Y + VisibilityProbeOffsetPx)
            };
    }
}
