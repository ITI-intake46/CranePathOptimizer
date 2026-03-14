using HelixToolkit.Geometry;
using System.Numerics;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using CraneOptimizer.Domain.IfcEntities;

namespace CraneOptimizer.UI.Rendering
{
    public class HelixElementBuilder
    {
        public ModelVisual3D BuildElement(PrecastElement element)
        {
            var meshBuilder = new MeshBuilder(true, true);

            meshBuilder.AddBox(
                new Vector3(
                    (float)element.ElementCOG.X,
                    (float)element.ElementCOG.Y,
                    (float)element.ElementCOG.Z),

                (float)(element.BBoxLength ?? 1),
                (float)(element.BBoxWidth ?? 1),
                (float)(element.BBoxHeight ?? 1)
            );

            var helixMesh = meshBuilder.ToMesh();

            var mesh = new System.Windows.Media.Media3D.MeshGeometry3D
            {
                Positions = new Point3DCollection(
                    helixMesh.Positions.Select(p => new Point3D(p.X, p.Y, p.Z))),

                TriangleIndices = new Int32Collection(helixMesh.TriangleIndices),

                Normals = new Vector3DCollection(
                    helixMesh.Normals.Select(n => new Vector3D(n.X, n.Y, n.Z)))
            };

            var material = new DiffuseMaterial(
                new SolidColorBrush(Colors.Gray));

            var model = new GeometryModel3D
            {
                Geometry = mesh,
                Material = material,
                BackMaterial = material
            };

            return new ModelVisual3D { Content = model };
        }
    }
}