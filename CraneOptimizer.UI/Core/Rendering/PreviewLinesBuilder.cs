using CraneOptimizer.Domain.Common;
using CraneOptimizer.UI.Core.Interfaces;
using HelixToolkit.Wpf;
using System.Windows.Media;
using System.Windows.Media.Media3D;

public class PreviewLinesBuilder : IVisualBuilder<List<DomainPoint3D>>
{
    public Visual3D Build(List<DomainPoint3D> points)
    {
        var lines = new LinesVisual3D
        {
            Color = Colors.Yellow,
            Thickness = 2
        };

        for (int i = 0; i < points.Count - 1; i++)
        {
            var p1 = points[i];
            var p2 = points[i + 1];

            lines.Points.Add(new Point3D(p1.X, p1.Y, p1.Z));
            lines.Points.Add(new Point3D(p2.X, p2.Y, p2.Z));
        }

        if (points.Count >= 3)
        {
            var first = points[0];
            var last = points[^1];

            lines.Points.Add(new Point3D(last.X, last.Y, last.Z));
            lines.Points.Add(new Point3D(first.X, first.Y, first.Z));
        }

        return lines;
    }
}
