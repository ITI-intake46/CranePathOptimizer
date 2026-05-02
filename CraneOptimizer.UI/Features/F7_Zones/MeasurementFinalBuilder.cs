using CraneOptimizer.Domain.Common;
using CraneOptimizer.UI.Core.Interfaces;
using HelixToolkit.Wpf;
using System.Windows.Media;
using System.Windows.Media.Media3D;

namespace CraneOptimizer.UI.Features.F7_Zones
{
    /// <summary>
    /// Data for one finalized measurement segment (rendered in MeasureFinal layer).
    /// </summary>
    public record MeasurementVisualData(DomainPoint3D Start, DomainPoint3D End, double Distance);

    /// <summary>
    /// Renders one measurement segment:
    ///   • An orange line from Start to End
    ///   • A camera-facing BillboardText label at the midpoint showing "X.XXX m"
    /// </summary>
    public class MeasurementFinalBuilder : IVisualBuilder<MeasurementVisualData>
    {
        public Visual3D Build(MeasurementVisualData data)
        {
            var container = new ModelVisual3D();

            // ── Segment line ─────────────────────────────────────────────────
            var line = new LinesVisual3D
            {
                Color = Colors.Orange,
                Thickness = 3
            };
            line.Points.Add(new Point3D(data.Start.X, data.Start.Y, data.Start.Z));
            line.Points.Add(new Point3D(data.End.X, data.End.Y, data.End.Z));
            container.Children.Add(line);

            // ── Endpoint dots ────────────────────────────────────────────────
            var dots = new PointsVisual3D
            {
                Color = Colors.OrangeRed,
                Size = 6
            };
            dots.Points.Add(new Point3D(data.Start.X, data.Start.Y, data.Start.Z));
            dots.Points.Add(new Point3D(data.End.X, data.End.Y, data.End.Z));
            container.Children.Add(dots);

            // ── Distance label (BillboardText — always faces camera) ─────────
            double midX = (data.Start.X + data.End.X) / 2.0;
            double midY = (data.Start.Y + data.End.Y) / 2.0;
            double midZ = (data.Start.Z + data.End.Z) / 2.0 + 0.4; // lift above line
            string text = $"{data.Distance:F3} m";

            // Dark shadow for contrast
            var shadow = new BillboardTextVisual3D
            {
                Text = text,
                Position = new Point3D(midX + 0.05, midY - 0.05, midZ),
                FontSize = 16,
                Foreground = new SolidColorBrush(Color.FromArgb(180, 0, 0, 0))
            };
            container.Children.Add(shadow);

            // Main bright label
            var label = new BillboardTextVisual3D
            {
                Text = text,
                Position = new Point3D(midX, midY, midZ),
                FontSize = 16,
                Foreground = new SolidColorBrush(Colors.Yellow)
            };
            container.Children.Add(label);

            return container;
        }

        public Visual3D BuildBatch(IEnumerable<MeasurementVisualData> items)
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