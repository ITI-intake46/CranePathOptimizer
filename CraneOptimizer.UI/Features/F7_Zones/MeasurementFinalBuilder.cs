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
        public Visual3D Build(MeasurementVisualData data)
        {
            var container = new ModelVisual3D();

            var line = new LinesVisual3D
            {
                Color = Colors.Orange,
                Thickness = 3
            };
            line.Points.Add(new Point3D(data.Start.X, data.Start.Y, data.Start.Z));
            line.Points.Add(new Point3D(data.End.X, data.End.Y, data.End.Z));
            container.Children.Add(line);

            var endpointOutlines = new PointsVisual3D
            {
                Color = Colors.White,
                Size = 15
            };
            endpointOutlines.Points.Add(new Point3D(data.Start.X, data.Start.Y, data.Start.Z));
            endpointOutlines.Points.Add(new Point3D(data.End.X, data.End.Y, data.End.Z));
            container.Children.Add(endpointOutlines);

            var endpoints = new PointsVisual3D
            {
                Color = Colors.OrangeRed,
                Size = 10
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
