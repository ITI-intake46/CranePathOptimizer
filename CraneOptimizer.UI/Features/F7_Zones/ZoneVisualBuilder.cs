using CraneOptimizer.Domain.Schemas;
using CraneOptimizer.Domain.Schemas.Enums;
using CraneOptimizer.UI.Core.Interfaces;
using CraneOptimizer.Domain.Interfaces;
using HelixToolkit.Wpf;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using System.Collections.Generic;
using System.Linq;
using CraneOptimizer.Domain.Common;

namespace CraneOptimizer.UI.Features.F7_Zones
{
    public class ZoneVisualBuilder : IVisualBuilder<GreenAreaZone>
    {
        private readonly ISceneService _scene;
        private readonly IZoneGeometryService _geometryService;

        public ZoneVisualBuilder(ISceneService scene, IZoneGeometryService? geometryService = null)
        {
            _scene = scene;
            _geometryService = geometryService ?? ZoneGeometryServiceHolder.Instance ?? new CraneOptimizer.Domain.Services.ZoneGeometryService();
        }

        public Visual3D Build(GreenAreaZone zone) =>
            BuildVisual(
                zone,
                renderFill: true,
                depthAdjustment: 0,
                Array.Empty<ZoneOverlayRegion>());

        private Visual3D BuildVisual(
            GreenAreaZone zone,
            bool renderFill,
            double depthAdjustment,
            IReadOnlyList<ZoneOverlayRegion> forbiddenRegions)
        {
            var normalizedVertices = _geometryService.NormalizeVertices(zone.Vertices);
            var points = normalizedVertices
                .Select(v => new Point3D(v.X, v.Y, v.Z))
                .ToList();

            if (points.Count < 3)
                return new ModelVisual3D();

            bool isSelected = _scene.SelectedZone == zone;

            var fillColor = zone.Type == ZoneType.Forbidden
                ? Color.FromRgb(176, 42, 52)
                : Color.FromRgb(24, 112, 54);
            var borderColor = zone.Type == ZoneType.Forbidden
                ? Color.FromRgb(245, 79, 88)
                : Color.FromRgb(67, 203, 103);

            var finalFillColor = isSelected ? Colors.Yellow : fillColor;
            var finalBorderColor = isSelected ? Colors.Yellow : borderColor;
            double fillOffset =
                (zone.Type == ZoneType.Forbidden
                    ? ZoneOverlayDepth.ForbiddenFill
                    : ZoneOverlayDepth.AllowedFill) +
                depthAdjustment;
            double borderOffset =
                (zone.Type == ZoneType.Forbidden
                    ? ZoneOverlayDepth.ForbiddenBorder
                    : ZoneOverlayDepth.AllowedBorder) +
                depthAdjustment;

            var border = new LinesVisual3D
            {
                Color = finalBorderColor,
                Thickness = isSelected ? 5 : 3
            };
            border.SetValue(FrameworkElement.TagProperty, zone);

            for (int i = 0; i < points.Count; i++)
            {
                var p1 = points[i];
                var p2 = points[(i + 1) % points.Count];
                List<(Point3D A, Point3D B)> visibleSegments =
                    zone.Type == ZoneType.Allowed
                        ? ZoneOverlayGeometry.ClipOutsideForbidden(
                            p1,
                            p2,
                            forbiddenRegions)
                        : new List<(Point3D, Point3D)> { (p1, p2) };
                foreach ((Point3D A, Point3D B) segment in visibleSegments)
                {
                    border.Points.Add(new Point3D(
                        segment.A.X,
                        segment.A.Y,
                        segment.A.Z + borderOffset));
                    border.Points.Add(new Point3D(
                        segment.B.X,
                        segment.B.Y,
                        segment.B.Z + borderOffset));
                }
            }

            var container = new ModelVisual3D();
            container.SetValue(FrameworkElement.TagProperty, zone);
            if (border.Points.Count > 0)
                container.Children.Add(border);

            if (!renderFill)
                return container;

            var triangleIndices = _geometryService.TriangulateSimplePolygon(normalizedVertices);
            if (triangleIndices.Count == 0)
            {
                for (int i = 1; i < points.Count - 1; i++)
                {
                    triangleIndices.Add(0);
                    triangleIndices.Add(i);
                    triangleIndices.Add(i + 1);
                }
            }

            var wpfMesh = new MeshGeometry3D();
            foreach (Point3D point in points)
                wpfMesh.Positions.Add(new Point3D(point.X, point.Y, point.Z + fillOffset));
            foreach (int index in triangleIndices)
                wpfMesh.TriangleIndices.Add(index);

            var brush = new SolidColorBrush(finalFillColor)
            {
                Opacity = isSelected
                    ? 0.62
                    : zone.Type == ZoneType.Forbidden
                        ? 0.72
                        : 0.68
            };
            var material = new DiffuseMaterial(brush);
            container.Content = new GeometryModel3D
            {
                Geometry = wpfMesh,
                Material = material,
                BackMaterial = material
            };

            return container;
        }

        public Visual3D BuildBatch(IEnumerable<GreenAreaZone> items)
        {
            List<ZoneRenderItem> prepared = items
                .Select((zone, index) =>
                {
                    List<DomainPoint3D> vertices =
                        _geometryService.NormalizeVertices(zone.Vertices);
                    return new ZoneRenderItem(
                        zone,
                        vertices,
                        Math.Abs(_geometryService.CalculateSignedArea2D(vertices)),
                        vertices.Count == 0 ? 0 : vertices.Average(point => point.Z),
                        index);
                })
                .Where(item => item.Vertices.Count >= 3)
                .ToList();

            Dictionary<GreenAreaZone, double> depthAdjustments = prepared
                .GroupBy(item => item.Zone.Type)
                .SelectMany(group => group
                    .OrderByDescending(item => item.Area)
                    .ThenBy(item => item.SourceIndex)
                    .Select((item, depthIndex) => new
                    {
                        item.Zone,
                        Adjustment = depthIndex * 0.00001
                    }))
                .ToDictionary(item => item.Zone, item => item.Adjustment);
            List<ZoneOverlayRegion> forbiddenRegions =
                ZoneOverlayGeometry.CreateRegions(prepared
                    .Where(item => item.Zone.Type == ZoneType.Forbidden)
                    .Select(item => item.Zone));

            var container = new ModelVisual3D();
            // Transparent WPF 3D content is painted in child order. Keep forbidden
            // zones last so red wins when zooming quantizes nearby surfaces together.
            foreach (ZoneRenderItem item in prepared
                         .OrderBy(item => item.Zone.Type == ZoneType.Forbidden ? 1 : 0)
                         .ThenByDescending(item => item.Area)
                         .ThenBy(item => item.SourceIndex))
            {
                bool renderFill =
                    ReferenceEquals(_scene.SelectedZone, item.Zone) ||
                    !IsFillCovered(item, prepared);
                container.Children.Add(BuildVisual(
                    item.Zone,
                    renderFill,
                    depthAdjustments[item.Zone],
                    forbiddenRegions));
            }

            return container;
        }

        private bool IsFillCovered(
            ZoneRenderItem candidate,
            IReadOnlyList<ZoneRenderItem> allItems)
        {
            foreach (ZoneRenderItem other in allItems)
            {
                if (ReferenceEquals(candidate, other) ||
                    Math.Abs(candidate.AverageZ - other.AverageZ) > 0.001)
                {
                    continue;
                }

                bool dominates =
                    other.Zone.Type == candidate.Zone.Type ||
                    other.Zone.Type == ZoneType.Forbidden &&
                    candidate.Zone.Type == ZoneType.Allowed;
                if (!dominates)
                    continue;

                double areaTolerance = Math.Max(1, candidate.Area) * 1e-8;
                if (other.Area + areaTolerance < candidate.Area)
                    continue;
                if (Math.Abs(other.Area - candidate.Area) <= areaTolerance &&
                    other.SourceIndex > candidate.SourceIndex)
                {
                    continue;
                }

                if (ContainsPolygon(other.Vertices, candidate.Vertices))
                    return true;
            }

            return false;
        }

        private bool ContainsPolygon(
            IReadOnlyList<DomainPoint3D> outer,
            IReadOnlyList<DomainPoint3D> inner)
        {
            for (int index = 0; index < inner.Count; index++)
            {
                DomainPoint3D start = inner[index];
                DomainPoint3D end = inner[(index + 1) % inner.Count];
                if (!IsInsideOrOnBoundary(start, outer))
                    return false;

                for (int sample = 1; sample <= 3; sample++)
                {
                    double ratio = sample / 4.0;
                    var point = new DomainPoint3D(
                        start.X + (end.X - start.X) * ratio,
                        start.Y + (end.Y - start.Y) * ratio,
                        start.Z + (end.Z - start.Z) * ratio);
                    if (!IsInsideOrOnBoundary(point, outer))
                        return false;
                }
            }

            return true;
        }

        private bool IsInsideOrOnBoundary(
            DomainPoint3D point,
            IReadOnlyList<DomainPoint3D> polygon)
        {
            if (_geometryService.PointInPolygon(point, polygon))
                return true;

            const double toleranceSquared = 1e-10;
            for (int index = 0; index < polygon.Count; index++)
            {
                DomainPoint3D start = polygon[index];
                DomainPoint3D end = polygon[(index + 1) % polygon.Count];
                double segmentX = end.X - start.X;
                double segmentY = end.Y - start.Y;
                double lengthSquared =
                    segmentX * segmentX +
                    segmentY * segmentY;
                if (lengthSquared <= toleranceSquared)
                    continue;

                double projection = Math.Clamp(
                    ((point.X - start.X) * segmentX +
                     (point.Y - start.Y) * segmentY) /
                    lengthSquared,
                    0,
                    1);
                double nearestX = start.X + projection * segmentX;
                double nearestY = start.Y + projection * segmentY;
                double distanceX = point.X - nearestX;
                double distanceY = point.Y - nearestY;
                if (distanceX * distanceX + distanceY * distanceY <= toleranceSquared)
                    return true;
            }

            return false;
        }

        private sealed record ZoneRenderItem(
            GreenAreaZone Zone,
            List<DomainPoint3D> Vertices,
            double Area,
            double AverageZ,
            int SourceIndex);
    }
}
