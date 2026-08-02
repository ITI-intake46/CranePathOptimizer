using CraneOptimizer.Domain.Common;
using CraneOptimizer.UI.Core.Interfaces;
using HelixToolkit.Wpf;
using System.Windows.Media;
using System.Windows.Media.Media3D;

namespace CraneOptimizer.UI.Features.F7_Zones
{
    public record MeasurementVisualData(
        DomainPoint3D Start,
        DomainPoint3D End,
        double Distance,
        string LabelText);

    public class MeasurementFinalBuilder : IVisualBuilder<MeasurementVisualData>
    {
        private readonly double _visualScale;

        public MeasurementFinalBuilder(double visualScale = 1d)
        {
            _visualScale = System.Math.Clamp(visualScale, 0.85d, 1.2d);
        }

        public Visual3D Build(MeasurementVisualData data)
        {
            var container = new ModelVisual3D();
            Color measurementColor =
                ((SolidColorBrush)System.Windows.Application.Current.FindResource(
                    "PrimaryBrush")).Color;

            var endpoints = new PointsVisual3D
            {
                Color = measurementColor,
                Size = 7 * _visualScale
            };
            endpoints.Points.Add(new Point3D(data.Start.X, data.Start.Y, data.Start.Z));
            endpoints.Points.Add(new Point3D(data.End.X, data.End.Y, data.End.Z));
            container.Children.Add(endpoints);

            return container;
        }

        public Visual3D BuildBatch(IEnumerable<MeasurementVisualData> items)
        {
            var container = new ModelVisual3D();
            foreach (MeasurementVisualData item in items)
                container.Children.Add(Build(item));

            return container;
        }
    }
}
