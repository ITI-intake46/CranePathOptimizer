using CraneOptimizer.Domain.Schemas;
using CraneOptimizer.Domain.Schemas.Enums;
using CraneOptimizer.UI.Core.Interfaces;
using CraneOptimizer.Domain.Interfaces;
using HelixToolkit.Wpf;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using System.Collections.Generic;
using System.Linq;

namespace CraneOptimizer.UI.Features.F7_Zones
{
    public class ZoneVisualBuilder : IVisualBuilder<GreenAreaZone>
    {
        private readonly ISceneService _scene;
        private readonly IZoneGeometryService _geometryService;

        public ZoneVisualBuilder(ISceneService scene, IZoneGeometryService? geometryService = null)
        {
            _scene = scene;
            _geometryService = geometryService ?? ZoneGeometryServiceHolder.Instance ?? new CraneOptimizer.Domain.Services.ZoneGeometryService();
        }

        public Visual3D Build(GreenAreaZone zone)
        {
            var normalizedVertices = _geometryService.NormalizeVertices(zone.Vertices);
            var points = normalizedVertices
                .Select(v => new Point3D(v.X, v.Y, v.Z))
                .ToList();

            if (points.Count < 3)
                return new ModelVisual3D();

            bool isSelected = _scene.SelectedZone == zone;

            var baseColor = zone.Type == ZoneType.Forbidden
                ? Colors.Red
                : Colors.Green;

            var finalColor = isSelected ? Colors.Yellow : baseColor;

            // Separate border visual with Z-offset to prevent z-fighting
            var border = new LinesVisual3D
            {
                Color = finalColor,
                Thickness = isSelected ? 5 : 3
            };

            for (int i = 0; i < points.Count; i++)
            {
                var p1 = points[i];
                var p2 = points[(i + 1) % points.Count];
                // Apply a small Z offset (0.02 meters) to place the outline cleanly on top of the fill mesh
                border.Points.Add(new Point3D(p1.X, p1.Y, p1.Z + 0.02));
                border.Points.Add(new Point3D(p2.X, p2.Y, p2.Z + 0.02));
            }

            // Triangulate using Ear Clipping for convex/concave polygon fill
            var triangleIndices = _geometryService.TriangulateSimplePolygon(normalizedVertices);

            if (triangleIndices.Count == 0)
            {
                // Fallback to simple triangle fan for degenerate/extreme cases
                for (int i = 1; i < points.Count - 1; i++)
                {
                    triangleIndices.Add(0);
                    triangleIndices.Add(i);
                    triangleIndices.Add(i + 1);
                }
            }

            // Fill mesh geometry with small Z offset (0.005 meters) to avoid z-fighting with the grid/floors
            var wpfMesh = new MeshGeometry3D();
            foreach (var p in points)
            {
                wpfMesh.Positions.Add(new Point3D(p.X, p.Y, p.Z + 0.005));
            }
            foreach (var idx in triangleIndices)
            {
                wpfMesh.TriangleIndices.Add(idx);
            }

            var material = new DiffuseMaterial(
                new SolidColorBrush(finalColor)
                {
                    Opacity = isSelected ? 0.85 : 0.5
                });

            var model = new GeometryModel3D
            {
                Geometry = wpfMesh,
                Material = material,
                BackMaterial = material
            };

            var container = new ModelVisual3D { Content = model };
            container.SetValue(FrameworkElement.TagProperty, zone);
            container.Children.Add(border);

            return container;
        }

        public Visual3D BuildBatch(IEnumerable<GreenAreaZone> items)
        {
            var container = new ModelVisual3D();
            foreach (var item in items)
            {
                container.Children.Add(Build(item));
            }
            return container;
        }
    }
}