using CraneOptimizer.UI.Core.Interfaces;
using HelixToolkit.Geometry;
using System.Numerics;
using System.Windows.Media;
using System.Windows.Media.Media3D;

namespace CraneOptimizer.UI.Features.F8_GAOptimizer
{
    public class BestPointVisualBuilder : IVisualBuilder<(double x, double y, double z)>
    {
        private readonly double _markerRadiusInMillimeters;

        public BestPointVisualBuilder(double markerRadiusInMillimeters)
        {
            _markerRadiusInMillimeters = Math.Max(1.0, markerRadiusInMillimeters);
        }

        public Visual3D Build((double x, double y, double z) item)
        {
            // FIX: use raw mm coordinates (same as IFC rendering)
            var center = new Point3D(item.x, item.y, 0);

            // FIX: keep radius in mm (NO scaling)
            var renderRadius = _markerRadiusInMillimeters;

            var meshBuilder = new MeshBuilder(true, true);
            meshBuilder.AddSphere(
                new Vector3((float)center.X, (float)center.Y, (float)center.Z),
                (float)renderRadius,
                18,
                12);

            var helixMesh = meshBuilder.ToMesh();
            var mesh = new System.Windows.Media.Media3D.MeshGeometry3D
            {
                Positions = new Point3DCollection(
                    helixMesh.Positions.Select(position => new Point3D(position.X, position.Y, position.Z))),
                TriangleIndices = new Int32Collection(helixMesh.TriangleIndices)
            };

            if (helixMesh.Normals != null)
            {
                mesh.Normals = new Vector3DCollection(
                    helixMesh.Normals.Select(normal => new Vector3D(normal.X, normal.Y, normal.Z)));
            }

            var geometryModel = new GeometryModel3D
            {
                Geometry = mesh,
                Material = new DiffuseMaterial(new SolidColorBrush(Colors.Blue)),
                BackMaterial = new DiffuseMaterial(new SolidColorBrush(Colors.Blue))
            };

            return new ModelVisual3D
            {
                Content = geometryModel
            };
        }
    }
}
