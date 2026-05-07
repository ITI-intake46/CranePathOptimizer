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
            var line = new LinesVisual3D
            {
                Color = Colors.Cyan,
                Thickness = 2
            };
            line.Points.Add(new Point3D(data.Start.X, data.Start.Y, data.Start.Z));
            line.Points.Add(new Point3D(data.End.X, data.End.Y, data.End.Z));
            return line;
        }

        public Visual3D BuildBatch(IEnumerable<MeasurementLineData> items)
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