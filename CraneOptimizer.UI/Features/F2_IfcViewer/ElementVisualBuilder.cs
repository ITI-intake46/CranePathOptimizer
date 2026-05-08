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

namespace CraneOptimizer.UI.Features.F2_IfcViewer
{
    public class ElementVisualBuilder : IVisualBuilder<(PrecastElement element, LiftStatus? status, bool isSelected)>
    {
        public enum MeshQuality
        {
            Standard,
            High
        }

        public static MeshQuality CurrentQuality { get; set; } = MeshQuality.Standard;

        private readonly IIfcReaderRepository? _ifcReader;
        private readonly DomainPoint3D? _origin;
        private readonly DomainPoint3D? _placementOffset;
        private readonly bool _renderOnlyStatusElements;

        public ElementVisualBuilder(
            IIfcReaderRepository? ifcReader = null,
            DomainPoint3D? origin = null,
            DomainPoint3D? placementOffset = null,
            bool renderOnlyStatusElements = false)
        {
            _ifcReader = ifcReader;
            _origin = origin;
            _placementOffset = placementOffset;
            _renderOnlyStatusElements = renderOnlyStatusElements;
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

                var selectionMaterial = new DiffuseMaterial(new SolidColorBrush(Color.FromArgb(110, 100, 180, 255)));
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

                        var mesh = CreateMeshSmooth(shapeGeom.ShapeData, si.Transformation, oneMeter, origin, placementOffset);
                        if (mesh == null || mesh.Positions.Count == 0) continue;
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
                            BackMaterial = material
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

        private static Material BuildMaterial(Color baseColor, bool isSelected, Material selectionOverlay)
        {
            var mg = new MaterialGroup();
            mg.Children.Add(new DiffuseMaterial(new SolidColorBrush(baseColor)));

            // Add slight specular for better close-up shape readability.
            var specPower = CurrentQuality == MeshQuality.High ? 90.0 : 45.0;
            mg.Children.Add(new SpecularMaterial(new SolidColorBrush(Color.FromArgb(90, 255, 255, 255)), specPower));

            if (isSelected)
                mg.Children.Add(selectionOverlay);

            return mg;
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

        // Smoother normals for better shading. Keeps double precision until final Point3D creation.
        private static System.Windows.Media.Media3D.MeshGeometry3D? CreateMeshSmooth(byte[] shapeData, XbimMatrix3D matrix, double oneMeter, DomainPoint3D origin, DomainPoint3D placementOffset)
        {
            using var ms = new MemoryStream(shapeData);
            using var br = new BinaryReader(ms);
            var xbimMesh = br.ReadShapeTriangulation();
            if (xbimMesh == null) return null;

            var tv = xbimMesh.Vertices.Select(v =>
            {
                var wp = matrix.Transform(new XbimPoint3D(v.X, v.Y, v.Z));
                return new Point3D(
                    wp.X / oneMeter * 1000.0 + placementOffset.X - origin.X,
                    wp.Y / oneMeter * 1000.0 + placementOffset.Y - origin.Y,
                    wp.Z / oneMeter * 1000.0 + placementOffset.Z - origin.Z);
            }).ToArray();

            // Standard = flat-ish look via per-corner duplication.
            if (CurrentQuality == MeshQuality.Standard)
            {
                var positions = new Point3DCollection();
                var normals = new Vector3DCollection();
                var indices = new Int32Collection();

                foreach (var face in xbimMesh.Faces)
                {
                    for (int i = 0; i < face.TriangleCount; i++)
                    {
                        var p0 = tv[face.Indices[3 * i]];
                        var p1 = tv[face.Indices[3 * i + 1]];
                        var p2 = tv[face.Indices[3 * i + 2]];

                        var n = Vector3D.CrossProduct(p1 - p0, p2 - p0);
                        if (n.LengthSquared < 1e-18) continue;
                        n.Normalize();

                        int idx = positions.Count;
                        positions.Add(p0); normals.Add(n);
                        positions.Add(p1); normals.Add(n);
                        positions.Add(p2); normals.Add(n);

                        indices.Add(idx); indices.Add(idx + 1); indices.Add(idx + 2);
                    }
                }

                return positions.Count == 0
                    ? null
                    : new System.Windows.Media.Media3D.MeshGeometry3D { Positions = positions, Normals = normals, TriangleIndices = indices };
            }

            // High quality = shared vertices + averaged normals.
            var triIndices = new Int32Collection();
            var normalAccumulator = new Vector3D[tv.Length];

            foreach (var face in xbimMesh.Faces)
            {
                for (int i = 0; i < face.TriangleCount; i++)
                {
                    int i0 = face.Indices[3 * i];
                    int i1 = face.Indices[3 * i + 1];
                    int i2 = face.Indices[3 * i + 2];

                    var p0 = tv[i0];
                    var p1 = tv[i1];
                    var p2 = tv[i2];

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

            var normalsShared = new Vector3DCollection(tv.Length);
            for (int i = 0; i < tv.Length; i++)
            {
                var n = normalAccumulator[i];
                if (n.LengthSquared < 1e-18) n = new Vector3D(0, 0, 1);
                else n.Normalize();
                normalsShared.Add(n);
            }

            return new System.Windows.Media.Media3D.MeshGeometry3D
            {
                Positions = new Point3DCollection(tv),
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

            return status.Value switch
            {
                // Premium High-Contrast Palette
                LiftStatus.Liftable    => Color.FromRgb(46, 204, 113),  // Emerald Green
                LiftStatus.Overweight  => Color.FromRgb(241, 196, 15),  // Vivid Sun Yellow
                LiftStatus.OutOfRadius => Color.FromRgb(231, 76, 60),   // Alizarin Red
                LiftStatus.HeightFail  => Color.FromRgb(230, 126, 34),  // Carrot Orange
                LiftStatus.SpecialCase => Color.FromRgb(155, 89, 182),  // Amethyst Purple
                _                      => Color.FromRgb(149, 165, 166)  // Asbestos Gray
            };
        }
    }
}
