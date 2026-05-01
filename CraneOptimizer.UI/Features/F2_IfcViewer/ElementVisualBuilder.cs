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
        /// The REAL rendering path. Iterates ALL shape instances from the geometry 
        /// context (exactly like the reference code), gets raw byte[] ShapeData via 
        /// IXbimShapeGeometryData, decodes triangulations, and batches them into a 
        /// single Model3DGroup. Falls back to bounding boxes only for elements with 
        /// no shape data.
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

                // ModelLengthFactor = _lengthToMillimetersFactor (raw model-units → mm)
                // This is EXACTLY the same factor used by ConvertLength() to compute
                // ElementCOG and BBox*, so our mesh vertices will align perfectly.
                double lengthFactor = concreteRepo.ModelLengthFactor;

                Debug.WriteLine($"[ElementVisualBuilder] ModelLengthFactor = {lengthFactor}");

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

                // Iterate ALL shape instances (exactly like reference code)
                var allInstances = context.ShapeInstances()
                    .Where(si => labelToGid.ContainsKey(si.IfcProductLabel))
                    .ToList();

                Debug.WriteLine($"[ElementVisualBuilder] Found {allInstances.Count} shape instances to render");

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
                        var origin = _origin ?? new DomainPoint3D(0, 0, 0);

                        // Decode triangulated mesh from raw bytes (NOT Base64!)
                        var mesh = CreateMeshFlat(shapeGeom.ShapeData, si.Transformation, lengthFactor, origin);
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
                // No geometry context at all → all bounding boxes
                Debug.WriteLine("[ElementVisualBuilder] No GeometryContext — all fallback boxes");
                foreach (var item in itemsList)
                {
                    BuildFallbackBox(batchGroup, item.element, item.status);
                }
            }

            return new ModelVisual3D { Content = batchGroup };
        }

        /// <summary>
        /// Creates a mesh from raw xBIM binary shape data (byte[]).
        /// This is the same approach as the reference code's CreateMeshFlat.
        /// </summary>
        private System.Windows.Media.Media3D.MeshGeometry3D? CreateMeshFlat(
            byte[] shapeData, XbimMatrix3D matrix, double lengthFactor, DomainPoint3D origin)
        {
            var positions = new Point3DCollection();
            var normals = new Vector3DCollection();
            var indices = new Int32Collection();

            using var ms = new MemoryStream(shapeData);
            using var br = new BinaryReader(ms);

            var xbimMesh = br.ReadShapeTriangulation();
            if (xbimMesh == null) return null;

            // Transform vertices: apply instance matrix, then multiply by lengthFactor
            // to convert raw model-units directly to millimeters (same as ConvertLength).
            var tv = xbimMesh.Vertices
                .Select(v =>
                {
                    var wp = matrix.Transform(new XbimPoint3D(v.X, v.Y, v.Z));
                    return new Point3D(
                        wp.X * lengthFactor - origin.X,
                        wp.Y * lengthFactor - origin.Y,
                        wp.Z * lengthFactor - origin.Z);
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
