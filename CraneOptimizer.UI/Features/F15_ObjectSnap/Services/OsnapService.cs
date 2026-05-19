using CraneOptimizer.UI.Features.F15_ObjectSnap.Models;
using HelixToolkit.Wpf;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Media.Media3D;

namespace CraneOptimizer.UI.Features.F15_ObjectSnap.Services
{
    public sealed class OsnapService : IOsnapService
    {
        public OsnapSettings Settings { get; }

        private List<Point3D> _zoneVertices = new();
        private List<(Point3D A, Point3D B)> _zoneEdges = new();

        private List<Point3D> _dxfEndpoints = new();
        private List<(Point3D A, Point3D B)> _dxfSegments = new();
        private List<(Point3D A, Point3D B)> _dxfMidpointSegments = new();
        private List<Point3D> _dxfCenters = new();
        private List<Point3D> _dxfQuadrants = new();
        private DxfSnapScreenIndex? _dxfScreenIndex;
        private int _dxfGeometryVersion;

        private List<Point3D> _ifcVertices = new();
        private List<(Point3D A, Point3D B)> _ifcSegments = new();
        private Dictionary<string, IfcSnapGeometry> _ifcGeometryByElementId = new(StringComparer.OrdinalIgnoreCase);
        private IfcSnapScreenIndex? _ifcScreenIndex;
        private int _ifcGeometryVersion;

        private int _snapCallCount;

        public OsnapService(OsnapSettings settings)
            => Settings = settings ?? throw new ArgumentNullException(nameof(settings));

        public void SetMasterEnabled(bool enabled) => Settings.MasterEnabled = enabled;

        public void SetSnapEnabled(OsnapType type, bool enabled)
        {
            switch (type)
            {
                case OsnapType.Endpoint: Settings.SnapEndpoint = enabled; break;
                case OsnapType.Midpoint: Settings.SnapMidpoint = enabled; break;
                case OsnapType.Intersection: Settings.SnapIntersection = enabled; break;
                case OsnapType.Perpendicular: Settings.SnapPerpendicular = false; break;
                case OsnapType.Nearest: Settings.SnapNearest = false; break;
                case OsnapType.Grid: Settings.SnapGrid = false; break;
                case OsnapType.Center: Settings.SnapCenter = enabled; break;
                case OsnapType.Quadrant: Settings.SnapQuadrant = enabled; break;
            }
        }

        public void SetZoneGeometry(IEnumerable<(IReadOnlyList<Point3D> Vertices, bool IsClosed)> polygons)
        {
            _zoneVertices.Clear();
            _zoneEdges.Clear();

            foreach (var (vertices, isClosed) in polygons)
            {
                if (vertices.Count == 0)
                    continue;

                _zoneVertices.AddRange(vertices);
                int edgeCount = isClosed ? vertices.Count : vertices.Count - 1;
                for (int i = 0; i < edgeCount; i++)
                    _zoneEdges.Add((vertices[i], vertices[(i + 1) % vertices.Count]));
            }
        }

        public void SetDxfSnapPoints(
            IEnumerable<Point3D> endpoints,
            IEnumerable<(Point3D A, Point3D B)> segments,
            IEnumerable<Point3D>? centers = null,
            IEnumerable<Point3D>? quadrants = null,
            IEnumerable<(Point3D A, Point3D B)>? midpointSegments = null)
        {
            _dxfEndpoints = endpoints?.Where(IsFinite).ToList() ?? new();
            _dxfSegments = segments?.Where(s => IsFinite(s.A) && IsFinite(s.B)).ToList() ?? new();
            _dxfMidpointSegments = midpointSegments?.Where(s => IsFinite(s.A) && IsFinite(s.B)).ToList()
                                   ?? _dxfSegments;
            _dxfCenters = centers?.Where(IsFinite).ToList() ?? new();
            _dxfQuadrants = quadrants?.Where(IsFinite).ToList() ?? new();
            _dxfGeometryVersion++;
            _dxfScreenIndex = null;

            Debug.WriteLine($"[OSnap] SetDxfSnapPoints: endpoints={_dxfEndpoints.Count}, segments={_dxfSegments.Count}");
        }

        public void SetIfcSnapPoints(
            IEnumerable<Point3D> vertices,
            IEnumerable<(Point3D A, Point3D B)>? segments = null)
        {
            _ifcVertices = vertices?.Where(IsFinite).ToList() ?? new();
            _ifcSegments = segments?.Where(s => IsFinite(s.A) && IsFinite(s.B)).ToList() ?? new();
            _ifcGeometryByElementId.Clear();
            _ifcGeometryVersion++;
            _ifcScreenIndex = null;

            Debug.WriteLine($"[OSnap] SetIfcSnapPoints: vertices={_ifcVertices.Count}, segments={_ifcSegments.Count}");
        }

        public void SetIfcSnapGeometry(IEnumerable<IfcSnapGeometry> geometry)
        {
            var items = geometry?.Where(g => !string.IsNullOrWhiteSpace(g.ElementId)).ToList() ?? new();
            _ifcGeometryByElementId = items
                .GroupBy(g => g.ElementId, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(group => group.Key, group => group.Last(), StringComparer.OrdinalIgnoreCase);

            _ifcVertices = items.SelectMany(g => g.Vertices).Where(IsFinite).ToList();
            _ifcSegments = items.SelectMany(g => g.Segments).Where(s => IsFinite(s.A) && IsFinite(s.B)).ToList();
            _ifcGeometryVersion++;
            _ifcScreenIndex = null;

            Debug.WriteLine(
                $"[OSnap] SetIfcSnapGeometry: elements={_ifcGeometryByElementId.Count}, " +
                $"vertices={_ifcVertices.Count}, segments={_ifcSegments.Count}");
        }

        public OsnapResult? Snap(Point screenCursor, HelixViewport3D viewport)
        {
            if (!Settings.MasterEnabled)
                return null;

            var enabled = Settings.EnabledTypes;
            if (enabled.Count == 0)
                return null;

            var snap = new SnapAccumulator(screenCursor, viewport, Settings.SnapRadiusPx);
            bool logThisFrame = ShouldLogFrame(screenCursor);
            var dxfIndex = Settings.SnapToDxf ? GetDxfScreenIndex(viewport) : null;
            var ifcIndex = Settings.SnapToIfc ? GetIfcScreenIndex(viewport) : null;

            if (enabled.Contains(OsnapType.Endpoint))
            {
                foreach (var vertex in _zoneVertices)
                    snap.TryVertex(vertex, OsnapSource.Zone, OsnapType.Endpoint);

                if (dxfIndex != null)
                    DxfSnapResolver.TryEndpoints(snap, dxfIndex);

                if (ifcIndex != null)
                    IfcSnapResolver.TryEndpoints(snap, ifcIndex);
            }

            if (enabled.Contains(OsnapType.Midpoint))
            {
                foreach (var (a, b) in _zoneEdges)
                    snap.TryVertex(MidPoint(a, b), OsnapSource.Zone, OsnapType.Midpoint);

                if (dxfIndex != null)
                    DxfSnapResolver.TryMidpoints(snap, dxfIndex);

                if (ifcIndex != null)
                    IfcSnapResolver.TryMidpoints(snap, ifcIndex);
            }

            if (enabled.Contains(OsnapType.Center) && dxfIndex != null)
                DxfSnapResolver.TryCenters(snap, dxfIndex);

            if (enabled.Contains(OsnapType.Quadrant) && dxfIndex != null)
                DxfSnapResolver.TryQuadrants(snap, dxfIndex);

            if (enabled.Contains(OsnapType.Intersection))
                TryIntersections(snap, viewport, dxfIndex);

            var result = snap.ToResult();
            if (logThisFrame)
            {
                Debug.WriteLine(result == null
                    ? "[OSnap]   Result: NO MATCH"
                    : $"[OSnap]   Result: {result.SnapType} source={result.Source} dist={snap.BestDistance:F1}px");
            }

            return result;
        }

        private DxfSnapScreenIndex? GetDxfScreenIndex(HelixViewport3D viewport)
        {
            if (_dxfEndpoints.Count == 0 &&
                _dxfSegments.Count == 0 &&
                _dxfCenters.Count == 0 &&
                _dxfQuadrants.Count == 0)
            {
                return null;
            }

            string key = DxfSnapScreenIndex.BuildKey(viewport, _dxfGeometryVersion);
            if (_dxfScreenIndex != null && string.Equals(_dxfScreenIndex.Key, key, StringComparison.Ordinal))
                return _dxfScreenIndex;

            _dxfScreenIndex = DxfSnapScreenIndex.Build(
                viewport,
                _dxfEndpoints,
                _dxfSegments,
                _dxfMidpointSegments,
                _dxfCenters,
                _dxfQuadrants,
                _dxfGeometryVersion);
            return _dxfScreenIndex;
        }

        private IfcSnapScreenIndex? GetIfcScreenIndex(HelixViewport3D viewport)
        {
            if (_ifcGeometryByElementId.Count == 0)
                return null;

            string key = IfcSnapScreenIndex.BuildKey(viewport, _ifcGeometryVersion);
            if (_ifcScreenIndex != null && string.Equals(_ifcScreenIndex.Key, key, StringComparison.Ordinal))
                return _ifcScreenIndex;

            _ifcScreenIndex = IfcSnapScreenIndex.Build(viewport, _ifcGeometryByElementId, _ifcGeometryVersion);
            return _ifcScreenIndex;
        }

        private bool ShouldLogFrame(Point screenCursor)
        {
            _snapCallCount++;
            bool logThisFrame = (_snapCallCount % 200) == 1;
            if (!logThisFrame)
                return false;

            Debug.WriteLine(
                $"[OSnap] cursor=({screenCursor.X:F0},{screenCursor.Y:F0}) " +
                $"dxf={_dxfEndpoints.Count}/{_dxfSegments.Count} " +
                $"ifc={_ifcVertices.Count}/{_ifcSegments.Count} R={Settings.SnapRadiusPx}");

            return true;
        }

        private void TryIntersections(
            SnapAccumulator snap,
            HelixViewport3D viewport,
            DxfSnapScreenIndex? dxfIndex)
        {
            var candidates = new List<ProjectedSegment>();
            double intersectionSearchRadius = Math.Max(Settings.SnapRadiusPx * 2.5, Settings.SnapRadiusPx + 24.0);
            AddProjectedSegments(candidates, viewport, snap, _zoneEdges, OsnapSource.Zone, intersectionSearchRadius);

            if (dxfIndex != null)
                AddDxfProjectedSegments(candidates, snap, dxfIndex, intersectionSearchRadius);

            if (candidates.Count < 2)
                return;

            if (candidates.Count > Settings.MaxEdgesForIntersection)
            {
                candidates = candidates
                    .OrderBy(segment => DistanceToScreenSegment(snap.ScreenCursor, segment.ScreenA, segment.ScreenB))
                    .Take(Settings.MaxEdgesForIntersection)
                    .ToList();
            }

            for (int i = 0; i < candidates.Count; i++)
            {
                for (int j = i + 1; j < candidates.Count; j++)
                {
                    if (ShareEndpoint(candidates[i], candidates[j]))
                        continue;

                    var source = ResolveIntersectionSource(candidates[i].Source, candidates[j].Source);
                    if (TryScreenIntersection(candidates[i], candidates[j], out var screenIntersection, out double tOnFirst))
                    {
                        var screenWorld = Lerp(candidates[i].WorldA, candidates[i].WorldB, tOnFirst);
                        snap.TryKnownScreenPoint(screenWorld, screenIntersection, source, OsnapType.Intersection);
                    }

                    if (!TryWorldIntersection(candidates[i], candidates[j], out var world))
                        continue;

                    if (!ProjectForSource(viewport, source, world, out var screen))
                        continue;

                    snap.TryKnownScreenPoint(world, screen, source, OsnapType.Intersection);
                }
            }
        }

        private static bool ShareEndpoint(ProjectedSegment a, ProjectedSegment b)
            => SamePoint(a.WorldA, b.WorldA) ||
               SamePoint(a.WorldA, b.WorldB) ||
               SamePoint(a.WorldB, b.WorldA) ||
               SamePoint(a.WorldB, b.WorldB);

        private static bool SamePoint(Point3D a, Point3D b)
        {
            const double tolerance = 0.001;
            return Math.Abs(a.X - b.X) <= tolerance &&
                   Math.Abs(a.Y - b.Y) <= tolerance &&
                   Math.Abs(a.Z - b.Z) <= tolerance;
        }

        private static void AddDxfProjectedSegments(
            List<ProjectedSegment> sink,
            SnapAccumulator snap,
            DxfSnapScreenIndex index,
            double radius)
        {
            foreach (var segment in index.QuerySegments(snap.ScreenCursor, radius))
            {
                sink.Add(new ProjectedSegment(
                    segment.WorldA,
                    segment.WorldB,
                    segment.ScreenA,
                    segment.ScreenB,
                    OsnapSource.Dxf));
            }
        }

        private void AddProjectedSegments(
            List<ProjectedSegment> sink,
            HelixViewport3D viewport,
            SnapAccumulator snap,
            IEnumerable<(Point3D A, Point3D B)> segments,
            OsnapSource source,
            double radius)
        {
            foreach (var (a, b) in segments)
            {
                if (!ProjectForSource(viewport, source, a, out var sa) ||
                    !ProjectForSource(viewport, source, b, out var sb))
                {
                    continue;
                }

                if (!ScreenBoundsTouchesCursor(sa, sb, snap.ScreenCursor, radius))
                    continue;

                if (DistanceToScreenSegment(snap.ScreenCursor, sa, sb) > radius)
                    continue;

                sink.Add(new ProjectedSegment(a, b, sa, sb, source));
            }
        }

        private static bool ProjectForSource(
            HelixViewport3D viewport,
            OsnapSource source,
            Point3D world,
            out Point screen)
            => OsnapProjection.TryProject(viewport, world, out screen);

        private static bool ScreenBoundsTouchesCursor(Point a, Point b, Point cursor, double radius)
        {
            double minX = Math.Min(a.X, b.X) - radius;
            double maxX = Math.Max(a.X, b.X) + radius;
            double minY = Math.Min(a.Y, b.Y) - radius;
            double maxY = Math.Max(a.Y, b.Y) + radius;
            return cursor.X >= minX && cursor.X <= maxX && cursor.Y >= minY && cursor.Y <= maxY;
        }

        private static double DistanceToScreenSegment(Point p, Point a, Point b)
        {
            double dx = b.X - a.X;
            double dy = b.Y - a.Y;
            double lenSq = dx * dx + dy * dy;
            if (lenSq < 1e-6)
                return SnapAccumulator.PixelDistance(p, a);

            double t = Math.Clamp(((p.X - a.X) * dx + (p.Y - a.Y) * dy) / lenSq, 0, 1);
            var closest = new Point(a.X + t * dx, a.Y + t * dy);
            return SnapAccumulator.PixelDistance(p, closest);
        }

        private static bool TryWorldIntersection(
            ProjectedSegment first,
            ProjectedSegment second,
            out Point3D world)
        {
            world = default;

            double x1 = first.WorldA.X;
            double y1 = first.WorldA.Y;
            double x2 = first.WorldB.X;
            double y2 = first.WorldB.Y;
            double x3 = second.WorldA.X;
            double y3 = second.WorldA.Y;
            double x4 = second.WorldB.X;
            double y4 = second.WorldB.Y;

            double d = (x1 - x2) * (y3 - y4) - (y1 - y2) * (x3 - x4);
            if (Math.Abs(d) < 1e-9)
                return false;

            double t = ((x1 - x3) * (y3 - y4) - (y1 - y3) * (x3 - x4)) / d;
            double u = ((x1 - x3) * (y1 - y2) - (y1 - y3) * (x1 - x2)) / d;

            const double epsilon = 1e-9;
            if (t < -epsilon || t > 1 + epsilon || u < -epsilon || u > 1 + epsilon)
                return false;

            t = Math.Clamp(t, 0.0, 1.0);
            world = Lerp(first.WorldA, first.WorldB, t);
            return true;
        }

        private static bool TryScreenIntersection(
            ProjectedSegment first,
            ProjectedSegment second,
            out Point screen,
            out double tOnFirst)
        {
            screen = default;
            tOnFirst = default;

            double x1 = first.ScreenA.X;
            double y1 = first.ScreenA.Y;
            double x2 = first.ScreenB.X;
            double y2 = first.ScreenB.Y;
            double x3 = second.ScreenA.X;
            double y3 = second.ScreenA.Y;
            double x4 = second.ScreenB.X;
            double y4 = second.ScreenB.Y;

            double d = (x1 - x2) * (y3 - y4) - (y1 - y2) * (x3 - x4);
            if (Math.Abs(d) < 1e-6)
                return false;

            double t = ((x1 - x3) * (y3 - y4) - (y1 - y3) * (x3 - x4)) / d;
            double u = ((x1 - x3) * (y1 - y2) - (y1 - y3) * (x1 - x2)) / d;
            const double endpointTolerance = 0.02;
            if (t < -endpointTolerance || t > 1 + endpointTolerance ||
                u < -endpointTolerance || u > 1 + endpointTolerance)
                return false;

            t = Math.Clamp(t, 0.0, 1.0);
            screen = new Point(x1 + t * (x2 - x1), y1 + t * (y2 - y1));
            tOnFirst = t;
            return true;
        }

        private static OsnapSource ResolveIntersectionSource(OsnapSource a, OsnapSource b)
        {
            if (a == b)
                return a;

            if (a == OsnapSource.Ifc || b == OsnapSource.Ifc)
                return OsnapSource.Ifc;

            if (a == OsnapSource.Dxf || b == OsnapSource.Dxf)
                return OsnapSource.Dxf;

            return OsnapSource.Zone;
        }

        private void TryGridSnap(SnapAccumulator snap, HelixViewport3D viewport, Point screenCursor)
        {
            if (!OsnapProjection.TryUnprojectZ0(viewport, screenCursor, out var worldPt))
                return;

            double spacing = Settings.GridSpacing;
            if (spacing <= 0)
                return;

            double gx = Math.Round(worldPt.X / spacing) * spacing;
            double gy = Math.Round(worldPt.Y / spacing) * spacing;
            snap.TryVertex(new Point3D(gx, gy, 0), OsnapSource.Grid, OsnapType.Grid);
        }

        private static Point3D MidPoint(Point3D a, Point3D b)
            => new((a.X + b.X) / 2.0, (a.Y + b.Y) / 2.0, (a.Z + b.Z) / 2.0);

        private static Point3D Lerp(Point3D a, Point3D b, double t)
            => new(
                a.X + t * (b.X - a.X),
                a.Y + t * (b.Y - a.Y),
                a.Z + t * (b.Z - a.Z));

        private static bool IsFinite(Point3D point)
            => double.IsFinite(point.X) && double.IsFinite(point.Y) && double.IsFinite(point.Z);

        private readonly record struct ProjectedSegment(
            Point3D WorldA,
            Point3D WorldB,
            Point ScreenA,
            Point ScreenB,
            OsnapSource Source);
    }
}
