using CraneOptimizer.Domain.Common;
using CraneOptimizer.UI.Core.Interfaces;
using HelixToolkit.Wpf;
using System.Windows.Media;
using System.Windows.Media.Media3D;

namespace CraneOptimizer.UI.Features.F7_Zones
{
    public record MeasurementLineData(DomainPoint3D Start, DomainPoint3D End);

    public class MeasurementPreviewBuilder : IVisualBuilder<MeasurementLineData>
    {
        public Visual3D Build(MeasurementLineData data)
        {
            var container = new ModelVisual3D();

            var line = new LinesVisual3D
            {
                Color = Colors.Cyan,
                Thickness = 3
            };
            line.Points.Add(new Point3D(data.Start.X, data.Start.Y, data.Start.Z));
            line.Points.Add(new Point3D(data.End.X, data.End.Y, data.End.Z));
            container.Children.Add(line);

            var startOutline = new PointsVisual3D
            {
                Color = Colors.White,
                Size = 15
            };
            startOutline.Points.Add(new Point3D(data.Start.X, data.Start.Y, data.Start.Z));
            container.Children.Add(startOutline);

            var startPoint = new PointsVisual3D
            {
                Color = Colors.Gold,
                Size = 10
            };
            startPoint.Points.Add(new Point3D(data.Start.X, data.Start.Y, data.Start.Z));
            container.Children.Add(startPoint);

            double dx = data.End.X - data.Start.X;
            double dy = data.End.Y - data.Start.Y;
            double dz = data.End.Z - data.Start.Z;
            if (dx * dx + dy * dy + dz * dz > 1e-12)
            {
                var hoverPoint = new PointsVisual3D
                {
                    Color = Colors.Magenta,
                    Size = 10
                };
                hoverPoint.Points.Add(new Point3D(data.End.X, data.End.Y, data.End.Z));
                container.Children.Add(hoverPoint);
            }

            return container;
        }

        public Visual3D BuildBatch(IEnumerable<MeasurementLineData> items)
        {
            var container = new ModelVisual3D();
            foreach (MeasurementLineData item in items)
                container.Children.Add(Build(item));

            return container;
        }
    }
}
