using CraneOptimizer.Domain.Common;
using CraneOptimizer.UI.Core.Interfaces;
using HelixToolkit.Wpf;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Media3D;

namespace CraneOptimizer.UI.Features.F7_Zones
{
    public enum SnapType
    {
        None,
        Point,
        Orthogonal,
        Grid
    }

    public record SnapIndicatorData(
        DomainPoint3D Point,
        SnapType Type,
        bool IsMeasurement = false);

    public sealed class SnapIndicatorBuilder : IVisualBuilder<SnapIndicatorData>
    {
        private readonly double _visualScale;

        public SnapIndicatorBuilder(double visualScale = 1d)
        {
            _visualScale = System.Math.Clamp(visualScale, 0.85d, 1.2d);
        }

        public Visual3D Build(SnapIndicatorData data)
        {
            var container = new ModelVisual3D();

            if (data.IsMeasurement)
            {
                var primaryBrush = (SolidColorBrush)System.Windows.Application.Current.FindResource(
                    "PrimaryBrush");
                var pen = new Pen(primaryBrush, 1.25)
                {
                    StartLineCap = PenLineCap.Round,
                    EndLineCap = PenLineCap.Round,
                    LineJoin = PenLineJoin.Round
                };

                var drawing = new DrawingGroup();
                drawing.Children.Add(new GeometryDrawing(
                    null,
                    pen,
                    Geometry.Parse("M 1,7 L 13,7 M 7,1 L 7,13")));
                drawing.Children.Add(new GeometryDrawing(
                    null,
                    pen,
                    new EllipseGeometry(new Point(7, 7), 3.25, 3.25)));
                drawing.Children.Add(new GeometryDrawing(
                    primaryBrush,
                    null,
                    new EllipseGeometry(new Point(7, 7), 1.2, 1.2)));
                drawing.Freeze();

                var markerBrush = new DrawingBrush(drawing)
                {
                    Viewbox = new Rect(0, 0, 14, 14),
                    ViewboxUnits = BrushMappingMode.Absolute,
                    Stretch = Stretch.Fill
                };
                markerBrush.Freeze();

                var material = new EmissiveMaterial(markerBrush);
                material.Freeze();
                var marker = new BillboardVisual3D
                {
                    Position = new Point3D(
                        data.Point.X,
                        data.Point.Y,
                        data.Point.Z),
                    Width = 16 * _visualScale,
                    Height = 16 * _visualScale,
                    Material = material,
                    DepthOffset = 0.001
                };
                container.Children.Add(marker);
                return container;
            }

            Color color = data.Type switch
                {
                    SnapType.Point => Colors.Cyan,
                    SnapType.Orthogonal => Colors.Yellow,
                    SnapType.Grid => Colors.LightGreen,
                    _ => Colors.White
            };

            var point = data.Point;
            const double arm = 0.4;
            const double diamondSize = 0.22;

            var cross = new LinesVisual3D
            {
                Color = color,
                Thickness = 3
            };
            cross.Points.Add(new Point3D(point.X - arm, point.Y, point.Z));
            cross.Points.Add(new Point3D(point.X + arm, point.Y, point.Z));
            cross.Points.Add(new Point3D(point.X, point.Y - arm, point.Z));
            cross.Points.Add(new Point3D(point.X, point.Y + arm, point.Z));
            cross.Points.Add(new Point3D(point.X, point.Y, point.Z - arm * 0.5));
            cross.Points.Add(new Point3D(point.X, point.Y, point.Z + arm * 0.5));
            container.Children.Add(cross);

            var ring = new LinesVisual3D
            {
                Color = color,
                Thickness = 2
            };
            var diamond = new[]
            {
                new Point3D(point.X,               point.Y + diamondSize, point.Z),
                new Point3D(point.X + diamondSize, point.Y,               point.Z),
                new Point3D(point.X,               point.Y - diamondSize, point.Z),
                new Point3D(point.X - diamondSize, point.Y,               point.Z),
                new Point3D(point.X,               point.Y + diamondSize, point.Z)
            };

            for (int index = 0; index < diamond.Length - 1; index++)
            {
                ring.Points.Add(diamond[index]);
                ring.Points.Add(diamond[index + 1]);
            }
            container.Children.Add(ring);

            return container;
        }

        public Visual3D BuildBatch(IEnumerable<SnapIndicatorData> items)
        {
            var container = new ModelVisual3D();
            foreach (SnapIndicatorData item in items)
                container.Children.Add(Build(item));

            return container;
        }
    }
}
