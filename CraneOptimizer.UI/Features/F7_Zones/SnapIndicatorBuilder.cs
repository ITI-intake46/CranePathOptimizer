using CraneOptimizer.Domain.Common;
using CraneOptimizer.UI.Core.Interfaces;
using HelixToolkit.Wpf;
using System.Windows.Media;
using System.Windows.Media.Media3D;

namespace CraneOptimizer.UI.Features.F7_Zones
{
    /// <summary>Data passed to the snap indicator builder.</summary>
    public record SnapIndicatorData(DomainPoint3D Point, SnapType Type);

    /// <summary>
    /// Renders a crosshair + small sphere at the snap location with colour-coded type:
    ///   Cyan    = point snap
    ///   Yellow  = orthogonal lock
    ///   Green   = grid snap
    /// </summary>
    public sealed class SnapIndicatorBuilder : IVisualBuilder<SnapIndicatorData>
    {
        public Visual3D Build(SnapIndicatorData data)
        {
            var container = new ModelVisual3D();

            Color color = data.Type switch
            {
                SnapType.Point => Colors.Cyan,
                SnapType.Orthogonal => Colors.Yellow,
                SnapType.Grid => Colors.LightGreen,
                _ => Colors.White
            };

            var p = data.Point;
            const double arm = 0.4;

            // Crosshair — three axes
            var cross = new LinesVisual3D { Color = color, Thickness = 3 };
            cross.Points.Add(new Point3D(p.X - arm, p.Y, p.Z));
            cross.Points.Add(new Point3D(p.X + arm, p.Y, p.Z));
            cross.Points.Add(new Point3D(p.X, p.Y - arm, p.Z));
            cross.Points.Add(new Point3D(p.X, p.Y + arm, p.Z));
            cross.Points.Add(new Point3D(p.X, p.Y, p.Z - arm * 0.5));
            cross.Points.Add(new Point3D(p.X, p.Y, p.Z + arm * 0.5));
            container.Children.Add(cross);

            // Diamond loop in XY plane
            const double d = 0.22;
            var ring = new LinesVisual3D { Color = color, Thickness = 2 };
            var diamond = new[]
            {
                new Point3D(p.X,     p.Y + d, p.Z),
                new Point3D(p.X + d, p.Y,     p.Z),
                new Point3D(p.X,     p.Y - d, p.Z),
                new Point3D(p.X - d, p.Y,     p.Z),
                new Point3D(p.X,     p.Y + d, p.Z),
            };

            for (int i = 0; i < diamond.Length - 1; i++)
            {
                ring.Points.Add(diamond[i]);
                ring.Points.Add(diamond[i + 1]);
            }

            container.Children.Add(ring);
            return container;
        }
    }
}