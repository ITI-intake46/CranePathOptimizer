using CraneOptimizer.Domain.Common;
using CraneOptimizer.Domain.Interfaces;
using CraneOptimizer.Domain.Schemas;
using CraneOptimizer.UI.Core.Interfaces;
using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Models;
using HelixToolkit.Geometry;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using Xbim.Common.Geometry;
using Xbim.Common.XbimExtensions;
using Xbim.Ifc4.Interfaces;

namespace CraneOptimizer.UI.Features.F2_IfcViewer
{
    public class ElementVisualBuilder : IVisualBuilder<(PrecastElement element, LiftStatus? status)>
    {
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

        public Visual3D Build((PrecastElement element, LiftStatus? status) item)
        {
            var group = new Model3DGroup();
            BuildFallbackBox(group, item.element, item.status);
            return new ModelVisual3D { Content = group };
        }

        /// <summary>
        /// Renders IFC elements using the EXACT same approach as the reference code:
        ///   1. Use model.ModelFactors.OneMeter for scaling (xBIM-computed, reliable)
        ///   2. Iterate ALL shape instances, filter only by product label
        ///   3. Cast to IXbimShapeGeometryData for raw byte[] ShapeData
        ///   4. Divide coordinates by OneMeter to get meters, then convert once to millimeters
        ///      because this viewport/grid works in millimeter-sized scene units
        /// </summary>
        public Visual3D BuildBatch(IEnumerable<(PrecastElement element, LiftStatus? status)> items)
        {
            var batchGroup = new Model3DGroup();
            var itemsList = items.ToList();
            if (_renderOnlyStatusElements)
                itemsList = itemsList.Where(item => item.status.HasValue).ToList();

            if (_ifcReader is CraneOptimizer.Infrastructure.Repositories.IfcReaderRepository concreteRepo
                && concreteRepo.GeometryContext != null
                && concreteRepo.CurrentModel != null)
            {
                var model = concreteRepo.CurrentModel;
                var context = concreteRepo.GeometryContext;

                // Use OneMeter exactly like the reference code.
                // OneMeter = how many model-internal-units make 1 real meter.
                // This is computed by xBIM's engine and is always correct.
                double oneMeter = model.ModelFactors.OneMeter;
                if (oneMeter <= 0) oneMeter = 1000.0;

                Debug.WriteLine($"[ElementVisualBuilder] OneMeter = {oneMeter}");

                // Build a lookup: GlobalId → (element, status, color)
                var elementMap = new Dictionary<string, (PrecastElement el, LiftStatus? st, Color col)>(
                    StringComparer.OrdinalIgnoreCase);
                foreach (var item in itemsList)
                {
                    var color = GetColor(item.status);
                    elementMap[item.element.ElementID] = (item.element, item.status, color);
                }

                // Build a lookup: IfcProductLabel → GlobalId for fast shape filtering
                var labelToGid = new Dictionary<int, string>();
                foreach (var p in model.Instances.OfType<IIfcProduct>())
                {
                    var gid = p.GlobalId.ToString();
                    if (!string.IsNullOrEmpty(gid) && elementMap.ContainsKey(gid))
                    {
                        labelToGid[p.EntityLabel] = gid;
                        AddChildProductLabels(p, gid, labelToGid);
                    }
                }

                Debug.WriteLine($"[ElementVisualBuilder] Mapped {labelToGid.Count} product labels to GlobalIds");

                // Track which elements got real geometry
                var renderedElements = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                // Iterate ALL shape instances — EXACTLY like the reference code.
                // The reference code does NOT filter by RepresentationType.
                // It renders every shape instance that belongs to a whitelisted product.
                var allInstances = context.ShapeInstances()
                    .Where(si => labelToGid.ContainsKey(si.IfcProductLabel))
                    .ToList();

                Debug.WriteLine($"[ElementVisualBuilder] Found {allInstances.Count} shape instances to render");

                var origin = _origin ?? new DomainPoint3D(0, 0, 0);
                var placementOffset = _placementOffset ?? new DomainPoint3D(0, 0, 0);

                int successCount = 0;
                int failCount = 0;

                foreach (var si in allInstances)
                {
                    try
                    {
                        // Cast to IXbimShapeGeometryData to get ShapeData as byte[]
                        var shapeGeom = context.ShapeGeometry(si) as IXbimShapeGeometryData;
                        if (shapeGeom?.ShapeData == null || shapeGeom.ShapeData.Length == 0)
                        {
                            failCount++;
                            continue;
                        }

                        var gid = labelToGid[si.IfcProductLabel];
                        var (el, st, col) = elementMap[gid];

                        // Build mesh using the reference code's xBIM approach:
                        //   wp / oneMeter -> meters, then convert once to millimeters.
                        var mesh = CreateMeshFlat(
                            shapeGeom.ShapeData,
                            si.Transformation,
                            oneMeter,
                            origin,
                            placementOffset);
                        if (mesh == null || mesh.Positions.Count == 0)
                        {
                            failCount++;
                            continue;
                        }

                        mesh.Freeze();

                        var material = new DiffuseMaterial(new SolidColorBrush(col));
                        var geoModel = new GeometryModel3D
                        {
                            Geometry = mesh,
                            Material = material,
                            BackMaterial = material
                        };

                        batchGroup.Children.Add(geoModel);
                        renderedElements.Add(gid);
                        successCount++;
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"[ElementVisualBuilder] Shape error: {ex.Message}");
                        failCount++;
                    }
                }

                Debug.WriteLine($"[ElementVisualBuilder] Rendered {successCount} shapes, {failCount} failed");

                // Do not draw normal missing elements as bounding boxes here. A large
                // assembly box can look like a stretched column and hide the real mesh.
                foreach (var item in itemsList)
                {
                    if (!renderedElements.Contains(item.element.ElementID))
                    {
                        Debug.WriteLine($"[ElementVisualBuilder] No xBIM shape geometry for: {item.element.ElementID}");
                    }
                }
            }
            else
            {
                Debug.WriteLine("[ElementVisualBuilder] No GeometryContext — all fallback boxes");
                foreach (var item in itemsList)
                {
                    BuildFallbackBox(batchGroup, item.element, item.status);
                }
            }

            return new ModelVisual3D { Content = batchGroup };
        }

        private static void AddChildProductLabels(
            IIfcProduct product,
            string owningGlobalId,
            Dictionary<int, string> labelToGid)
        {
            var visited = new HashSet<int>();
            AddChildProductLabels(product, owningGlobalId, labelToGid, visited);
        }

        private static void AddChildProductLabels(
            IIfcProduct product,
            string owningGlobalId,
            Dictionary<int, string> labelToGid,
            HashSet<int> visited)
        {
            if (!visited.Add(product.EntityLabel))
                return;

            var children = product.IsDecomposedBy?
                .SelectMany(rel => rel.RelatedObjects)
                .OfType<IIfcProduct>() ?? Enumerable.Empty<IIfcProduct>();

            foreach (var child in children)
            {
                if (!labelToGid.ContainsKey(child.EntityLabel))
                    labelToGid[child.EntityLabel] = owningGlobalId;

                AddChildProductLabels(child, owningGlobalId, labelToGid, visited);
            }
        }

        /// <summary>
        /// Creates a mesh from raw xBIM binary shape data.
        /// Uses the same xBIM transform order as the reference code's CreateMeshFlat:
        ///   wp = matrix.Transform(vertex)
        ///   result = wp / oneMeter * 1000       -> millimeters
        ///   result = result + placement - origin
        /// </summary>
        private static System.Windows.Media.Media3D.MeshGeometry3D? CreateMeshFlat(
            byte[] shapeData,
            XbimMatrix3D matrix,
            double oneMeter,
            DomainPoint3D origin,
            DomainPoint3D placementOffset)
        {
            var positions = new Point3DCollection();
            var normals = new Vector3DCollection();
            var indices = new Int32Collection();

            using var ms = new MemoryStream(shapeData);
            using var br = new BinaryReader(ms);

            var xbimMesh = br.ReadShapeTriangulation();
            if (xbimMesh == null) return null;

            // xBIM gives local vertices plus the shape-instance transform.
            // Convert to millimeters exactly once so the mesh matches the extracted
            // IFC metadata and the existing viewport/grid scale.
            var tv = xbimMesh.Vertices
                .Select(v =>
                {
                    var wp = matrix.Transform(new XbimPoint3D(v.X, v.Y, v.Z));
                    return new Point3D(
                        wp.X / oneMeter * 1000.0 + placementOffset.X - origin.X,
                        wp.Y / oneMeter * 1000.0 + placementOffset.Y - origin.Y,
                        wp.Z / oneMeter * 1000.0 + placementOffset.Z - origin.Z);
                })
                .ToArray();

            foreach (var face in xbimMesh.Faces)
            {
                for (int i = 0; i < face.TriangleCount; i++)
                {
                    var p0 = tv[face.Indices[3 * i]];
                    var p1 = tv[face.Indices[3 * i + 1]];
                    var p2 = tv[face.Indices[3 * i + 2]];

                    var n = Vector3D.CrossProduct(p1 - p0, p2 - p0);
                    n.Normalize();
                    if (double.IsNaN(n.X)) continue;

                    int idx = positions.Count;
                    positions.Add(p0); normals.Add(n);
                    positions.Add(p1); normals.Add(n);
                    positions.Add(p2); normals.Add(n);
                    indices.Add(idx); indices.Add(idx + 1); indices.Add(idx + 2);
                }
            }

            if (positions.Count == 0) return null;

            return new System.Windows.Media.Media3D.MeshGeometry3D
            {
                Positions = positions,
                Normals = normals,
                TriangleIndices = indices
            };
        }

        private void BuildFallbackBox(Model3DGroup group, PrecastElement element, LiftStatus? status)
        {
            var color = GetColor(status);
            var material = new DiffuseMaterial(new SolidColorBrush(color));

            var meshBuilder = new MeshBuilder(true, true);
            var x = element.ElementCOG.X - (_origin?.X ?? 0d);
            var y = element.ElementCOG.Y - (_origin?.Y ?? 0d);
            var z = element.ElementCOG.Z - (_origin?.Z ?? 0d);

            meshBuilder.AddBox(
                new Vector3((float)x, (float)y, (float)z),
                (float)(element.BBoxLength ?? 1),
                (float)(element.BBoxWidth ?? 1),
                (float)(element.BBoxHeight ?? 1)
            );

            var helixMesh = meshBuilder.ToMesh();
            var fallbackModel = new GeometryModel3D
            {
                Geometry = new System.Windows.Media.Media3D.MeshGeometry3D
                {
                    Positions = new Point3DCollection(helixMesh.Positions.Select(p => new Point3D(p.X, p.Y, p.Z))),
                    TriangleIndices = new Int32Collection(helixMesh.TriangleIndices),
                    Normals = new Vector3DCollection(helixMesh.Normals.Select(n => new Vector3D(n.X, n.Y, n.Z)))
                },
                Material = material,
                BackMaterial = material
            };

            group.Children.Add(fallbackModel);
        }

        private Color GetColor(LiftStatus? status)
        {
            if (!status.HasValue)
                return Color.FromRgb(160, 160, 170);

            return status.Value switch
            {
                LiftStatus.Liftable => Colors.LightGreen,
                LiftStatus.Overweight => Colors.Gold,
                LiftStatus.OutOfRadius => Colors.IndianRed,
                LiftStatus.HeightFail => Colors.IndianRed,
                LiftStatus.SpecialCase => Colors.Gold,
                _ => Colors.Gray
            };
        }
    }
}
