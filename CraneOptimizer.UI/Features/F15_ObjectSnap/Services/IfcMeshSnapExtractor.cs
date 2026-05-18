using CraneOptimizer.Domain.Schemas;
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

        public sealed record MeshOptions(
            int MaxTotalVertices = 35_000,
            int MaxVerticesPerElement = 900,
            int MaxSegmentsPerElement = 1_400,
            int MaxTrianglesPerElement = 2_800,
            double FeatureEdgeAngleDegrees = 8.0);

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
                CollectFromVisual(visual, Matrix3D.Identity, inElementLayer: false, raw, options.MaxPointsPerMesh, hardCollectCap);
            }

            if (raw.Count == 0) return raw;

            // Decimate aggressively if needed (pre-dedup for speed).
            raw = Decimate(raw, options.MaxTotalPoints, options.MinStride);

            // Deduplicate on a spatial grid.
            return Deduplicate(raw);
        }

        public static List<IfcSnapGeometry> ExtractElementBoundsFromViewport(HelixViewport3D viewport)
        {
            if (viewport == null) throw new ArgumentNullException(nameof(viewport));

            var result = new List<IfcSnapGeometry>();
            foreach (var visual in viewport.Children.OfType<Visual3D>())
                CollectElementBounds(visual, Matrix3D.Identity, result);

            return result;
        }

        public static List<IfcSnapGeometry> ExtractElementMeshGeometryFromViewport(
            HelixViewport3D viewport,
            MeshOptions? options = null)
        {
            if (viewport == null) throw new ArgumentNullException(nameof(viewport));
            options ??= new MeshOptions();

            var result = new List<IfcSnapGeometry>();
            int totalVertices = 0;

            foreach (var visual in viewport.Children.OfType<Visual3D>())
            {
                if (totalVertices >= options.MaxTotalVertices)
                    break;

                CollectElementMesh(visual, Matrix3D.Identity, result, options, ref totalVertices);
            }

            return result;
        }

        private static void CollectElementMesh(
            Visual3D visual,
            Matrix3D parentTransform,
            List<IfcSnapGeometry> sink,
            MeshOptions options,
            ref int totalVertices)
        {
            if (visual is not ModelVisual3D mv)
                return;

            var local = parentTransform;
            if (mv.Transform != null && !mv.Transform.Value.IsIdentity)
                local.Append(mv.Transform.Value);

            var tag = mv.GetValue(FrameworkElement.TagProperty);
            if (tag is PrecastElement element && !string.IsNullOrWhiteSpace(element.ElementID))
            {
                var builder = new ElementMeshBuilder(element.ElementID, options);
                CollectElementVisualMesh(mv, local, builder);
                var geometry = builder.Build();
                if (geometry != null)
                {
                    sink.Add(geometry);
                    totalVertices += geometry.Vertices.Count;
                }

                return;
            }

            foreach (var child in mv.Children.OfType<Visual3D>())
            {
                if (totalVertices >= options.MaxTotalVertices)
                    break;

                CollectElementMesh(child, local, sink, options, ref totalVertices);
            }
        }

        private static void CollectElementVisualMesh(
            ModelVisual3D visual,
            Matrix3D transform,
            ElementMeshBuilder builder)
        {
            if (visual.Content != null)
                CollectMeshFromModel(visual.Content, transform, builder);

            foreach (var child in visual.Children.OfType<ModelVisual3D>())
            {
                var childTransform = transform;
                if (child.Transform != null && !child.Transform.Value.IsIdentity)
                    childTransform.Append(child.Transform.Value);

                CollectElementVisualMesh(child, childTransform, builder);
            }
        }

        private static void CollectMeshFromModel(
            Model3D model,
            Matrix3D parentTransform,
            ElementMeshBuilder builder)
        {
            if (builder.IsFull)
                return;

            if (model is Model3DGroup group)
            {
                foreach (var child in group.Children)
                {
                    if (builder.IsFull)
                        break;

                    CollectMeshFromModel(child, parentTransform, builder);
                }

                return;
            }

            if (model is not GeometryModel3D geometryModel)
                return;

            var local = parentTransform;
            if (geometryModel.Transform != null && !geometryModel.Transform.Value.IsIdentity)
                local.Append(geometryModel.Transform.Value);

            if (geometryModel.Geometry is not MeshGeometry3D mesh ||
                mesh.Positions == null ||
                mesh.Positions.Count < 3)
            {
                return;
            }

            if (mesh.TriangleIndices != null && mesh.TriangleIndices.Count >= 3)
            {
                for (int i = 0; i + 2 < mesh.TriangleIndices.Count && !builder.IsFull; i += 3)
                {
                    int i0 = mesh.TriangleIndices[i];
                    int i1 = mesh.TriangleIndices[i + 1];
                    int i2 = mesh.TriangleIndices[i + 2];
                    if (!IsValidIndex(i0, mesh.Positions.Count) ||
                        !IsValidIndex(i1, mesh.Positions.Count) ||
                        !IsValidIndex(i2, mesh.Positions.Count))
                    {
                        continue;
                    }

                    builder.AddTriangle(
                        Transform(mesh.Positions[i0], local),
                        Transform(mesh.Positions[i1], local),
                        Transform(mesh.Positions[i2], local));
                }

                return;
            }

            for (int i = 0; i + 2 < mesh.Positions.Count && !builder.IsFull; i += 3)
            {
                builder.AddTriangle(
                    Transform(mesh.Positions[i], local),
                    Transform(mesh.Positions[i + 1], local),
                    Transform(mesh.Positions[i + 2], local));
            }
        }

        private static bool IsValidIndex(int index, int count)
            => index >= 0 && index < count;

        private static void CollectElementBounds(
            Visual3D visual,
            Matrix3D parentTransform,
            List<IfcSnapGeometry> sink)
        {
            if (visual is not ModelVisual3D mv)
                return;

            var local = parentTransform;
            if (mv.Transform != null && !mv.Transform.Value.IsIdentity)
                local.Append(mv.Transform.Value);

            var tag = mv.GetValue(FrameworkElement.TagProperty);
            if (tag is PrecastElement element &&
                !string.IsNullOrWhiteSpace(element.ElementID) &&
                mv.Content != null &&
                !mv.Content.Bounds.IsEmpty)
            {
                sink.Add(BuildGeometryFromBounds(element.ElementID, mv.Content.Bounds, local));
            }

            foreach (var child in mv.Children.OfType<Visual3D>())
                CollectElementBounds(child, local, sink);
        }

        private static IfcSnapGeometry BuildGeometryFromBounds(string elementId, Rect3D bounds, Matrix3D transform)
        {
            var min = new Point3D(bounds.X, bounds.Y, bounds.Z);
            var max = new Point3D(bounds.X + bounds.SizeX, bounds.Y + bounds.SizeY, bounds.Z + bounds.SizeZ);

            var p000 = Transform(new Point3D(min.X, min.Y, min.Z), transform);
            var p100 = Transform(new Point3D(max.X, min.Y, min.Z), transform);
            var p110 = Transform(new Point3D(max.X, max.Y, min.Z), transform);
            var p010 = Transform(new Point3D(min.X, max.Y, min.Z), transform);
            var p001 = Transform(new Point3D(min.X, min.Y, max.Z), transform);
            var p101 = Transform(new Point3D(max.X, min.Y, max.Z), transform);
            var p111 = Transform(new Point3D(max.X, max.Y, max.Z), transform);
            var p011 = Transform(new Point3D(min.X, max.Y, max.Z), transform);

            var vertices = new List<Point3D> { p000, p100, p110, p010, p001, p101, p111, p011 };
            var segments = new List<(Point3D A, Point3D B)>
            {
                (p000, p100), (p100, p110), (p110, p010), (p010, p000),
                (p001, p101), (p101, p111), (p111, p011), (p011, p001),
                (p000, p001), (p100, p101), (p110, p111), (p010, p011),
            };

            return new IfcSnapGeometry(elementId, vertices, segments);
        }

        private static Point3D Transform(Point3D point, Matrix3D transform)
            => transform.IsIdentity ? point : transform.Transform(point);

        private static void CollectFromVisual(
            Visual3D visual,
            Matrix3D parentTransform,
            bool inElementLayer,
            List<Point3D> sink,
            int maxPointsPerMesh,
            int hardCollectCap)
        {
            if (visual is not ModelVisual3D mv) return;
            if (sink.Count >= hardCollectCap) return;

            // Only harvest IFC element meshes (skip grid lines, lights, zones, DXF, etc.)
            // Layer tagging is done in HelixRenderingService.
            var tag = mv.GetValue(FrameworkElement.TagProperty);
            bool isElementLayer =
                inElementLayer ||
                tag is PrecastElement ||
                tag is string layer && string.Equals(layer, "Elements", StringComparison.OrdinalIgnoreCase);

            var local = parentTransform;
            if (mv.Transform != null && !mv.Transform.Value.IsIdentity)
                local.Append(mv.Transform.Value);

            if (!isElementLayer)
            {
                // Still traverse children because some builders may nest visuals,
                // but ignore this node's mesh content if it isn't tagged.
                foreach (var child in mv.Children.OfType<Visual3D>())
                {
                    if (sink.Count >= hardCollectCap) break;
                    CollectFromVisual(child, local, inElementLayer: false, sink, maxPointsPerMesh, hardCollectCap);
                }
                return;
            }

            if (mv.Content != null)
                CollectFromModel(mv.Content, local, sink, maxPointsPerMesh, hardCollectCap);

            // Children visuals can add additional transforms.
            foreach (var child in mv.Children.OfType<Visual3D>())
            {
                if (sink.Count >= hardCollectCap) break;
                CollectFromVisual(child, local, inElementLayer: true, sink, maxPointsPerMesh, hardCollectCap);
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

        private sealed class ElementMeshBuilder
        {
            private readonly string _elementId;
            private readonly MeshOptions _options;
            private readonly double _featureCos;
            private readonly Dictionary<(long X, long Y, long Z), int> _vertexByKey = new();
            private readonly List<Point3D> _vertices = new();
            private readonly Dictionary<(int A, int B), EdgeNormals> _edges = new();
            private int _triangleCount;

            public ElementMeshBuilder(string elementId, MeshOptions options)
            {
                _elementId = elementId;
                _options = options;
                _featureCos = Math.Cos(options.FeatureEdgeAngleDegrees * Math.PI / 180.0);
            }

            public bool IsFull =>
                _vertices.Count >= _options.MaxVerticesPerElement ||
                _edges.Count >= _options.MaxSegmentsPerElement * 4 ||
                _triangleCount >= _options.MaxTrianglesPerElement;

            public void AddTriangle(Point3D p0, Point3D p1, Point3D p2)
            {
                if (IsFull)
                    return;

                var normal = Vector3D.CrossProduct(p1 - p0, p2 - p0);
                if (normal.LengthSquared < 1e-8)
                    return;

                normal.Normalize();

                int i0 = GetVertexIndex(p0);
                int i1 = GetVertexIndex(p1);
                int i2 = GetVertexIndex(p2);
                if (i0 < 0 || i1 < 0 || i2 < 0)
                    return;

                AddEdge(i0, i1, normal);
                AddEdge(i1, i2, normal);
                AddEdge(i2, i0, normal);
                _triangleCount++;
            }

            public IfcSnapGeometry? Build()
            {
                var featureEdges = _edges
                    .Where(pair => pair.Value.IsFeature(_featureCos))
                    .Take(_options.MaxSegmentsPerElement)
                    .Select(pair => pair.Key)
                    .ToList();

                if (featureEdges.Count == 0 && _vertices.Count == 0)
                    return null;

                var used = new HashSet<int>();
                var segments = new List<(Point3D A, Point3D B)>(featureEdges.Count);
                foreach (var edge in featureEdges)
                {
                    used.Add(edge.A);
                    used.Add(edge.B);
                    segments.Add((_vertices[edge.A], _vertices[edge.B]));
                }

                var snapVertices = used.Count > 0
                    ? used.Select(index => _vertices[index]).ToList()
                    : _vertices.Take(_options.MaxVerticesPerElement).ToList();

                return new IfcSnapGeometry(_elementId, snapVertices, segments);
            }

            private int GetVertexIndex(Point3D point)
            {
                var key = ToKey(point);
                if (_vertexByKey.TryGetValue(key, out int existing))
                    return existing;

                if (_vertices.Count >= _options.MaxVerticesPerElement)
                    return -1;

                int index = _vertices.Count;
                _vertices.Add(point);
                _vertexByKey[key] = index;
                return index;
            }

            private void AddEdge(int a, int b, Vector3D normal)
            {
                if (a == b)
                    return;

                var key = a < b ? (a, b) : (b, a);
                if (!_edges.TryGetValue(key, out var edge))
                {
                    edge = new EdgeNormals();
                    _edges[key] = edge;
                }

                edge.Add(normal);
            }

            private static (long X, long Y, long Z) ToKey(Point3D point)
                => (
                    (long)Math.Round(point.X / 1.0),
                    (long)Math.Round(point.Y / 1.0),
                    (long)Math.Round(point.Z / 1.0));
        }

        private sealed class EdgeNormals
        {
            private readonly List<Vector3D> _normals = new(2);

            public void Add(Vector3D normal)
            {
                if (_normals.Count >= 4)
                    return;

                _normals.Add(normal);
            }

            public bool IsFeature(double featureCos)
            {
                if (_normals.Count < 2)
                    return true;

                for (int i = 0; i < _normals.Count; i++)
                {
                    for (int j = i + 1; j < _normals.Count; j++)
                    {
                        double dot = Vector3D.DotProduct(_normals[i], _normals[j]);
                        if (dot < featureCos)
                            return true;
                    }
                }

                return false;
            }
        }
    }
}

