using CraneOptimizer.Domain.Common;
using CraneOptimizer.UI.Core.Interfaces;
using HelixToolkit.Wpf;
using System.Windows.Media;
using System.Windows.Media.Media3D;

namespace CraneOptimizer.UI.Features.F7_Zones
{
    public record SnapIndicatorData(
        DomainPoint3D Point,
        SnapType Type,
        bool IsMeasurement = false);

    public sealed class SnapIndicatorBuilder : IVisualBuilder<SnapIndicatorData>
    {
        public Visual3D Build(SnapIndicatorData data)
        {
            var container = new ModelVisual3D();

            Color color = data.IsMeasurement
                ? Colors.Magenta
                : data.Type switch
                {
                    SnapType.Point => Colors.Cyan,
                    SnapType.Orthogonal => Colors.Yellow,
                    SnapType.Grid => Colors.LightGreen,
                    _ => Colors.White
            };

            var point = data.Point;
            double arm = data.IsMeasurement ? 0.7 : 0.4;
            double diamondSize = data.IsMeasurement ? 0.38 : 0.22;

            var cross = new LinesVisual3D
            {
                Color = color,
                Thickness = data.IsMeasurement ? 5 : 3
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
                Thickness = data.IsMeasurement ? 4 : 2
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

            if (data.IsMeasurement)
            {
                var outline = new PointsVisual3D
                {
                    Color = Colors.White,
                    Size = 17
                };
                outline.Points.Add(new Point3D(point.X, point.Y, point.Z));
                container.Children.Add(outline);

                var center = new PointsVisual3D
                {
                    Color = color,
                    Size = 11
                };
                center.Points.Add(new Point3D(point.X, point.Y, point.Z));
                container.Children.Add(center);
            }

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
