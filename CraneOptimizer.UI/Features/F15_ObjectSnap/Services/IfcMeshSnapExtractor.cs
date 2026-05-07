using HelixToolkit.Wpf;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Media.Media3D;

namespace CraneOptimizer.UI.Features.F15_ObjectSnap.Services
{
    /// <summary>
    /// Extracts snap-eligible vertices from the *rendered* IFC visuals in the Helix viewport.
    /// This keeps snapping aligned with what the user sees (including origin shifts).
    /// </summary>
    public static class IfcMeshSnapExtractor
    {
        // Keep consistent with IfcSnapExtractor (1 cm) but configurable here if needed.
        private const double DedupCellSize = 0.01;

        public sealed record Options(
            int MaxTotalPoints = 50_000,
            int MaxPointsPerMesh = 10_000,
            int MinStride = 1);

        public static List<Point3D> ExtractFromViewport(HelixViewport3D viewport, Options? options = null)
        {
            if (viewport == null) throw new ArgumentNullException(nameof(viewport));
            options ??= new Options();

            // Gather mesh vertices (world/render space) from all visuals in the viewport.
            var raw = new List<Point3D>(capacity: 4096);

            // Hard cap while collecting to prevent UI freezes on very large IFC models.
            // We over-collect slightly so decimation + dedup still have enough points.
            int hardCollectCap = Math.Max(options.MaxTotalPoints * 2, options.MaxTotalPoints + 10_000);

            foreach (var visual in viewport.Children.OfType<Visual3D>())
            {
                if (raw.Count >= hardCollectCap) break;
                CollectFromVisual(viewport, visual, Matrix3D.Identity, raw, options.MaxPointsPerMesh, hardCollectCap);
            }

            if (raw.Count == 0) return raw;

            // Decimate aggressively if needed (pre-dedup for speed).
            raw = Decimate(raw, options.MaxTotalPoints, options.MinStride);

            // Deduplicate on a spatial grid.
            return Deduplicate(raw);
        }

        private static void CollectFromVisual(
            HelixViewport3D viewport,
            Visual3D visual,
            Matrix3D parentTransform,
            List<Point3D> sink,
            int maxPointsPerMesh,
            int hardCollectCap)
        {
            if (visual is not ModelVisual3D mv) return;
            if (sink.Count >= hardCollectCap) return;

            // Only harvest IFC element meshes (skip grid lines, lights, zones, DXF, etc.)
            // Layer tagging is done in HelixRenderingService.
            var tag = mv.GetValue(FrameworkElement.TagProperty) as string;
            if (!string.Equals(tag, "Elements", StringComparison.OrdinalIgnoreCase))
            {
                // Still traverse children because some builders may nest visuals,
                // but ignore this node's mesh content if it isn't tagged.
                foreach (var child in mv.Children.OfType<Visual3D>())
                {
                    if (sink.Count >= hardCollectCap) break;
                    CollectFromVisual(viewport, child, parentTransform, sink, maxPointsPerMesh, hardCollectCap);
                }
                return;
            }

            var local = parentTransform;
            if (mv.Transform != null && !mv.Transform.Value.IsIdentity)
                local.Append(mv.Transform.Value);

            if (mv.Content != null)
                CollectFromModel(mv.Content, local, sink, maxPointsPerMesh, hardCollectCap);

            // Children visuals can add additional transforms.
            foreach (var child in mv.Children.OfType<Visual3D>())
            {
                if (sink.Count >= hardCollectCap) break;
                CollectFromVisual(viewport, child, local, sink, maxPointsPerMesh, hardCollectCap);
            }
        }

        private static void CollectFromModel(
            Model3D model,
            Matrix3D parentTransform,
            List<Point3D> sink,
            int maxPointsPerMesh,
            int hardCollectCap)
        {
            if (model is Model3DGroup g)
            {
                foreach (var child in g.Children)
                {
                    if (sink.Count >= hardCollectCap) break;
                    CollectFromModel(child, parentTransform, sink, maxPointsPerMesh, hardCollectCap);
                }
                return;
            }

            if (model is not GeometryModel3D gm) return;
            if (sink.Count >= hardCollectCap) return;

            var local = parentTransform;
            if (gm.Transform != null && !gm.Transform.Value.IsIdentity)
                local.Append(gm.Transform.Value);

            if (gm.Geometry is not MeshGeometry3D mesh) return;

            var positions = mesh.Positions;
            if (positions == null || positions.Count == 0) return;

            // Per-mesh stride to cap cost.
            int stride = 1;
            if (positions.Count > maxPointsPerMesh && maxPointsPerMesh > 0)
                stride = Math.Max(1, positions.Count / maxPointsPerMesh);

            for (int i = 0; i < positions.Count; i += stride)
            {
                if (sink.Count >= hardCollectCap) break;
                var p = positions[i];
                if (!local.IsIdentity)
                    p = local.Transform(p);
                sink.Add(p);
            }
        }

        private static List<Point3D> Decimate(List<Point3D> points, int maxTotal, int minStride)
        {
            if (maxTotal <= 0 || points.Count <= maxTotal) return points;
            int stride = Math.Max(minStride, points.Count / maxTotal);
            var result = new List<Point3D>(capacity: Math.Min(maxTotal, points.Count));
            for (int i = 0; i < points.Count; i += stride)
                result.Add(points[i]);
            return result;
        }

        private static List<Point3D> Deduplicate(List<Point3D> points)
        {
            var seen = new HashSet<(long, long, long)>();
            var result = new List<Point3D>(points.Count);

            foreach (var p in points)
            {
                long gx = (long)Math.Round(p.X / DedupCellSize);
                long gy = (long)Math.Round(p.Y / DedupCellSize);
                long gz = (long)Math.Round(p.Z / DedupCellSize);
                if (seen.Add((gx, gy, gz)))
                    result.Add(p);
            }

            return result;
        }
    }
}

