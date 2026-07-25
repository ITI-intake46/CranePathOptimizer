using CraneOptimizer.Domain.Schemas;
using CraneOptimizer.Domain.Schemas.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Media.Media3D;

namespace CraneOptimizer.UI.Features.F7_Zones
{
    internal sealed record ZoneOverlayRegion(
        ZoneType Type,
        Point3D[] Vertices,
        Rect Bounds,
        double AverageZ);

    internal static class ZoneOverlayGeometry
    {
        private const double GeometryTolerance = 1e-8;
        private const double PlaneTolerance = 0.05;

        public static List<ZoneOverlayRegion> CreateRegions(
            IEnumerable<GreenAreaZone> zones)
        {
            var regions = new List<ZoneOverlayRegion>();
            foreach (GreenAreaZone zone in zones)
            {
                Point3D[] vertices = NormalizeVertices(zone);
                if (vertices.Length < 3)
                    continue;

                double minX = vertices.Min(point => point.X);
                double minY = vertices.Min(point => point.Y);
                double maxX = vertices.Max(point => point.X);
                double maxY = vertices.Max(point => point.Y);
                regions.Add(new ZoneOverlayRegion(
                    zone.Type,
                    vertices,
                    new Rect(minX, minY, maxX - minX, maxY - minY),
                    vertices.Average(point => point.Z)));
            }

            return regions;
        }

        public static bool AreEquivalent(
            IReadOnlyList<ZoneOverlayRegion> left,
            IReadOnlyList<ZoneOverlayRegion> right)
        {
            if (left.Count != right.Count)
                return false;

            for (int regionIndex = 0; regionIndex < left.Count; regionIndex++)
            {
                ZoneOverlayRegion first = left[regionIndex];
                ZoneOverlayRegion second = right[regionIndex];
                if (first.Type != second.Type ||
                    first.Vertices.Length != second.Vertices.Length)
                {
                    return false;
                }

                for (int vertexIndex = 0;
                     vertexIndex < first.Vertices.Length;
                     vertexIndex++)
                {
                    if (!AreClose(
                            first.Vertices[vertexIndex],
                            second.Vertices[vertexIndex]))
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        public static bool TouchesAny(
            Point3D point,
            IReadOnlyList<ZoneOverlayRegion> regions)
        {
            foreach (ZoneOverlayRegion region in regions)
            {
                if (IsOnSamePlane(point.Z, point.Z, region) &&
                    IsInsideOrOnBoundary(point, region))
                {
                    return true;
                }
            }

            return false;
        }

        public static bool TouchesAny(
            Point3D start,
            Point3D end,
            IReadOnlyList<ZoneOverlayRegion> regions)
        {
            foreach (ZoneOverlayRegion region in regions)
            {
                if (!IsOnSamePlane(start.Z, end.Z, region) ||
                    !BoundsOverlap(start, end, region.Bounds))
                {
                    continue;
                }

                if (IsInsideOrOnBoundary(start, region) ||
                    IsInsideOrOnBoundary(end, region))
                {
                    return true;
                }

                for (int index = 0; index < region.Vertices.Length; index++)
                {
                    Point3D edgeStart = region.Vertices[index];
                    Point3D edgeEnd =
                        region.Vertices[(index + 1) % region.Vertices.Length];
                    if (SegmentsIntersect(start, end, edgeStart, edgeEnd))
                        return true;
                }
            }

            return false;
        }

        public static List<(Point3D A, Point3D B)> ClipOutsideForbidden(
            Point3D start,
            Point3D end,
            IReadOnlyList<ZoneOverlayRegion> regions)
        {
            ZoneOverlayRegion[] candidates = regions
                .Where(region =>
                    region.Type == ZoneType.Forbidden &&
                    IsOnSamePlane(start.Z, end.Z, region) &&
                    BoundsOverlap(start, end, region.Bounds))
                .ToArray();
            if (candidates.Length == 0)
                return new List<(Point3D, Point3D)> { (start, end) };

            var parameters = new List<double> { 0, 1 };
            foreach (ZoneOverlayRegion region in candidates)
            {
                for (int index = 0; index < region.Vertices.Length; index++)
                {
                    AddIntersectionParameters(
                        start,
                        end,
                        region.Vertices[index],
                        region.Vertices[(index + 1) % region.Vertices.Length],
                        parameters);
                }
            }

            double[] ordered = parameters
                .Select(parameter => Math.Clamp(parameter, 0, 1))
                .OrderBy(parameter => parameter)
                .Aggregate(
                    new List<double>(),
                    (result, parameter) =>
                    {
                        if (result.Count == 0 ||
                            Math.Abs(result[^1] - parameter) > GeometryTolerance)
                        {
                            result.Add(parameter);
                        }

                        return result;
                    })
                .ToArray();

            var visibleSegments = new List<(Point3D, Point3D)>();
            for (int index = 0; index < ordered.Length - 1; index++)
            {
                double from = ordered[index];
                double to = ordered[index + 1];
                if (to - from <= GeometryTolerance)
                    continue;

                Point3D midpoint = Interpolate(start, end, (from + to) / 2);
                bool isCovered = candidates.Any(region =>
                    IsInsideOrOnBoundary(midpoint, region));
                if (!isCovered)
                {
                    visibleSegments.Add((
                        Interpolate(start, end, from),
                        Interpolate(start, end, to)));
                }
            }

            return visibleSegments;
        }

        internal static bool IsInsideOrOnBoundary(
            Point3D point,
            ZoneOverlayRegion region)
        {
            if (point.X < region.Bounds.Left - GeometryTolerance ||
                point.X > region.Bounds.Right + GeometryTolerance ||
                point.Y < region.Bounds.Top - GeometryTolerance ||
                point.Y > region.Bounds.Bottom + GeometryTolerance)
            {
                return false;
            }

            for (int index = 0; index < region.Vertices.Length; index++)
            {
                Point3D start = region.Vertices[index];
                Point3D end =
                    region.Vertices[(index + 1) % region.Vertices.Length];
                if (IsPointOnSegment(point, start, end))
                    return true;
            }

            bool inside = false;
            for (int current = 0, previous = region.Vertices.Length - 1;
                 current < region.Vertices.Length;
                 previous = current++)
            {
                Point3D a = region.Vertices[current];
                Point3D b = region.Vertices[previous];
                bool crossesRay =
                    (a.Y > point.Y) != (b.Y > point.Y) &&
                    point.X <
                    (b.X - a.X) * (point.Y - a.Y) /
                    (b.Y - a.Y) + a.X;
                if (crossesRay)
                    inside = !inside;
            }

            return inside;
        }

        private static Point3D[] NormalizeVertices(GreenAreaZone zone)
        {
            var vertices = new List<Point3D>();
            foreach (var vertex in zone.Vertices)
            {
                var point = new Point3D(vertex.X, vertex.Y, vertex.Z);
                if (!IsFinite(point) ||
                    vertices.Count > 0 && AreClose(vertices[^1], point))
                {
                    continue;
                }

                vertices.Add(point);
            }

            if (vertices.Count > 1 && AreClose(vertices[0], vertices[^1]))
                vertices.RemoveAt(vertices.Count - 1);

            return vertices.ToArray();
        }

        private static bool IsOnSamePlane(
            double startZ,
            double endZ,
            ZoneOverlayRegion region)
        {
            double minZ = Math.Min(startZ, endZ) - PlaneTolerance;
            double maxZ = Math.Max(startZ, endZ) + PlaneTolerance;
            return region.AverageZ >= minZ && region.AverageZ <= maxZ;
        }

        private static bool BoundsOverlap(
            Point3D start,
            Point3D end,
            Rect bounds)
        {
            return Math.Max(start.X, end.X) >=
                   bounds.Left - GeometryTolerance &&
                   Math.Min(start.X, end.X) <=
                   bounds.Right + GeometryTolerance &&
                   Math.Max(start.Y, end.Y) >=
                   bounds.Top - GeometryTolerance &&
                   Math.Min(start.Y, end.Y) <=
                   bounds.Bottom + GeometryTolerance;
        }

        private static bool SegmentsIntersect(
            Point3D firstStart,
            Point3D firstEnd,
            Point3D secondStart,
            Point3D secondEnd)
        {
            var parameters = new List<double>();
            AddIntersectionParameters(
                firstStart,
                firstEnd,
                secondStart,
                secondEnd,
                parameters);
            return parameters.Count > 0;
        }

        private static void AddIntersectionParameters(
            Point3D start,
            Point3D end,
            Point3D edgeStart,
            Point3D edgeEnd,
            ICollection<double> parameters)
        {
            double segmentX = end.X - start.X;
            double segmentY = end.Y - start.Y;
            double edgeX = edgeEnd.X - edgeStart.X;
            double edgeY = edgeEnd.Y - edgeStart.Y;
            double cross = Cross(segmentX, segmentY, edgeX, edgeY);
            double offsetX = edgeStart.X - start.X;
            double offsetY = edgeStart.Y - start.Y;

            if (Math.Abs(cross) <= GeometryTolerance)
            {
                if (Math.Abs(Cross(offsetX, offsetY, segmentX, segmentY)) >
                    GeometryTolerance)
                {
                    return;
                }

                double lengthSquared =
                    segmentX * segmentX + segmentY * segmentY;
                if (lengthSquared <= GeometryTolerance)
                    return;

                double first =
                    (offsetX * segmentX + offsetY * segmentY) /
                    lengthSquared;
                double second =
                    ((edgeEnd.X - start.X) * segmentX +
                     (edgeEnd.Y - start.Y) * segmentY) /
                    lengthSquared;
                double overlapStart = Math.Max(0, Math.Min(first, second));
                double overlapEnd = Math.Min(1, Math.Max(first, second));
                if (overlapStart <= overlapEnd + GeometryTolerance)
                {
                    parameters.Add(overlapStart);
                    parameters.Add(overlapEnd);
                }

                return;
            }

            double segmentParameter =
                Cross(offsetX, offsetY, edgeX, edgeY) / cross;
            double edgeParameter =
                Cross(offsetX, offsetY, segmentX, segmentY) / cross;
            if (segmentParameter >= -GeometryTolerance &&
                segmentParameter <= 1 + GeometryTolerance &&
                edgeParameter >= -GeometryTolerance &&
                edgeParameter <= 1 + GeometryTolerance)
            {
                parameters.Add(segmentParameter);
            }
        }

        private static bool IsPointOnSegment(
            Point3D point,
            Point3D start,
            Point3D end)
        {
            double segmentX = end.X - start.X;
            double segmentY = end.Y - start.Y;
            double pointX = point.X - start.X;
            double pointY = point.Y - start.Y;
            if (Math.Abs(Cross(segmentX, segmentY, pointX, pointY)) >
                GeometryTolerance)
            {
                return false;
            }

            double dot = pointX * segmentX + pointY * segmentY;
            double lengthSquared =
                segmentX * segmentX + segmentY * segmentY;
            return dot >= -GeometryTolerance &&
                   dot <= lengthSquared + GeometryTolerance;
        }

        private static Point3D Interpolate(
            Point3D start,
            Point3D end,
            double ratio) =>
            new(
                start.X + (end.X - start.X) * ratio,
                start.Y + (end.Y - start.Y) * ratio,
                start.Z + (end.Z - start.Z) * ratio);

        private static double Cross(
            double firstX,
            double firstY,
            double secondX,
            double secondY) =>
            firstX * secondY - firstY * secondX;

        private static bool AreClose(Point3D first, Point3D second) =>
            Math.Abs(first.X - second.X) <= GeometryTolerance &&
            Math.Abs(first.Y - second.Y) <= GeometryTolerance &&
            Math.Abs(first.Z - second.Z) <= GeometryTolerance;

        private static bool IsFinite(Point3D point) =>
            double.IsFinite(point.X) &&
            double.IsFinite(point.Y) &&
            double.IsFinite(point.Z);
    }
}
