using CraneOptimizer.Domain.Common;
using CraneOptimizer.UI.Core.Interfaces;
using HelixToolkit.Wpf;
using System.Windows.Media;
using System.Windows.Media.Media3D;

namespace CraneOptimizer.UI.Features.F7_Zones
{
    public class PreviewLinesBuilder : IVisualBuilder<List<DomainPoint3D>>
    {
        private readonly Color _lineColor;
        private readonly bool _showClosureIndicator;

        public PreviewLinesBuilder(
            Color? lineColor = null,
            bool showClosureIndicator = false)
        {
            _lineColor = lineColor ?? Colors.Yellow;
            _showClosureIndicator = showClosureIndicator;
        }

        public Visual3D Build(List<DomainPoint3D> points)
        {
            const double previewDepthOffset = ZoneOverlayDepth.LivePreview;
            var container = new ModelVisual3D();
            var lines = new LinesVisual3D
            {
                Color = _lineColor,
                Thickness = 3
            };

            for (int i = 0; i < points.Count - 1; i++)
            {
                var p1 = points[i];
                var p2 = points[i + 1];
                lines.Points.Add(new Point3D(
                    p1.X,
                    p1.Y,
                    p1.Z + previewDepthOffset));
                lines.Points.Add(new Point3D(
                    p2.X,
                    p2.Y,
                    p2.Z + previewDepthOffset));
            }

            if (points.Count >= 3)
            {
                var first = points[0];
                var last = points[^1];
                lines.Points.Add(new Point3D(
                    last.X,
                    last.Y,
                    last.Z + previewDepthOffset));
                lines.Points.Add(new Point3D(
                    first.X,
                    first.Y,
                    first.Z + previewDepthOffset));
            }

            container.Children.Add(lines);

            var vertices = new PointsVisual3D
            {
                Color = _lineColor,
                Size = 7
            };
            foreach (DomainPoint3D point in points)
            {
                vertices.Points.Add(new Point3D(
                    point.X,
                    point.Y,
                    point.Z + previewDepthOffset));
            }
            container.Children.Add(vertices);

            if (_showClosureIndicator && points.Count >= 3)
            {
                DomainPoint3D first = points[0];
                var closure = new PointsVisual3D
                {
                    Color = _lineColor,
                    Size = 12
                };
                closure.Points.Add(new Point3D(
                    first.X,
                    first.Y,
                    first.Z + previewDepthOffset));
                container.Children.Add(closure);
            }

            return container;
        }

        public Visual3D BuildBatch(IEnumerable<List<DomainPoint3D>> items)
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
