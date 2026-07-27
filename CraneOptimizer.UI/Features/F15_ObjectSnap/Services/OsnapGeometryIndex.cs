using CraneOptimizer.UI.Features.F15_ObjectSnap.Models;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Windows.Media.Media3D;

namespace CraneOptimizer.UI.Features.F15_ObjectSnap.Services
{
    internal readonly record struct OsnapPointCandidate(
        Point3D World,
        OsnapType Type,
        OsnapSource Source);

    internal readonly record struct OsnapSegmentCandidate(
        long Id,
        Point3D A,
        Point3D B,
        OsnapSource Source);

    internal sealed class OsnapGeometryIndex
    {
        private static long _nextSegmentId;

        private readonly OsnapSpatialIndex<OsnapPointCandidate> _points;
        private readonly OsnapSpatialIndex<OsnapSegmentCandidate> _segments;

        private OsnapGeometryIndex(
            OsnapSource source,
            OsnapSpatialIndex<OsnapPointCandidate> points,
            OsnapSpatialIndex<OsnapSegmentCandidate> segments)
        {
            Source = source;
            _points = points;
            _segments = segments;
            Bounds = points.Bounds.Include(segments.Bounds);
        }

        public OsnapSource Source { get; }
        public OsnapWorldBounds Bounds { get; }
        public int PointCount => _points.Count;
        public int SegmentCount => _segments.Count;

        public static OsnapGeometryIndex Empty(OsnapSource source) =>
            Create(
                source,
                Array.Empty<OsnapPointCandidate>(),
                Array.Empty<OsnapSegmentCandidate>(),
                CancellationToken.None);

        public static OsnapGeometryIndex BuildDxf(
            IReadOnlyList<Point3D> endpoints,
            IReadOnlyList<(Point3D A, Point3D B)> segments,
            IReadOnlyList<Point3D> centers,
            IReadOnlyList<Point3D> quadrants,
            IReadOnlyList<(Point3D A, Point3D B)> midpointSegments,
            CancellationToken cancellationToken)
        {
            var points = new List<OsnapPointCandidate>(
                endpoints.Count +
                midpointSegments.Count +
                centers.Count +
                quadrants.Count);
            var pointKeys = new HashSet<PointKey>();
            AddPoints(
                points,
                pointKeys,
                endpoints,
                OsnapType.Endpoint,
                OsnapSource.Dxf,
                cancellationToken);
            AddMidpoints(
                points,
                pointKeys,
                midpointSegments,
                OsnapSource.Dxf,
                cancellationToken);
            AddPoints(
                points,
                pointKeys,
                centers,
                OsnapType.Center,
                OsnapSource.Dxf,
                cancellationToken);
            AddPoints(
                points,
                pointKeys,
                quadrants,
                OsnapType.Quadrant,
                OsnapSource.Dxf,
                cancellationToken);

            List<OsnapSegmentCandidate> indexedSegments =
                BuildSegments(segments, OsnapSource.Dxf, cancellationToken);
            return Create(
                OsnapSource.Dxf,
                points,
                indexedSegments,
                cancellationToken);
        }

        public static OsnapGeometryIndex BuildIfc(
            IReadOnlyList<IfcSnapGeometry> geometry,
            CancellationToken cancellationToken)
        {
            var byElementId = new Dictionary<string, IfcSnapGeometry>(
                StringComparer.Ordinal);
            for (int index = 0; index < geometry.Count; index++)
            {
                if ((index & 127) == 0)
                    cancellationToken.ThrowIfCancellationRequested();

                IfcSnapGeometry item = geometry[index];
                if (!string.IsNullOrWhiteSpace(item.ElementId))
                    byElementId[item.ElementId] = item;
            }

            var points = new List<OsnapPointCandidate>();
            var segments = new List<(Point3D A, Point3D B)>();
            var pointKeys = new HashSet<PointKey>();
            foreach (IfcSnapGeometry item in byElementId.Values)
            {
                cancellationToken.ThrowIfCancellationRequested();
                AddPoints(
                    points,
                    pointKeys,
                    item.Vertices,
                    OsnapType.Endpoint,
                    OsnapSource.Ifc,
                    cancellationToken);
                AddMidpoints(
                    points,
                    pointKeys,
                    item.Segments,
                    OsnapSource.Ifc,
                    cancellationToken);
                for (int index = 0; index < item.Segments.Count; index++)
                    segments.Add(item.Segments[index]);
            }

            List<OsnapSegmentCandidate> indexedSegments =
                BuildSegments(segments, OsnapSource.Ifc, cancellationToken);
            return Create(
                OsnapSource.Ifc,
                points,
                indexedSegments,
                cancellationToken);
        }

        public static OsnapGeometryIndex BuildZones(
            IReadOnlyList<OsnapZoneGeometry> polygons,
            CancellationToken cancellationToken)
        {
            var points = new List<OsnapPointCandidate>();
            var segments = new List<(Point3D A, Point3D B)>();
            var pointKeys = new HashSet<PointKey>();
            for (int polygonIndex = 0;
                 polygonIndex < polygons.Count;
                 polygonIndex++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                OsnapZoneGeometry polygon = polygons[polygonIndex];
                AddPoints(
                    points,
                    pointKeys,
                    polygon.Vertices,
                    OsnapType.Endpoint,
                    OsnapSource.Zone,
                    cancellationToken);

                int edgeCount = polygon.IsClosed
                    ? polygon.Vertices.Count
                    : Math.Max(0, polygon.Vertices.Count - 1);
                for (int edgeIndex = 0; edgeIndex < edgeCount; edgeIndex++)
                {
                    Point3D start = polygon.Vertices[edgeIndex];
                    Point3D end =
                        polygon.Vertices[(edgeIndex + 1) % polygon.Vertices.Count];
                    if (IsFinite(start) &&
                        IsFinite(end) &&
                        !SamePoint(start, end))
                    {
                        segments.Add((start, end));
                    }
                }
            }

            AddMidpoints(
                points,
                pointKeys,
                segments,
                OsnapSource.Zone,
                cancellationToken);
            List<OsnapSegmentCandidate> indexedSegments =
                BuildSegments(segments, OsnapSource.Zone, cancellationToken);
            return Create(
                OsnapSource.Zone,
                points,
                indexedSegments,
                cancellationToken);
        }

        public IReadOnlyList<OsnapPointCandidate> QueryPoints(
            OsnapViewportSnapshot snapshot,
            OsnapRay ray,
            int maxResults,
            CancellationToken cancellationToken)
        {
            return _points.QueryNearRay(
                snapshot,
                ray,
                snapshot.SnapRadiusPx,
                maxResults,
                point => IsEnabled(snapshot.EnabledModes, point.Type),
                (point, unusedRay, camera) =>
                {
                    if (!OsnapProjectionMath.TryProject(
                            camera,
                            point.World,
                            out System.Windows.Point screen,
                            out _))
                    {
                        return double.PositiveInfinity;
                    }

                    double distanceSquared =
                        OsnapProjectionMath.PixelDistanceSquared(
                            camera.CursorScreenPoint,
                            screen);
                    double radius = camera.SnapRadiusPx * 1.2;
                    return distanceSquared <= radius * radius
                        ? Math.Sqrt(distanceSquared)
                        : double.PositiveInfinity;
                },
                cancellationToken);
        }

        public IReadOnlyList<OsnapSegmentCandidate> QuerySegments(
            OsnapViewportSnapshot snapshot,
            OsnapRay ray,
            int maxResults,
            CancellationToken cancellationToken)
        {
            return _segments.QueryNearRay(
                snapshot,
                ray,
                Math.Max(
                    snapshot.SnapRadiusPx * 2.5,
                    snapshot.SnapRadiusPx + 24.0),
                maxResults,
                static _ => true,
                (segment, unusedRay, camera) =>
                {
                    if (!OsnapProjectionMath.TryProject(
                            camera,
                            segment.A,
                            out System.Windows.Point screenA,
                            out _) ||
                        !OsnapProjectionMath.TryProject(
                            camera,
                            segment.B,
                            out System.Windows.Point screenB,
                            out _))
                    {
                        return double.PositiveInfinity;
                    }

                    double distance =
                        OsnapProjectionMath.DistanceToScreenSegment(
                            camera.CursorScreenPoint,
                            screenA,
                            screenB);
                    double radius = Math.Max(
                        camera.SnapRadiusPx * 2.5,
                        camera.SnapRadiusPx + 24.0);
                    return distance <= radius * 1.1
                        ? distance
                        : double.PositiveInfinity;
                },
                cancellationToken);
        }

        private static OsnapGeometryIndex Create(
            OsnapSource source,
            IReadOnlyList<OsnapPointCandidate> points,
            IReadOnlyList<OsnapSegmentCandidate> segments,
            CancellationToken cancellationToken)
        {
            OsnapSpatialIndex<OsnapPointCandidate> pointIndex =
                OsnapSpatialIndex<OsnapPointCandidate>.Build(
                    points,
                    static point =>
                        new OsnapWorldBounds(point.World, point.World),
                    cancellationToken);
            OsnapSpatialIndex<OsnapSegmentCandidate> segmentIndex =
                OsnapSpatialIndex<OsnapSegmentCandidate>.Build(
                    segments,
                    static segment =>
                        OsnapWorldBounds.Empty
                            .Include(segment.A)
                            .Include(segment.B),
                    cancellationToken);
            return new OsnapGeometryIndex(source, pointIndex, segmentIndex);
        }

        private static void AddPoints(
            List<OsnapPointCandidate> sink,
            HashSet<PointKey> keys,
            IReadOnlyList<Point3D> source,
            OsnapType type,
            OsnapSource osnapSource,
            CancellationToken cancellationToken)
        {
            for (int index = 0; index < source.Count; index++)
            {
                if ((index & 255) == 0)
                    cancellationToken.ThrowIfCancellationRequested();

                Point3D point = source[index];
                if (!IsFinite(point))
                    continue;

                var key = new PointKey(type, point);
                if (keys.Add(key))
                    sink.Add(new OsnapPointCandidate(point, type, osnapSource));
            }
        }

        private static void AddMidpoints(
            List<OsnapPointCandidate> sink,
            HashSet<PointKey> keys,
            IReadOnlyList<(Point3D A, Point3D B)> source,
            OsnapSource osnapSource,
            CancellationToken cancellationToken)
        {
            for (int index = 0; index < source.Count; index++)
            {
                if ((index & 255) == 0)
                    cancellationToken.ThrowIfCancellationRequested();

                (Point3D A, Point3D B) segment = source[index];
                if (!IsFinite(segment.A) || !IsFinite(segment.B))
                    continue;

                Point3D midpoint = new(
                    segment.A.X / 2.0 + segment.B.X / 2.0,
                    segment.A.Y / 2.0 + segment.B.Y / 2.0,
                    segment.A.Z / 2.0 + segment.B.Z / 2.0);
                if (!IsFinite(midpoint))
                    continue;

                var key = new PointKey(OsnapType.Midpoint, midpoint);
                if (keys.Add(key))
                {
                    sink.Add(new OsnapPointCandidate(
                        midpoint,
                        OsnapType.Midpoint,
                        osnapSource));
                }
            }
        }

        private static List<OsnapSegmentCandidate> BuildSegments(
            IReadOnlyList<(Point3D A, Point3D B)> source,
            OsnapSource osnapSource,
            CancellationToken cancellationToken)
        {
            var result = new List<OsnapSegmentCandidate>(source.Count);
            var keys = new HashSet<SegmentKey>();
            for (int index = 0; index < source.Count; index++)
            {
                if ((index & 255) == 0)
                    cancellationToken.ThrowIfCancellationRequested();

                (Point3D A, Point3D B) segment = source[index];
                if (!IsFinite(segment.A) ||
                    !IsFinite(segment.B) ||
                    SamePoint(segment.A, segment.B))
                {
                    continue;
                }

                SegmentKey key = SegmentKey.Create(
                    segment.A,
                    segment.B);
                if (!keys.Add(key))
                    continue;

                result.Add(new OsnapSegmentCandidate(
                    Interlocked.Increment(ref _nextSegmentId),
                    segment.A,
                    segment.B,
                    osnapSource));
            }

            return result;
        }

        private static bool IsEnabled(
            OsnapModeMask enabled,
            OsnapType type)
        {
            OsnapModeMask mode = type switch
            {
                OsnapType.Endpoint => OsnapModeMask.Endpoint,
                OsnapType.Intersection => OsnapModeMask.Intersection,
                OsnapType.Midpoint => OsnapModeMask.Midpoint,
                OsnapType.Center => OsnapModeMask.Center,
                OsnapType.Quadrant => OsnapModeMask.Quadrant,
                _ => OsnapModeMask.None
            };
            return (enabled & mode) != 0;
        }

        private static bool SamePoint(Point3D first, Point3D second)
        {
            const double tolerance = 1e-9;
            return Math.Abs(first.X - second.X) <= tolerance &&
                   Math.Abs(first.Y - second.Y) <= tolerance &&
                   Math.Abs(first.Z - second.Z) <= tolerance;
        }

        private static bool IsFinite(Point3D point) =>
            double.IsFinite(point.X) &&
            double.IsFinite(point.Y) &&
            double.IsFinite(point.Z);

        private readonly record struct PointKey(
            OsnapType Type,
            long X,
            long Y,
            long Z)
        {
            public PointKey(OsnapType type, Point3D point)
                : this(
                    type,
                    Quantize(point.X),
                    Quantize(point.Y),
                    Quantize(point.Z))
            {
            }
        }

        private readonly record struct CoordinateKey(
            long X,
            long Y,
            long Z) : IComparable<CoordinateKey>
        {
            public CoordinateKey(Point3D point)
                : this(
                    Quantize(point.X),
                    Quantize(point.Y),
                    Quantize(point.Z))
            {
            }

            public int CompareTo(CoordinateKey other)
            {
                int x = X.CompareTo(other.X);
                if (x != 0)
                    return x;

                int y = Y.CompareTo(other.Y);
                return y != 0
                    ? y
                    : Z.CompareTo(other.Z);
            }
        }

        private readonly record struct SegmentKey(
            CoordinateKey A,
            CoordinateKey B)
        {
            public static SegmentKey Create(
                Point3D first,
                Point3D second)
            {
                var firstKey = new CoordinateKey(first);
                var secondKey = new CoordinateKey(second);
                return firstKey.CompareTo(secondKey) <= 0
                    ? new SegmentKey(firstKey, secondKey)
                    : new SegmentKey(secondKey, firstKey);
            }
        }

        private static long Quantize(double value)
        {
            double scaled = Math.Round(value * 1_000_000.0);
            if (scaled >= long.MaxValue)
                return long.MaxValue;
            if (scaled <= long.MinValue)
                return long.MinValue;
            return (long)scaled;
        }
    }
}
