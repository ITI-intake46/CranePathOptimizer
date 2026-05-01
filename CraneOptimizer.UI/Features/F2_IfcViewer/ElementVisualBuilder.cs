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

        public ElementVisualBuilder(IIfcReaderRepository? ifcReader = null, DomainPoint3D? origin = null)
        {
            _ifcReader = ifcReader;
            _origin = origin;
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
        ///   4. Divide coordinates by OneMeter to get meters (reference approach)
        ///      then multiply by 1000 to convert to our mm-based viewport
        /// </summary>
        public Visual3D BuildBatch(IEnumerable<(PrecastElement element, LiftStatus? status)> items)
        {
            var batchGroup = new Model3DGroup();
            var itemsList = items.ToList();

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
                        labelToGid[p.EntityLabel] = gid;
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

                // Convert origin from mm to meters (our origin is stored in mm by ConvertLength,
                // but we'll render in meters first then scale the whole batch to mm)
                var originMm = _origin ?? new DomainPoint3D(0, 0, 0);

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

                        // Build mesh using EXACTLY the reference code's approach:
                        //   wp / oneMeter → meters, then * 1000 → millimeters
                        var mesh = CreateMeshFlat(shapeGeom.ShapeData, si.Transformation, oneMeter, originMm);
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

                // Fallback: any elements that had no shape instances → draw bounding box
                foreach (var item in itemsList)
                {
                    if (!renderedElements.Contains(item.element.ElementID))
                    {
                        Debug.WriteLine($"[ElementVisualBuilder] Fallback box for: {item.element.ElementID}");
                        BuildFallbackBox(batchGroup, item.element, item.status);
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

        /// <summary>
        /// Creates a mesh from raw xBIM binary shape data.
        /// Uses EXACTLY the same math as the reference code's CreateMeshFlat:
        ///   wp = matrix.Transform(vertex)
        ///   result = wp / oneMeter              → meters
        ///   result = result * 1000 - originMm   → millimeters with origin offset
        /// </summary>
        private static System.Windows.Media.Media3D.MeshGeometry3D? CreateMeshFlat(
            byte[] shapeData, XbimMatrix3D matrix, double oneMeter, DomainPoint3D originMm)
        {
            var positions = new Point3DCollection();
            var normals = new Vector3DCollection();
            var indices = new Int32Collection();

            using var ms = new MemoryStream(shapeData);
            using var br = new BinaryReader(ms);

            var xbimMesh = br.ReadShapeTriangulation();
            if (xbimMesh == null) return null;

            // EXACTLY the reference code's vertex transformation:
            //   wp / oneMeter  →  meters
            // Then we convert meters to mm for our viewport:
            //   meters * 1000  →  mm
            // Then subtract origin (already in mm):
            var tv = xbimMesh.Vertices
                .Select(v =>
                {
                    var wp = matrix.Transform(new XbimPoint3D(v.X, v.Y, v.Z));
                    double xM = wp.X / oneMeter;
                    double yM = wp.Y / oneMeter;
                    double zM = wp.Z / oneMeter;
                    return new Point3D(
                        xM * 1000.0 - originMm.X,
                        yM * 1000.0 - originMm.Y,
                        zM * 1000.0 - originMm.Z);
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
                LiftStatus.Overweight => Colors.OrangeRed,
                LiftStatus.OutOfRadius => Colors.DarkRed,
                LiftStatus.HeightFail => Colors.Purple,
                LiftStatus.SpecialCase => Colors.Magenta,
                _ => Colors.Gray
            };
        }
    }
}
