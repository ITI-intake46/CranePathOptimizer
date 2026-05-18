using CraneOptimizer.UI.Features.F15_ObjectSnap.Models;
using System;
using System.Windows;
using System.Windows.Media.Media3D;

namespace CraneOptimizer.UI.Features.F15_ObjectSnap.Services
{
    internal static class IfcSnapResolver
    {
        public static void TryEndpoints(
            SnapAccumulator snap,
            IfcSnapScreenIndex index)
        {
            foreach (var point in index.QueryEndpoints(snap.ScreenCursor, snap.SnapRadiusPx))
            {
                snap.TryKnownScreenPoint(
                    point.World,
                    point.Screen,
                    OsnapSource.Ifc,
                    OsnapType.Endpoint);
            }
        }

        public static void TryMidpoints(
            SnapAccumulator snap,
            IfcSnapScreenIndex index)
        {
            foreach (var point in index.QueryMidpoints(snap.ScreenCursor, snap.SnapRadiusPx))
            {
                snap.TryKnownScreenPoint(
                    point.World,
                    point.Screen,
                    OsnapSource.Ifc,
                    OsnapType.Midpoint);
            }
        }

        public static void TryNearest(
            SnapAccumulator snap,
            IfcSnapScreenIndex index)
        {
            foreach (var segment in index.QuerySegments(snap.ScreenCursor, snap.SnapRadiusPx))
            {
                if (!TryProjectedSegmentSnap(snap, segment, trimEnds: false, out var world, out var screen))
                    continue;

                snap.TryKnownScreenPoint(
                    world,
                    screen,
                    OsnapSource.Ifc,
                    OsnapType.Nearest);
            }
        }

        public static void TryPerpendicular(
            SnapAccumulator snap,
            IfcSnapScreenIndex index)
        {
            foreach (var segment in index.QuerySegments(snap.ScreenCursor, snap.SnapRadiusPx))
            {
                if (!TryProjectedSegmentSnap(snap, segment, trimEnds: true, out var world, out var screen))
                    continue;

                snap.TryKnownScreenPoint(
                    world,
                    screen,
                    OsnapSource.Ifc,
                    OsnapType.Perpendicular);
            }
        }

        private static bool TryProjectedSegmentSnap(
            SnapAccumulator snap,
            IfcSnapScreenIndex.SnapSegment segment,
            bool trimEnds,
            out Point3D world,
            out Point screen)
        {
            world = default;
            screen = default;

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
            return true;
        }

        private static Point3D Lerp(Point3D a, Point3D b, double t)
            => new(
                a.X + t * (b.X - a.X),
                a.Y + t * (b.Y - a.Y),
                a.Z + t * (b.Z - a.Z));
    }
}
