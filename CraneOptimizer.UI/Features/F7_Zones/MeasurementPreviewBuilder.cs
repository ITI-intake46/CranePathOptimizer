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
        private readonly double _visualScale;

        public MeasurementPreviewBuilder(double visualScale = 1d)
        {
            _visualScale = System.Math.Clamp(visualScale, 0.85d, 1.2d);
        }

        public Visual3D Build(MeasurementLineData data)
        {
            var container = new ModelVisual3D();
            Color measurementColor =
                ((SolidColorBrush)System.Windows.Application.Current.FindResource(
                    "PrimaryBrush")).Color;

            var startPoint = new PointsVisual3D
            {
                Color = measurementColor,
                Size = 7 * _visualScale
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
                    Color = measurementColor,
                    Size = 7 * _visualScale
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
