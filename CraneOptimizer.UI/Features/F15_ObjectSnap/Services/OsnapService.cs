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

        private List<Point3D>              _zoneVertices  = new();
        private List<(Point3D A, Point3D B)> _zoneEdges   = new();
        private List<Point3D>              _dxfEndpoints  = new();
        private List<(Point3D A, Point3D B)> _dxfSegments  = new();
        private List<Point3D>              _dxfCenters    = new();
        private List<Point3D>              _dxfQuadrants  = new();
        private List<Point3D>              _ifcVertices   = new();
        private int _snapCallCount;

        public OsnapService(OsnapSettings settings)
            => Settings = settings ?? throw new ArgumentNullException(nameof(settings));

        // ── Settings ──────────────────────────────────────────────────────────
        public void SetMasterEnabled(bool enabled) => Settings.MasterEnabled = enabled;
        public void SetSnapEnabled(OsnapType type, bool enabled)
        {
            switch (type)
            {
                case OsnapType.Endpoint:      Settings.SnapEndpoint      = enabled; break;
                case OsnapType.Midpoint:      Settings.SnapMidpoint      = enabled; break;
                case OsnapType.Intersection:  Settings.SnapIntersection  = enabled; break;
                case OsnapType.Perpendicular: Settings.SnapPerpendicular = enabled; break;
                case OsnapType.Nearest:       Settings.SnapNearest       = enabled; break;
                case OsnapType.Grid:          Settings.SnapGrid          = enabled; break;
                case OsnapType.Center:        Settings.SnapCenter        = enabled; break;
                case OsnapType.Quadrant:      Settings.SnapQuadrant      = enabled; break;
            }
        }

        // ── Geometry feeds ─────────────────────────────────────────────────────
        public void SetZoneGeometry(IEnumerable<(IReadOnlyList<Point3D> Vertices, bool IsClosed)> polygons)
        {
            _zoneVertices.Clear(); _zoneEdges.Clear();
            foreach (var (verts, closed) in polygons)
            {
                _zoneVertices.AddRange(verts);
                int n = verts.Count, edges = closed ? n : n - 1;
                for (int i = 0; i < edges; i++)
                    _zoneEdges.Add((verts[i], verts[(i + 1) % n]));
            }
        }

        public void SetDxfSnapPoints(IEnumerable<Point3D> endpoints,
                                     IEnumerable<(Point3D A, Point3D B)> segments,
                                     IEnumerable<Point3D>? centers = null,
                                     IEnumerable<Point3D>? quadrants = null)
        {
            _dxfEndpoints  = endpoints?.ToList()  ?? new();
            _dxfSegments   = segments?.ToList()   ?? new();
            _dxfCenters    = centers?.ToList()     ?? new();
            _dxfQuadrants  = quadrants?.ToList()   ?? new();
            Debug.WriteLine($"[OSnap] SetDxfSnapPoints: endpoints={_dxfEndpoints.Count}, segments={_dxfSegments.Count}");
        }

        public void SetIfcSnapPoints(IEnumerable<Point3D> vertices)
        {
            _ifcVertices = vertices?.ToList() ?? new();
            Debug.WriteLine($"[OSnap] SetIfcSnapPoints: vertices={_ifcVertices.Count}");
        }

        // =====================================================================
        //  CORE SNAP — Custom camera-math projection (no WPF/HelixToolkit helpers)
        //
        //  Previous approaches using Point3DtoPoint2D, TranslatePoint, and
        //  TransformToAncestor all had coordinate system mismatches.
        //  This implementation computes projection from raw camera parameters,
        //  producing pixel coordinates in the same space as e.GetPosition(viewport).
        // =====================================================================
        public OsnapResult? Snap(
            Point screenCursor,           // e.GetPosition(viewport) – HelixViewport3D pixels
            HelixViewport3D viewport)
        {
            if (!Settings.MasterEnabled) return null;
            var enabled = Settings.EnabledTypes;
            if (enabled.Count == 0) return null;

            double R = Settings.SnapRadiusPx;

            // ── Projection (world → HelixViewport3D pixel coordinates) ──────────
            // Use WPF/Helix projection so FieldOfView/viewport transforms match exactly.
            // Then translate from the inner Viewport3D space to HelixViewport3D space.
            Point? ToScreen(Point3D world)
            {
                try
                {
                    var inner = Viewport3DHelper.Point3DtoPoint2D(viewport.Viewport, world);
                    if (double.IsNaN(inner.X) || double.IsNaN(inner.Y)
                        || double.IsInfinity(inner.X) || double.IsInfinity(inner.Y))
                        return null;

                    // inner is in viewport.Viewport coordinate space; translate to HelixViewport3D space
                    var helixPt = viewport.Viewport.TranslatePoint(inner, viewport);
                    return helixPt;
                }
                catch
                {
                    return null;
                }
            }

            // ── Pixel distance helper ──────────────────────────────────────
            static double PxDist(Point a, Point b)
            {
                double dx = a.X - b.X, dy = a.Y - b.Y;
                return Math.Sqrt(dx * dx + dy * dy);
            }

            // ── State ──────────────────────────────────────────────────────
            Point3D?    bestWorld  = null;
            Point?      bestScreen = null;
            double      bestDist   = double.MaxValue;
            OsnapType   bestType   = OsnapType.Endpoint;
            OsnapSource bestSrc    = OsnapSource.Zone;

            // ── Diagnostics (every ~200 frames) ────────────────────────────
            _snapCallCount++;
            bool logThisFrame = (_snapCallCount % 200) == 1;
            if (logThisFrame)
            {
                Debug.WriteLine($"[OSnap] cursor=({screenCursor.X:F0},{screenCursor.Y:F0}) " +
                    $"dxf={_dxfEndpoints.Count} ifc={_ifcVertices.Count} R={R}");
                if (_dxfEndpoints.Count > 0)
                {
                    var ts = ToScreen(_dxfEndpoints[0]);
                    if (ts.HasValue)
                        Debug.WriteLine($"[OSnap]   DXF[0] w=({_dxfEndpoints[0].X:F1},{_dxfEndpoints[0].Y:F1},{_dxfEndpoints[0].Z:F2}) s=({ts.Value.X:F0},{ts.Value.Y:F0})");
                }
                if (_ifcVertices.Count > 0)
                {
                    var ts = ToScreen(_ifcVertices[0]);
                    if (ts.HasValue)
                        Debug.WriteLine($"[OSnap]   IFC[0] w=({_ifcVertices[0].X:F1},{_ifcVertices[0].Y:F1},{_ifcVertices[0].Z:F2}) s=({ts.Value.X:F0},{ts.Value.Y:F0})");
                }
            }

            // Try a single world vertex as snap candidate
            void TryVertex(Point3D w, OsnapSource src, OsnapType type = OsnapType.Endpoint)
            {
                var s = ToScreen(w);
                if (s == null) return;
                double d = PxDist(screenCursor, s.Value);
                if (d < R && d < bestDist)
                { bestWorld = w; bestScreen = s; bestDist = d; bestType = type; bestSrc = src; }
            }

            // Try closest point on a world-space segment
            void TryEdge(Point3D wa, Point3D wb, OsnapSource src, OsnapType type)
            {
                var sa = ToScreen(wa);
                var sb = ToScreen(wb);
                if (sa == null || sb == null) return;

                double dxS = sb.Value.X - sa.Value.X, dyS = sb.Value.Y - sa.Value.Y;
                double lenSq = dxS * dxS + dyS * dyS;
                if (lenSq < 1e-6) { TryVertex(wa, src, type); return; }
                double t = Math.Clamp(
                    ((screenCursor.X - sa.Value.X) * dxS + (screenCursor.Y - sa.Value.Y) * dyS) / lenSq,
                    0, 1);
                var nearSc = new Point(sa.Value.X + t * dxS, sa.Value.Y + t * dyS);
                double d = PxDist(screenCursor, nearSc);
                if (d < R && d < bestDist)
                {
                    var nearW = new Point3D(wa.X + t * (wb.X - wa.X),
                                           wa.Y + t * (wb.Y - wa.Y),
                                           wa.Z + t * (wb.Z - wa.Z));
                    bestWorld = nearW; bestScreen = nearSc;
                    bestDist = d; bestType = type; bestSrc = src;
                }
            }

            // ── ENDPOINT ────────────────────────────────────────────────────
            if (enabled.Contains(OsnapType.Endpoint))
            {
                foreach (var v in _zoneVertices)   TryVertex(v, OsnapSource.Zone);
                if (Settings.SnapToDxf)
                    foreach (var v in _dxfEndpoints) TryVertex(v, OsnapSource.Dxf);
                if (Settings.SnapToIfc)
                    foreach (var v in _ifcVertices)  TryVertex(v, OsnapSource.Ifc);
            }

            // Build combined edge list
            var allEdges = _zoneEdges.AsEnumerable();
            if (Settings.SnapToDxf) allEdges = allEdges.Concat(_dxfSegments);
            var edgeList = allEdges.ToList();

            // ── MIDPOINT ────────────────────────────────────────────────────
            if (enabled.Contains(OsnapType.Midpoint))
            {
                int zi = _zoneEdges.Count;
                for (int i = 0; i < edgeList.Count; i++)
                {
                    var (a, b) = edgeList[i];
                    var mid = new Point3D((a.X + b.X) / 2, (a.Y + b.Y) / 2, (a.Z + b.Z) / 2);
                    TryVertex(mid, i < zi ? OsnapSource.Zone : OsnapSource.Dxf, OsnapType.Midpoint);
                }
            }

            // ── NEAREST ─────────────────────────────────────────────────────
            if (enabled.Contains(OsnapType.Nearest))
            {
                int zi = _zoneEdges.Count;
                for (int i = 0; i < edgeList.Count; i++)
                {
                    var (a, b) = edgeList[i];
                    TryEdge(a, b, i < zi ? OsnapSource.Zone : OsnapSource.Dxf, OsnapType.Nearest);
                }
                if (Settings.SnapToIfc)
                    foreach (var v in _ifcVertices) TryVertex(v, OsnapSource.Ifc, OsnapType.Nearest);
            }

            // ── PERPENDICULAR ───────────────────────────────────────────────
            if (enabled.Contains(OsnapType.Perpendicular))
            {
                int zi = _zoneEdges.Count;
                for (int i = 0; i < edgeList.Count; i++)
                {
                    var (a, b) = edgeList[i];
                    var sa = ToScreen(a); var sb = ToScreen(b);
                    if (sa == null || sb == null) continue;
                    double dxS = sb.Value.X - sa.Value.X, dyS = sb.Value.Y - sa.Value.Y;
                    double lenSq = dxS * dxS + dyS * dyS;
                    if (lenSq < 1) continue;
                    double t = ((screenCursor.X - sa.Value.X) * dxS + (screenCursor.Y - sa.Value.Y) * dyS) / lenSq;
                    if (t < 0.05 || t > 0.95) continue;
                    var nearSc = new Point(sa.Value.X + t * dxS, sa.Value.Y + t * dyS);
                    double d = PxDist(screenCursor, nearSc);
                    if (d < R && d < bestDist)
                    {
                        var nearW = new Point3D(a.X + t * (b.X - a.X), a.Y + t * (b.Y - a.Y),
                                               a.Z + t * (b.Z - a.Z));
                        bestWorld = nearW; bestScreen = nearSc;
                        bestDist = d; bestType = OsnapType.Perpendicular;
                        bestSrc = i < zi ? OsnapSource.Zone : OsnapSource.Dxf;
                    }
                }
            }

            // ── INTERSECTION ────────────────────────────────────────────────
            if (enabled.Contains(OsnapType.Intersection)
                && edgeList.Count <= Settings.MaxEdgesForIntersection)
            {
                for (int i = 0; i < edgeList.Count; i++)
                for (int j = i + 1; j < edgeList.Count; j++)
                {
                    if (TryIntersect(edgeList[i], edgeList[j], out var ix))
                    {
                        var src = i < _zoneEdges.Count ? OsnapSource.Zone : OsnapSource.Dxf;
                        TryVertex(ix, src, OsnapType.Intersection);
                    }
                }
            }

            // ── CENTER ──────────────────────────────────────────────────────
            if (enabled.Contains(OsnapType.Center))
            {
                if (Settings.SnapToDxf)
                    foreach (var c in _dxfCenters) TryVertex(c, OsnapSource.Dxf, OsnapType.Center);
            }

            // ── QUADRANT ────────────────────────────────────────────────────
            if (enabled.Contains(OsnapType.Quadrant))
            {
                if (Settings.SnapToDxf)
                    foreach (var q in _dxfQuadrants) TryVertex(q, OsnapSource.Dxf, OsnapType.Quadrant);
            }

            // ── GRID ────────────────────────────────────────────────────────
            if (enabled.Contains(OsnapType.Grid))
            {
                if (TryUnprojectZ0(viewport, screenCursor, out var worldPt))
                {
                    double g = Settings.GridSpacing;
                    double gx = Math.Round(worldPt.X / g) * g;
                    double gy = Math.Round(worldPt.Y / g) * g;
                    TryVertex(new Point3D(gx, gy, 0), OsnapSource.Grid, OsnapType.Grid);
                }
            }

            if (bestWorld == null)
            {
                if (logThisFrame)
                    Debug.WriteLine($"[OSnap]   Result: NO MATCH");
                return null;
            }

            if (logThisFrame)
                Debug.WriteLine($"[OSnap]   Result: {bestType} dist={bestDist:F1}px");
            return new OsnapResult(bestType, bestWorld.Value, bestScreen, bestSrc);
        }

        // ── Geometry helpers ───────────────────────────────────────────────────

        private static bool TryIntersect(
            (Point3D A, Point3D B) e1, (Point3D A, Point3D B) e2, out Point3D result)
        {
            result = default;
            double x1=e1.A.X, y1=e1.A.Y, x2=e1.B.X, y2=e1.B.Y;
            double x3=e2.A.X, y3=e2.A.Y, x4=e2.B.X, y4=e2.B.Y;
            double d = (x1-x2)*(y3-y4) - (y1-y2)*(x3-x4);
            if (Math.Abs(d) < 1e-10) return false;
            double t = ((x1-x3)*(y3-y4) - (y1-y3)*(x3-x4)) / d;
            double u = -((x1-x2)*(y1-y3) - (y1-y2)*(x1-x3)) / d;
            if (t < 0 || t > 1 || u < 0 || u > 1) return false;
            result = new Point3D(x1 + t*(x2-x1), y1 + t*(y2-y1), 0);
            return true;
        }

        private static bool TryUnprojectZ0(HelixViewport3D vp, Point screenPt, out Point3D worldPt)
        {
            worldPt = default;
            try
            {
                // IMPORTANT: Viewport3DHelper expects coordinates in the inner Viewport3D space,
                // but our mouse coordinates come from e.GetPosition(HelixViewport3D).
                // Translate to the inner viewport's coordinate system first.
                Point innerPt;
                try
                {
                    innerPt = vp.TranslatePoint(screenPt, vp.Viewport);
                }
                catch
                {
                    return false;
                }

                bool ok = Viewport3DHelper.Point2DtoPoint3D(
                    vp.Viewport, innerPt, out Point3D near, out Point3D far);
                if (!ok) return false;
                var dir = far - near;
                if (Math.Abs(dir.Z) < 1e-9) return false;
                double t = -near.Z / dir.Z;
                if (t < 0) return false;
                worldPt = near + t * dir;
                return true;
            }
            catch { return false; }
        }
    }
}
