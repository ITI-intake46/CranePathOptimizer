using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Services;
using CraneOptimizer.UI.Core.Interfaces;
using CraneOptimizer.UI.Core.Models;
using HelixToolkit.Wpf;
using System.Windows.Media;
using System.Windows.Media.Media3D;

namespace CraneOptimizer.UI.Features.F3_CraneSetup
{
    public class CraneRadiusBuilder : IVisualBuilder<SceneCrane>
    {
        public Visual3D Build(SceneCrane item)
        {
            var radius = CraneEnvelopeContainmentRadiusResolver.ResolveRenderedOperatingEnvelopeRadius(
                item.ActiveSpecification?.MaxRadius ?? 0);

            var center = new Point3D(item.Position.X, item.Position.Y, item.Position.Z + 0.02);

            var circle = new LinesVisual3D
            {
                Color = item.IsValidPlacement ? Colors.LimeGreen : Colors.IndianRed,
                Thickness = 2
            };

            const int segments = 96;
            var pts = new Point3DCollection();
            for (int i = 0; i <= segments; i++)
            {
                var t = 2.0 * System.Math.PI * i / segments;
                pts.Add(new Point3D(
                    center.X + radius * System.Math.Cos(t),
                    center.Y + radius * System.Math.Sin(t),
                    center.Z));
            }

            circle.Points = pts;
            circle.SetValue(System.Windows.FrameworkElement.TagProperty, item);
            return circle;
        }

        public Visual3D BuildBatch(IEnumerable<SceneCrane> items)
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
