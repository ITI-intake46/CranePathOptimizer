using CraneOptimizer.UI.Core.Interfaces;
using HelixToolkit.Geometry;
using System.Numerics;
using System.Windows.Media;
using System.Windows.Media.Media3D;

namespace CraneOptimizer.UI.Features.F8_GAOptimizer.Rendering
{
    public class BestPointVisualBuilder : IVisualBuilder<(double x, double y, double z)>
    {
        private readonly double _markerRadiusMeters;

        public BestPointVisualBuilder(double markerRadiusMeters)
        {
            _markerRadiusMeters = Math.Max(0.01, markerRadiusMeters);
        }

        public Visual3D Build((double x, double y, double z) item)
        {
            // Use normalized meter coordinates so GA markers align with IFC scene geometry.
            var center = new Point3D(item.x, item.y, item.z);

            var meshBuilder = new MeshBuilder(true, true);
            meshBuilder.AddSphere(
                new Vector3((float)center.X, (float)center.Y, (float)center.Z),
                (float)_markerRadiusMeters,
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

            var brush = System.Windows.Application.Current?.TryFindResource("SelectedCranePointBrush") as Brush
                ?? new SolidColorBrush(Color.FromRgb(59, 130, 246));
            var material = new DiffuseMaterial(brush);
            var geometryModel = new GeometryModel3D
            {
                Geometry = mesh,
                Material = material,
                BackMaterial = material
            };

            return new ModelVisual3D
            {
                Content = geometryModel
            };
        }

        public Visual3D BuildBatch(IEnumerable<(double x, double y, double z)> items)
        {
            var group = new Model3DGroup();
            foreach (var item in items)
            {
                var visual = Build(item) as ModelVisual3D;
                if (visual?.Content != null)
                {
                    group.Children.Add(visual.Content);
                }
            }
            return new ModelVisual3D { Content = group };
        }
    }
}
