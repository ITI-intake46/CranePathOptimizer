using CraneOptimizer.Domain.Schemas;
using CraneOptimizer.Domain.Schemas.Enums;
using CraneOptimizer.UI.Core.Interfaces;
using HelixToolkit.Wpf;
using System.Windows.Media;
using System.Windows.Media.Media3D;

namespace CraneOptimizer.UI.Features.F7_Zones
{
    public class ZoneVisualBuilder : IVisualBuilder<GreenAreaZone>
    {
        public Visual3D Build(GreenAreaZone zone)
        {
            var meshBuilder = new HelixToolkit.Geometry.MeshBuilder(false, false);

            var points = zone.Vertices
                .Select(v => new Point3D(v.X, v.Y, v.Z))
                .ToList();

            if (points.Count < 3)
                return new ModelVisual3D();

            var color = zone.Type == ZoneType.Forbidden
                ? Colors.Red
                : Colors.Green;

            var border = new LinesVisual3D
            {
                Color = color,
                Thickness = 3
            };

            for (int i = 0; i < points.Count; i++)
            {
                border.Points.Add(points[i]);
                border.Points.Add(points[(i + 1) % points.Count]);
            }

            var vectors = zone.Vertices
                .Select(v => new System.Numerics.Vector3((float)v.X, (float)v.Y, (float)v.Z))
                .ToList();

            meshBuilder.AddPolygon(vectors);

            var helixMesh = meshBuilder.ToMesh();

            var wpfMesh = new MeshGeometry3D
            {
                Positions = new Point3DCollection(
                    helixMesh.Positions.Select(p => new Point3D(p.X, p.Y, p.Z))),
                TriangleIndices = new Int32Collection(helixMesh.TriangleIndices)
            };

            var material = new DiffuseMaterial(
                new SolidColorBrush(color) { Opacity = 0.6 });

            var model = new GeometryModel3D
            {
                Geometry = wpfMesh,  
                Material = material,
                BackMaterial = material
            };

            var container = new ModelVisual3D { Content = model };
            container.Children.Add(border);

            return container;
        }
    }
}
