using CraneOptimizer.UI.Features.F15_ObjectSnap.Models;
using System.Collections.Generic;
using System.Windows.Media.Media3D;

namespace CraneOptimizer.UI.Features.F15_ObjectSnap.Services
{
    internal static class DxfSnapResolver
    {
        public static void TryEndpoints(SnapAccumulator snap, DxfSnapScreenIndex index)
        {
            foreach (var point in index.QueryEndpoints(snap.ScreenCursor, snap.SnapRadiusPx))
                snap.TryKnownScreenPoint(point.World, point.Screen, OsnapSource.Dxf, OsnapType.Endpoint);
        }

        public static void TryMidpoints(SnapAccumulator snap, DxfSnapScreenIndex index)
        {
            foreach (var point in index.QueryMidpoints(snap.ScreenCursor, snap.SnapRadiusPx))
                snap.TryKnownScreenPoint(point.World, point.Screen, OsnapSource.Dxf, OsnapType.Midpoint);
        }

        public static void TryNearest(SnapAccumulator snap, DxfSnapScreenIndex index)
        {
            foreach (var segment in index.QuerySegments(snap.ScreenCursor, snap.SnapRadiusPx))
            {
                if (!TryProjectedSegmentSnap(snap, segment, trimEnds: false, out var world, out var screen))
                    continue;

                snap.TryKnownScreenPoint(world, screen, OsnapSource.Dxf, OsnapType.Nearest);
            }
        }

        public static void TryPerpendicular(SnapAccumulator snap, DxfSnapScreenIndex index)
        {
            foreach (var segment in index.QuerySegments(snap.ScreenCursor, snap.SnapRadiusPx))
            {
                if (!TryProjectedSegmentSnap(snap, segment, trimEnds: true, out var world, out var screen))
                    continue;

                snap.TryKnownScreenPoint(world, screen, OsnapSource.Dxf, OsnapType.Perpendicular);
            }
        }

        public static void TryCenters(SnapAccumulator snap, DxfSnapScreenIndex index)
        {
            foreach (var point in index.QueryCenters(snap.ScreenCursor, snap.SnapRadiusPx))
                snap.TryKnownScreenPoint(point.World, point.Screen, OsnapSource.Dxf, OsnapType.Center);
        }

        public static void TryQuadrants(SnapAccumulator snap, DxfSnapScreenIndex index)
        {
            foreach (var point in index.QueryQuadrants(snap.ScreenCursor, snap.SnapRadiusPx))
                snap.TryKnownScreenPoint(point.World, point.Screen, OsnapSource.Dxf, OsnapType.Quadrant);
        }

        private static bool TryProjectedSegmentSnap(
            SnapAccumulator snap,
            DxfSnapScreenIndex.SnapSegment segment,
            bool trimEnds,
            out Point3D world,
            out System.Windows.Point screen)
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
            screen = new System.Windows.Point(segment.ScreenA.X + t * dx, segment.ScreenA.Y + t * dy);
            world = new Point3D(
                segment.WorldA.X + t * (segment.WorldB.X - segment.WorldA.X),
                segment.WorldA.Y + t * (segment.WorldB.Y - segment.WorldA.Y),
                segment.WorldA.Z + t * (segment.WorldB.Z - segment.WorldA.Z));
            return true;
        }
    }
}
