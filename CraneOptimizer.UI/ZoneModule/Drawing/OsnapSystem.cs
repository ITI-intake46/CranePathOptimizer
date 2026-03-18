// FILE: ZoneModule/Drawing/OsnapSystem.cs
// CHANGES vs original:
//   + Intersection snap (INT) — finds where two edges cross
//   + Perpendicular snap (PER) — foot of perpendicular from cursor to edge
//   All 6 OSNAP types from spec §6 are now implemented.

using HelixToolkit.Wpf;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Media.Media3D;
using WPF_IFC_test1.ZoneModule.Models;

namespace WPF_IFC_test1.ZoneModule.Drawing
{
    public enum OsnapType
    {
        Endpoint,      // snap to zone polygon vertices
        Midpoint,      // snap to midpoint of edges
        Intersection,  // snap to edge intersections  ← NEW
        Perpendicular, // snap to perpendicular foot on edge  ← NEW
        Nearest,       // snap to closest point on any edge
        Grid           // snap to configurable grid
    }

    /// <summary>
    /// Object Snap system — all 6 types from spec §6.
    /// Active during Drawing Mode and Dimension Mode.
    /// Snaps to user-drawn zones AND imported DXF geometry.
    /// Priority: Endpoint > Intersection > Perpendicular > Midpoint > Nearest > Grid
    /// Snap radius = 12px screen distance.
    /// </summary>
    public class OsnapSystem
    {
        private readonly ZoneManager _zoneManager;

        // Which snap types are currently enabled (toggled via draw panel)
        public HashSet<OsnapType> EnabledSnaps { get; } = new()
        {
            OsnapType.Endpoint,
            OsnapType.Midpoint,
            OsnapType.Nearest,
            OsnapType.Grid
        };

        public double SnapRadiusPx { get; set; } = 12.0;
        public double GridSpacing { get; set; } = 1.0; // meters

        // Last detected snap — used by drawing handler to show snap indicator
        public OsnapType? LastSnapType { get; private set; }
        public Point3D? LastSnapPoint { get; private set; }

        // External DXF points to snap to (populated by DxfRenderer)
        public List<Point3D> DxfSnapPoints { get; } = new();

        /// <summary>Additional snap points (e.g., IFC mesh vertices near cursor). Populated externally.</summary>
        public List<Point3D> ExtraSnapPoints { get; } = new();

        // ── Master toggle ──────────────────────────────────────────────────
        private HashSet<OsnapType> _savedSnaps;

        /// <summary>True when OSnap is globally enabled. False = all snaps suppressed.</summary>
        public bool MasterEnabled { get; private set; } = true;

        /// <summary>
        /// Master ON/OFF toggle. When disabled, saves current enabled set and clears all.
        /// When re-enabled, restores the previously saved set.
        /// </summary>
        public void SetMasterEnabled(bool enabled)
        {
            if (enabled == MasterEnabled) return;
            MasterEnabled = enabled;

            if (!enabled)
            {
                _savedSnaps = new HashSet<OsnapType>(EnabledSnaps);
                EnabledSnaps.Clear();
            }
            else
            {
                if (_savedSnaps != null)
                {
                    EnabledSnaps.Clear();
                    foreach (var s in _savedSnaps) EnabledSnaps.Add(s);
                    _savedSnaps = null;
                }
                else
                {
                    // Restore defaults if no saved state
                    EnabledSnaps.Add(OsnapType.Endpoint);
                    EnabledSnaps.Add(OsnapType.Midpoint);
                    EnabledSnaps.Add(OsnapType.Nearest);
                    EnabledSnaps.Add(OsnapType.Grid);
                }
            }
        }

        public OsnapSystem(ZoneManager manager)
        {
            _zoneManager = manager;
        }

        /// <summary>
        /// Tests world point against all enabled snap types.
        /// Returns snapped world point, or null if no snap found.
        /// </summary>
        public Point3D? Snap(Point3D world, List<Point3D> currentPoints, HelixViewport3D viewport)
        {
            // Master toggle: short-circuit if all snaps disabled
            if (!MasterEnabled) { LastSnapType = null; LastSnapPoint = null; return null; }

            double snapWorldRadius = WorldSnapRadius(viewport, world);
            Point3D? best = null;
            double bestDist = double.MaxValue;
            OsnapType bestType = OsnapType.Nearest;

            var allVertices = CollectAllVertices().ToList();
            var allEdges = CollectAllEdges().ToList();

            // ── 1. Endpoint snap (highest priority) ──────────────────────────
            if (EnabledSnaps.Contains(OsnapType.Endpoint))
            {
                foreach (var v in allVertices.Concat(DxfSnapPoints).Concat(currentPoints))
                {
                    double d = Dist2D(world, v);
                    if (d < snapWorldRadius && d < bestDist)
                    { best = v; bestDist = d; bestType = OsnapType.Endpoint; }
                }
            }

            // ── 2. Intersection snap ─────────────────────────────────────────
            if (EnabledSnaps.Contains(OsnapType.Intersection))
            {
                var allEdgesList = allEdges.ToList();
                for (int i = 0; i < allEdgesList.Count; i++)
                {
                    for (int j = i + 1; j < allEdgesList.Count; j++)
                    {
                        if (TryIntersect(allEdgesList[i], allEdgesList[j], out Point3D ix))
                        {
                            double d = Dist2D(world, ix);
                            if (d < snapWorldRadius && d < bestDist)
                            { best = ix; bestDist = d; bestType = OsnapType.Intersection; }
                        }
                    }
                }
            }

            // ── 3. Perpendicular snap ─────────────────────────────────────────
            if (EnabledSnaps.Contains(OsnapType.Perpendicular))
            {
                foreach (var (a, b) in allEdges)
                {
                    var proj = ProjectOnSegment(world, a, b);
                    double d = Dist2D(world, proj);
                    // Perpendicular: only snap to the foot, not to endpoints
                    double distToA = Dist2D(proj, a), distToB = Dist2D(proj, b);
                    bool isInterior = distToA > 0.01 && distToB > 0.01;

                    if (isInterior && d < snapWorldRadius * 0.85 && d < bestDist)
                    { best = proj; bestDist = d; bestType = OsnapType.Perpendicular; }
                }
            }

            // ── 4. Midpoint snap ─────────────────────────────────────────────
            if (EnabledSnaps.Contains(OsnapType.Midpoint))
            {
                foreach (var (a, b) in allEdges)
                {
                    var mid = new Point3D((a.X + b.X) / 2, (a.Y + b.Y) / 2, 0);
                    double d = Dist2D(world, mid);
                    if (d < snapWorldRadius && d < bestDist)
                    { best = mid; bestDist = d; bestType = OsnapType.Midpoint; }
                }
            }

            // ── 5. Nearest snap (on edge) ─────────────────────────────────────
            if (EnabledSnaps.Contains(OsnapType.Nearest))
            {
                foreach (var (a, b) in allEdges)
                {
                    var proj = ProjectOnSegment(world, a, b);
                    double d = Dist2D(world, proj);
                    if (d < snapWorldRadius * 0.7 && d < bestDist)
                    { best = proj; bestDist = d; bestType = OsnapType.Nearest; }
                }
            }

            // ── 6. Grid snap (lowest priority) ────────────────────────────────
            if (EnabledSnaps.Contains(OsnapType.Grid))
            {
                double gx = Math.Round(world.X / GridSpacing) * GridSpacing;
                double gy = Math.Round(world.Y / GridSpacing) * GridSpacing;
                var gridPt = new Point3D(gx, gy, 0);
                double d = Dist2D(world, gridPt);
                if (d < snapWorldRadius * 0.5 && d < bestDist)
                { best = gridPt; bestDist = d; bestType = OsnapType.Grid; }
            }

            LastSnapType = best.HasValue ? bestType : (OsnapType?)null;
            LastSnapPoint = best;
            return best;
        }

        public void SetSnapEnabled(OsnapType type, bool enabled)
        {
            if (enabled) EnabledSnaps.Add(type);
            else EnabledSnaps.Remove(type);
        }

        public void ToggleSnap(OsnapType type)
        {
            if (EnabledSnaps.Contains(type)) EnabledSnaps.Remove(type);
            else EnabledSnaps.Add(type);
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        private IEnumerable<Point3D> CollectAllVertices()
        {
            foreach (var z in _zoneManager.Zones)
                foreach (var v in z.Vertices)
                    yield return v;
            if (_zoneManager.SiteBoundary != null)
                foreach (var v in _zoneManager.SiteBoundary.Vertices)
                    yield return v;
            // Yield extra snap points (IFC mesh vertices, etc.)
            foreach (var p in ExtraSnapPoints)
                yield return p;
        }

        private IEnumerable<(Point3D, Point3D)> CollectAllEdges()
        {
            foreach (var z in _zoneManager.Zones)
            {
                var verts = z.Vertices;
                for (int i = 0; i < verts.Count; i++)
                    yield return (verts[i], verts[(i + 1) % verts.Count]);
            }
            if (_zoneManager.SiteBoundary != null)
            {
                var verts = _zoneManager.SiteBoundary.Vertices;
                for (int i = 0; i < verts.Count; i++)
                    yield return (verts[i], verts[(i + 1) % verts.Count]);
            }
        }

        /// <summary>Segment-segment intersection (2D). Returns false if parallel or no crossing.</summary>
        private static bool TryIntersect(
            (Point3D A, Point3D B) e1,
            (Point3D A, Point3D B) e2,
            out Point3D result)
        {
            result = default;
            double x1 = e1.A.X, y1 = e1.A.Y, x2 = e1.B.X, y2 = e1.B.Y;
            double x3 = e2.A.X, y3 = e2.A.Y, x4 = e2.B.X, y4 = e2.B.Y;

            double denom = (x1 - x2) * (y3 - y4) - (y1 - y2) * (x3 - x4);
            if (Math.Abs(denom) < 1e-10) return false; // parallel

            double t = ((x1 - x3) * (y3 - y4) - (y1 - y3) * (x3 - x4)) / denom;
            double u = -((x1 - x2) * (y1 - y3) - (y1 - y2) * (x1 - x3)) / denom;

            if (t < 0 || t > 1 || u < 0 || u > 1) return false; // outside segments

            result = new Point3D(x1 + t * (x2 - x1), y1 + t * (y2 - y1), 0);
            return true;
        }

        private double WorldSnapRadius(HelixViewport3D vp, Point3D world)
        {
            try
            {
                if (vp.Camera is PerspectiveCamera cam)
                    return cam.LookDirection.Length * (SnapRadiusPx / Math.Max(vp.ActualWidth, 1)) * 2;
                if (vp.Camera is OrthographicCamera orth)
                    return orth.Width * (SnapRadiusPx / Math.Max(vp.ActualWidth, 1));
            }
            catch { }
            return 1.0;
        }

        private static double Dist2D(Point3D a, Point3D b)
            => Math.Sqrt(Math.Pow(a.X - b.X, 2) + Math.Pow(a.Y - b.Y, 2));

        private static Point3D ProjectOnSegment(Point3D p, Point3D a, Point3D b)
        {
            double dx = b.X - a.X, dy = b.Y - a.Y;
            double lenSq = dx * dx + dy * dy;
            if (lenSq < 1e-12) return a;
            double t = Math.Clamp(((p.X - a.X) * dx + (p.Y - a.Y) * dy) / lenSq, 0, 1);
            return new Point3D(a.X + t * dx, a.Y + t * dy, 0);
        }
    }
}