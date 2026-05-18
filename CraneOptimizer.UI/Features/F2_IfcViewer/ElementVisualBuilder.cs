using CraneOptimizer.Domain.Common;
using CraneOptimizer.Domain.Interfaces;
using CraneOptimizer.Domain.Schemas;
using CraneOptimizer.UI.Core.Interfaces;
using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Models;
using HelixToolkit.Geometry;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using Xbim.Common.Geometry;
using Xbim.Common.XbimExtensions;
using Xbim.Ifc4.Interfaces;
using System.Diagnostics;
using MediaMeshGeometry3D = System.Windows.Media.Media3D.MeshGeometry3D;

namespace CraneOptimizer.UI.Features.F2_IfcViewer
{
    public class ElementVisualBuilder : IVisualBuilder<(PrecastElement element, LiftStatus? status, bool isSelected)>
    {
        private const double MinimumBoxSize = 1d;

        public enum MeshQuality
        {
            Low,
            Medium,
            High
        }

        public static MeshQuality CurrentQuality { get; set; } = MeshQuality.Medium;

        private static readonly Dictionary<int, MediaMeshGeometry3D> MeshCache = new();

        private readonly IIfcReaderRepository? _ifcReader;
        private readonly DomainPoint3D? _origin;
        private readonly DomainPoint3D? _placementOffset;
        private readonly bool _renderOnlyStatusElements;
        private readonly byte? _statusAlpha;

        public ElementVisualBuilder(
            IIfcReaderRepository? ifcReader = null,
            DomainPoint3D? origin = null,
            DomainPoint3D? placementOffset = null,
            bool renderOnlyStatusElements = false,
            byte? statusAlpha = null)
        {
            _ifcReader = ifcReader;
            _origin = origin;
            _placementOffset = placementOffset;
            _renderOnlyStatusElements = renderOnlyStatusElements;
            _statusAlpha = statusAlpha;
        }

        public Visual3D Build((PrecastElement element, LiftStatus? status, bool isSelected) item)
        {
            var container = new ModelVisual3D();
            BuildFallbackBox(container, item.element, item.status, item.isSelected);
            return container;
        }

        public Visual3D BuildBatch(IEnumerable<(PrecastElement element, LiftStatus? status, bool isSelected)> items)
        {
            var itemsList = items.ToList();
            
            // DEBUG: Verify status distribution
            int withStatus = itemsList.Count(i => i.status.HasValue);
            Debug.WriteLine($"[ElementVisualBuilder] BuildBatch called for {itemsList.Count} items. Items with LiftStatus: {withStatus}");

            if (_renderOnlyStatusElements)
                itemsList = itemsList.Where(item => item.status.HasValue).ToList();

            if (CurrentQuality == MeshQuality.Low)
                return BuildLargeModelBatch(itemsList);

            if (_ifcReader is CraneOptimizer.Infrastructure.Repositories.IfcReaderRepository concreteRepo
                && concreteRepo.GeometryContext != null
                && concreteRepo.CurrentModel != null)
            {
                var model = concreteRepo.CurrentModel;
                var context = concreteRepo.GeometryContext;

                double oneMeter = model.ModelFactors.OneMeter;
                if (oneMeter <= 0) oneMeter = 1000.0;

                var elementMap = new Dictionary<string, (PrecastElement el, LiftStatus? st, Color col, bool sel)>(StringComparer.OrdinalIgnoreCase);
                foreach (var item in itemsList)
                {
                    var color = GetColor(item.status);
                    elementMap[item.element.ElementID] = (item.element, item.status, color, item.isSelected);
                }

                var labelToGid = new Dictionary<int, string>();
                foreach (var p in model.Instances.OfType<IIfcProduct>())
                {
                    var gid = p.GlobalId.ToString();
                    var revitId = (p as IIfcElement)?.Tag?.ToString();

                    // Check if either Revit ID or Global ID matches our element map
                    var matchingKey = string.Empty;
                    if (revitId != null && elementMap.ContainsKey(revitId))
                        matchingKey = revitId;
                    else if (!string.IsNullOrEmpty(gid) && elementMap.ContainsKey(gid))
                        matchingKey = gid;

                    if (!string.IsNullOrEmpty(matchingKey))
                    {
                        labelToGid[p.EntityLabel] = matchingKey;
                        AddChildProductLabels(p, matchingKey, labelToGid);
                    }
                }

                var allInstances = context.ShapeInstances()
                    .Where(si => labelToGid.ContainsKey(si.IfcProductLabel))
                    .ToList();

                var origin = _origin ?? new DomainPoint3D(0, 0, 0);
                var placementOffset = _placementOffset ?? new DomainPoint3D(0, 0, 0);

                var selectionMaterial = CreateFrozenDiffuseMaterial(Color.FromArgb(110, 100, 180, 255));
                var elementVisualMap = new Dictionary<string, ModelVisual3D>();
                var batchVisual = new ModelVisual3D();

                foreach (var si in allInstances)
                {
                    try
                    {
                        var shapeGeom = context.ShapeGeometry(si) as IXbimShapeGeometryData;
                        if (shapeGeom?.ShapeData == null || shapeGeom.ShapeData.Length == 0) continue;

                        var gid = labelToGid[si.IfcProductLabel];
                        var (el, _, col, isSelected) = elementMap[gid];

                        var mesh = CurrentQuality == MeshQuality.High
                            ? CreateHighQualityDetailedMesh(shapeGeom.ShapeData, si.Transformation, oneMeter, origin, placementOffset)
                            : GetOrCreateCachedMesh(shapeGeom.ShapeData, oneMeter, CurrentQuality);
                        if (mesh == null || mesh.Positions.Count == 0) continue;
                        if (CurrentQuality == MeshQuality.High)
                            mesh.Freeze();

                        if (!elementVisualMap.TryGetValue(gid, out var ev))
                        {
                            ev = new ModelVisual3D { Content = new Model3DGroup() };
                            ev.SetValue(System.Windows.FrameworkElement.TagProperty, el);
                            elementVisualMap[gid] = ev;
                            batchVisual.Children.Add(ev);
                        }

                        var material = BuildMaterial(col, isSelected, selectionMaterial);

                        var geoModel = new GeometryModel3D
                        {
                            Geometry = mesh,
                            Material = material,
                            BackMaterial = material,
                            Transform = CurrentQuality == MeshQuality.High
                                ? Transform3D.Identity
                                : CreateInstanceTransform(si.Transformation, oneMeter, origin, placementOffset)
                        };

                        ((Model3DGroup)ev.Content).Children.Add(geoModel);
                    }
                    catch
                    {
                        // keep rendering robust per element
                    }
                }

                return batchVisual;
            }

            var fallbackBatch = new ModelVisual3D();
            foreach (var item in itemsList)
                BuildFallbackBox(fallbackBatch, item.element, item.status, item.isSelected);

            return fallbackBatch;
        }

        private Visual3D BuildLargeModelBatch(IReadOnlyList<(PrecastElement element, LiftStatus? status, bool isSelected)> items)
        {
            var group = new Model3DGroup();

            foreach (var renderGroup in items.GroupBy(GetBatchKey))
            {
                var mesh = BuildIndexedBoxMesh(renderGroup.Select(item => item.element));
                if (mesh == null || mesh.Positions.Count == 0)
                    continue;

                mesh.Freeze();
                var material = BuildMaterial(renderGroup.Key.Color, renderGroup.Key.IsSelected, CreateFrozenDiffuseMaterial(Color.FromArgb(110, 100, 180, 255)));
                if (material.CanFreeze)
                    material.Freeze();

                group.Children.Add(new GeometryModel3D
                {
                    Geometry = mesh,
                    Material = material,
                    BackMaterial = material
                });
            }

            if (group.CanFreeze)
                group.Freeze();

            return new ModelVisual3D { Content = group };
        }

        private BatchKey GetBatchKey((PrecastElement element, LiftStatus? status, bool isSelected) item)
        {
            var element = item.element;
            return new BatchKey(
                GetColor(item.status),
                item.status,
                NormalizeBatchText(element.ElementMaterial),
                NormalizeBatchText(element.ElementType),
                item.isSelected);
        }

        private MediaMeshGeometry3D? BuildIndexedBoxMesh(IEnumerable<PrecastElement> elements)
        {
            var origin = _origin ?? new DomainPoint3D(0, 0, 0);
            var positions = new Point3DCollection();
            var indices = new Int32Collection();

            foreach (var element in elements)
            {
                var length = GetBoxDimension(element.BBoxLength ?? element.ElementLength);
                var width = GetBoxDimension(element.BBoxWidth ?? element.ElementWidth);
                var height = GetBoxDimension(element.BBoxHeight ?? element.ElementHeight);
                var center = new Point3D(
                    element.ElementCOG.X - origin.X,
                    element.ElementCOG.Y - origin.Y,
                    element.ElementCOG.Z - origin.Z);

                AddIndexedBox(positions, indices, center, length, width, height);
            }

            if (positions.Count == 0)
                return null;

            var mesh = new MediaMeshGeometry3D
            {
                Positions = positions,
                TriangleIndices = indices
            };

            mesh.Normals = CalculateIndexedNormals(positions, indices);
            return mesh;
        }

        private static void AddIndexedBox(Point3DCollection positions, Int32Collection indices, Point3D center, double length, double width, double height)
        {
            var hx = length / 2d;
            var hy = width / 2d;
            var hz = height / 2d;
            var start = positions.Count;

            positions.Add(new Point3D(center.X - hx, center.Y - hy, center.Z - hz));
            positions.Add(new Point3D(center.X + hx, center.Y - hy, center.Z - hz));
            positions.Add(new Point3D(center.X + hx, center.Y + hy, center.Z - hz));
            positions.Add(new Point3D(center.X - hx, center.Y + hy, center.Z - hz));
            positions.Add(new Point3D(center.X - hx, center.Y - hy, center.Z + hz));
            positions.Add(new Point3D(center.X + hx, center.Y - hy, center.Z + hz));
            positions.Add(new Point3D(center.X + hx, center.Y + hy, center.Z + hz));
            positions.Add(new Point3D(center.X - hx, center.Y + hy, center.Z + hz));

            AddQuad(indices, start, 0, 3, 2, 1);
            AddQuad(indices, start, 4, 5, 6, 7);
            AddQuad(indices, start, 0, 1, 5, 4);
            AddQuad(indices, start, 1, 2, 6, 5);
            AddQuad(indices, start, 2, 3, 7, 6);
            AddQuad(indices, start, 3, 0, 4, 7);
        }

        private static void AddQuad(Int32Collection indices, int start, int a, int b, int c, int d)
        {
            indices.Add(start + a);
            indices.Add(start + b);
            indices.Add(start + c);
            indices.Add(start + a);
            indices.Add(start + c);
            indices.Add(start + d);
        }

        private static Vector3DCollection CalculateIndexedNormals(Point3DCollection positions, Int32Collection indices)
        {
            var normals = new Vector3D[positions.Count];

            for (var i = 0; i + 2 < indices.Count; i += 3)
            {
                var i0 = indices[i];
                var i1 = indices[i + 1];
                var i2 = indices[i + 2];
                var n = Vector3D.CrossProduct(positions[i1] - positions[i0], positions[i2] - positions[i0]);
                if (n.LengthSquared < 1e-18)
                    continue;

                normals[i0] += n;
                normals[i1] += n;
                normals[i2] += n;
            }

            var result = new Vector3DCollection(positions.Count);
            foreach (var normal in normals)
            {
                var n = normal;
                if (n.LengthSquared < 1e-18)
                    n = new Vector3D(0, 0, 1);
                else
                    n.Normalize();

                result.Add(n);
            }

            return result;
        }

        private static double GetBoxDimension(double? value)
        {
            if (!value.HasValue || double.IsNaN(value.Value) || double.IsInfinity(value.Value) || value.Value <= 0)
                return MinimumBoxSize;

            return Math.Max(MinimumBoxSize, value.Value);
        }

        private static string NormalizeBatchText(string? value) => string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();

        private readonly record struct BatchKey(Color Color, LiftStatus? Status, string Material, string Type, bool IsSelected);

        private static Material BuildMaterial(Color baseColor, bool isSelected, Material selectionOverlay)
        {
            var mg = new MaterialGroup();
            mg.Children.Add(CreateFrozenDiffuseMaterial(baseColor));

            if (CurrentQuality != MeshQuality.Low)
            {
                // Add slight specular for close-up shape readability in mesh modes.
                var specPower = CurrentQuality == MeshQuality.High ? 90.0 : 35.0;
                var specularBrush = new SolidColorBrush(Color.FromArgb(90, 255, 255, 255));
                specularBrush.Freeze();
                mg.Children.Add(new SpecularMaterial(specularBrush, specPower));
            }

            if (isSelected)
                mg.Children.Add(selectionOverlay);

            return mg;
        }

        private static DiffuseMaterial CreateFrozenDiffuseMaterial(Color color)
        {
            var brush = new SolidColorBrush(color);
            brush.Freeze();
            var material = new DiffuseMaterial(brush);
            material.Freeze();
            return material;
        }

        private static void AddChildProductLabels(IIfcProduct product, string owningGlobalId, Dictionary<int, string> labelToGid)
        {
            var visited = new HashSet<int>();
            AddChildProductLabels(product, owningGlobalId, labelToGid, visited);
        }

        private static void AddChildProductLabels(IIfcProduct product, string owningGlobalId, Dictionary<int, string> labelToGid, HashSet<int> visited)
        {
            if (!visited.Add(product.EntityLabel)) return;
            var children = product.IsDecomposedBy?.SelectMany(rel => rel.RelatedObjects).OfType<IIfcProduct>() ?? Enumerable.Empty<IIfcProduct>();
            foreach (var child in children)
            {
                if (!labelToGid.ContainsKey(child.EntityLabel)) labelToGid[child.EntityLabel] = owningGlobalId;
                AddChildProductLabels(child, owningGlobalId, labelToGid, visited);
            }
        }

        private static MediaMeshGeometry3D? GetOrCreateCachedMesh(byte[] shapeData, double oneMeter, MeshQuality quality)
        {
            var cacheKey = GetShapeCacheKey(shapeData, oneMeter, quality);
            if (MeshCache.TryGetValue(cacheKey, out var cachedMesh))
                return cachedMesh;

            var mesh = CreateIndexedLocalMesh(shapeData, oneMeter, quality);
            if (mesh == null)
                return null;

            mesh.Freeze();
            MeshCache[cacheKey] = mesh;
            return mesh;
        }

        private static int GetShapeCacheKey(byte[] shapeData, double oneMeter, MeshQuality quality)
        {
            unchecked
            {
                var hash = 17;
                hash = (hash * 31) + quality.GetHashCode();
                hash = (hash * 31) + oneMeter.GetHashCode();
                hash = (hash * 31) + shapeData.Length;

                foreach (var value in shapeData)
                    hash = (hash * 31) + value;

                return hash;
            }
        }

        private static Transform3D CreateInstanceTransform(XbimMatrix3D matrix, double oneMeter, DomainPoint3D origin, DomainPoint3D placementOffset)
        {
            var scale = 1000.0 / oneMeter;
            var wpfMatrix = new Matrix3D(
                matrix.M11, matrix.M12, matrix.M13, matrix.M14,
                matrix.M21, matrix.M22, matrix.M23, matrix.M24,
                matrix.M31, matrix.M32, matrix.M33, matrix.M34,
                matrix.OffsetX * scale - origin.X + placementOffset.X,
                matrix.OffsetY * scale - origin.Y + placementOffset.Y,
                matrix.OffsetZ * scale - origin.Z + placementOffset.Z,
                matrix.M44);

            var transform = new MatrixTransform3D(wpfMatrix);
            transform.Freeze();
            return transform;
        }

        // High quality intentionally preserves the pre-optimization rendering path:
        // full transformed IFC triangulation with no decimation or mesh cache reuse.
        private static MediaMeshGeometry3D? CreateHighQualityDetailedMesh(
            byte[] shapeData,
            XbimMatrix3D matrix,
            double oneMeter,
            DomainPoint3D origin,
            DomainPoint3D placementOffset)
        {
            using var ms = new MemoryStream(shapeData);
            using var br = new BinaryReader(ms);
            var xbimMesh = br.ReadShapeTriangulation();
            if (xbimMesh == null) return null;

            var transformedVertices = xbimMesh.Vertices.Select(v =>
            {
                var worldPoint = matrix.Transform(new XbimPoint3D(v.X, v.Y, v.Z));
                return new Point3D(
                    worldPoint.X / oneMeter * 1000.0 - origin.X + placementOffset.X,
                    worldPoint.Y / oneMeter * 1000.0 - origin.Y + placementOffset.Y,
                    worldPoint.Z / oneMeter * 1000.0 - origin.Z + placementOffset.Z);
            }).ToArray();

            var positions = new Point3DCollection();
            var normals = new Vector3DCollection();
            var indices = new Int32Collection();

            foreach (var face in xbimMesh.Faces)
            {
                for (int i = 0; i < face.TriangleCount; i++)
                {
                    var sourceIndex = 3 * i;
                    if (sourceIndex + 2 >= face.Indices.Count)
                        continue;

                    var p0 = transformedVertices[face.Indices[sourceIndex]];
                    var p1 = transformedVertices[face.Indices[sourceIndex + 1]];
                    var p2 = transformedVertices[face.Indices[sourceIndex + 2]];

                    var normal = Vector3D.CrossProduct(p1 - p0, p2 - p0);
                    if (normal.LengthSquared < 1e-18) continue;
                    normal.Normalize();

                    int index = positions.Count;
                    positions.Add(p0); normals.Add(normal);
                    positions.Add(p1); normals.Add(normal);
                    positions.Add(p2); normals.Add(normal);

                    indices.Add(index);
                    indices.Add(index + 1);
                    indices.Add(index + 2);
                }
            }

            return positions.Count == 0
                ? null
                : new MediaMeshGeometry3D
                {
                    Positions = positions,
                    Normals = normals,
                    TriangleIndices = indices
                };
        }

        // Indexed local geometry keeps shared vertices and applies IFC instance transforms separately.
        private static MediaMeshGeometry3D? CreateIndexedLocalMesh(byte[] shapeData, double oneMeter, MeshQuality quality)
        {
            using var ms = new MemoryStream(shapeData);
            using var br = new BinaryReader(ms);
            var xbimMesh = br.ReadShapeTriangulation();
            if (xbimMesh == null) return null;

            var scale = 1000.0 / oneMeter;
            var positions = new Point3DCollection(xbimMesh.Vertices.Select(v => new Point3D(v.X * scale, v.Y * scale, v.Z * scale)));
            var triIndices = new Int32Collection();
            var normalAccumulator = new Vector3D[positions.Count];

            foreach (var face in xbimMesh.Faces)
            {
                for (int i = 0; i < face.TriangleCount; i++)
                {
                    int i0 = face.Indices[3 * i];
                    int i1 = face.Indices[3 * i + 1];
                    int i2 = face.Indices[3 * i + 2];

                    if (i0 < 0 || i0 >= positions.Count || i1 < 0 || i1 >= positions.Count || i2 < 0 || i2 >= positions.Count)
                        continue;

                    var p0 = positions[i0];
                    var p1 = positions[i1];
                    var p2 = positions[i2];

                    var n = Vector3D.CrossProduct(p1 - p0, p2 - p0);
                    if (n.LengthSquared < 1e-18) continue;

                    normalAccumulator[i0] += n;
                    normalAccumulator[i1] += n;
                    normalAccumulator[i2] += n;

                    triIndices.Add(i0);
                    triIndices.Add(i1);
                    triIndices.Add(i2);
                }
            }

            if (triIndices.Count == 0)
                return null;

            var normalsShared = new Vector3DCollection(positions.Count);
            for (int i = 0; i < positions.Count; i++)
            {
                var n = normalAccumulator[i];
                if (n.LengthSquared < 1e-18) n = new Vector3D(0, 0, 1);
                else n.Normalize();
                normalsShared.Add(n);
            }

            return new MediaMeshGeometry3D
            {
                Positions = positions,
                Normals = normalsShared,
                TriangleIndices = triIndices
            };
        }

        private void BuildFallbackBox(ModelVisual3D container, PrecastElement element, LiftStatus? status, bool isSelected)
        {
            var color = GetColor(status);
            var meshBuilder = new MeshBuilder(true, true);
            var x = element.ElementCOG.X - (_origin?.X ?? 0d);
            var y = element.ElementCOG.Y - (_origin?.Y ?? 0d);
            var z = element.ElementCOG.Z - (_origin?.Z ?? 0d);
            meshBuilder.AddBox(new Vector3((float)x, (float)y, (float)z), (float)(element.BBoxLength ?? 1), (float)(element.BBoxWidth ?? 1), (float)(element.BBoxHeight ?? 1));
            var helixMesh = meshBuilder.ToMesh();
            var meshGeom = new System.Windows.Media.Media3D.MeshGeometry3D
            {
                Positions = new Point3DCollection(helixMesh.Positions.Select(p => new Point3D(p.X, p.Y, p.Z))),
                TriangleIndices = new Int32Collection(helixMesh.TriangleIndices),
                Normals = helixMesh.Normals == null
                    ? new Vector3DCollection()
                    : new Vector3DCollection(helixMesh.Normals.Select(n => new Vector3D(n.X, n.Y, n.Z)))
            };

            var finalMaterial = BuildMaterial(color, isSelected, new DiffuseMaterial(new SolidColorBrush(Color.FromArgb(120, 100, 180, 255))));

            var group = new Model3DGroup();
            group.Children.Add(new GeometryModel3D { Geometry = meshGeom, Material = finalMaterial, BackMaterial = finalMaterial });

            var ev = new ModelVisual3D { Content = group };
            ev.SetValue(System.Windows.FrameworkElement.TagProperty, element);
            container.Children.Add(ev);
        }

        private Color GetColor(LiftStatus? status)
        {
            if (!status.HasValue) return Color.FromRgb(160, 160, 175); // Neutral Blueprint Gray

            var color = status.Value switch
            {
                // Premium High-Contrast Palette
                LiftStatus.Liftable    => Color.FromRgb(46, 204, 113),  // Emerald Green
                LiftStatus.Overweight  => Color.FromRgb(241, 196, 15),  // Vivid Sun Yellow
                LiftStatus.OutOfRadius => Color.FromRgb(231, 76, 60),   // Alizarin Red
                LiftStatus.HeightFail  => Color.FromRgb(230, 126, 34),  // Carrot Orange
                LiftStatus.SpecialCase => Color.FromRgb(155, 89, 182),  // Amethyst Purple
                _                      => Color.FromRgb(149, 165, 166)  // Asbestos Gray
            };

            return _statusAlpha.HasValue
                ? Color.FromArgb(_statusAlpha.Value, color.R, color.G, color.B)
                : color;
        }
    }
}
