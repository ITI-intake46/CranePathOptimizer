using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Models;
using CraneOptimizer.Domain.Common;
using CraneOptimizer.Domain.Interfaces;
using CraneOptimizer.Domain.Schemas;
using CraneOptimizer.UI.Core.Interfaces;
using HelixToolkit.Geometry;
using System.Linq;
using System.Numerics;
using Xbim.Common.XbimExtensions;
using System.Windows.Media;
using System.Windows.Media.Media3D;

namespace CraneOptimizer.UI.Features.F2_IfcViewer
{
    /// <summary>
    /// Builds visual representation of IFC elements (no viewport logic).
    /// </summary>
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
            return BuildInternal(item.element, item.status);
        }

        private ModelVisual3D BuildInternal(PrecastElement element, LiftStatus? status)
        {
            var model = new GeometryModel3D();
            var color = GetColor(status);
            var material = new DiffuseMaterial(new SolidColorBrush(color));
            model.Material = material;
            model.BackMaterial = material;

            if (_ifcReader is CraneOptimizer.Infrastructure.Repositories.IfcReaderRepository concreteRepo && concreteRepo.GeometryContext != null)
            {
                var shapes = concreteRepo.GetElementShapes(element.ElementID).ToList();
                System.Diagnostics.Debug.WriteLine($"Element {element.ElementID} has {shapes.Count} shapes.");
                var meshBuilder = new MeshBuilder(true, true);
                var origin = _origin ?? new DomainPoint3D(0, 0, 0);

                bool hasMesh = false;

                foreach (var shape in shapes)
                {
                    var geometry = concreteRepo.GeometryContext.ShapeGeometry(shape);
                    if (geometry == null || string.IsNullOrEmpty(geometry.ShapeData))
                    {
                        System.Diagnostics.Debug.WriteLine($"Shape {shape.ShapeGeometryLabel} has no Geometry Data.");
                        continue;
                    }

                    byte[] bytes;
                    try
                    {
                        bytes = Convert.FromBase64String(geometry.ShapeData);
                    }
                    catch { continue; }

                    using var stream = new System.IO.MemoryStream(bytes);
                    using var reader = new System.IO.BinaryReader(stream);

                    var xbimMesh = reader.ReadShapeTriangulation();
                    if (xbimMesh?.Vertices == null || xbimMesh.Faces == null)
                    {
                        System.Diagnostics.Debug.WriteLine($"Shape {shape.ShapeGeometryLabel} failed to read triangulation.");
                        continue;
                    }

                    var transform = shape.Transformation;

                    // Read all vertices and transform them, then apply origin offset
                    var transformedVertices = xbimMesh.Vertices.Select(v =>
                    {
                        var tv = transform.Transform(v);
                        return new Vector3(
                            (float)(tv.X - origin.X),
                            (float)(tv.Y - origin.Y),
                            (float)(tv.Z - origin.Z));
                    }).ToList();

                    // Read faces and add triangles to meshBuilder
                    foreach (var face in xbimMesh.Faces)
                    {
                        var indices = face.Indices.ToList();
                        for (int i = 0; i + 2 < indices.Count; i += 3)
                        {
                            var p0 = transformedVertices[indices[i]];
                            var p1 = transformedVertices[indices[i + 1]];
                            var p2 = transformedVertices[indices[i + 2]];

                            meshBuilder.AddTriangle(p0, p1, p2);
                            hasMesh = true;
                        }
                    }
                }

                if (hasMesh)
                {
                    System.Diagnostics.Debug.WriteLine($"Successfully generated mesh for element {element.ElementID}.");
                    var helixMesh = meshBuilder.ToMesh();
                    model.Geometry = new System.Windows.Media.Media3D.MeshGeometry3D
                    {
                        Positions = new Point3DCollection(helixMesh.Positions.Select(p => new Point3D(p.X, p.Y, p.Z))),
                        TriangleIndices = new Int32Collection(helixMesh.TriangleIndices),
                        Normals = new Vector3DCollection(helixMesh.Normals.Select(n => new Vector3D(n.X, n.Y, n.Z)))
                    };
                    return new ModelVisual3D { Content = model };
                }
            }
            else
            {
                System.Diagnostics.Debug.WriteLine($"Fallback hit for element {element.ElementID}. Repo type: {_ifcReader?.GetType().Name}");
            }

            // Fallback to bounding box if no shape data is available
            var fallbackMeshBuilder = new MeshBuilder(true, true);
            var x = element.ElementCOG.X - (_origin?.X ?? 0d);
            var y = element.ElementCOG.Y - (_origin?.Y ?? 0d);
            var z = element.ElementCOG.Z - (_origin?.Z ?? 0d);

            fallbackMeshBuilder.AddBox(
                new Vector3((float)x, (float)y, (float)z),
                (float)(element.BBoxLength ?? 1),
                (float)(element.BBoxWidth ?? 1),
                (float)(element.BBoxHeight ?? 1)
            );

            var fallbackHelixMesh = fallbackMeshBuilder.ToMesh();
            model.Geometry = new System.Windows.Media.Media3D.MeshGeometry3D
            {
                Positions = new Point3DCollection(fallbackHelixMesh.Positions.Select(p => new Point3D(p.X, p.Y, p.Z))),
                TriangleIndices = new Int32Collection(fallbackHelixMesh.TriangleIndices),
                Normals = new Vector3DCollection(fallbackHelixMesh.Normals.Select(n => new Vector3D(n.X, n.Y, n.Z)))
            };

            return new ModelVisual3D { Content = model };
        }

        private Color GetColor(LiftStatus? status)
        {
            if (status == null)
                return Colors.Gray;

            return status switch
            {
                LiftStatus.Liftable => Colors.Green,

                LiftStatus.OutOfRadius => Colors.Red,
                LiftStatus.Overweight => Colors.Red,
                LiftStatus.HeightFail => Colors.Red,

                LiftStatus.SpecialCase => Colors.Yellow,

                _ => Colors.Gray
            };
        }
    }
}
