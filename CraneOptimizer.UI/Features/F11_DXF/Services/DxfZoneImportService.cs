using CraneOptimizer.Domain.Common;
using CraneOptimizer.Domain.Interfaces;
using CraneOptimizer.UI.Features.F11_DXF.DTOs;
using netDxf;
using netDxf.Entities;
using System.Windows.Media.Media3D;

namespace CraneOptimizer.UI.Features.F11_DXF.Services
{
    public sealed class DxfZoneImportService : IDxfZoneImportService
    {
        private const int CurveSegments = 72;
        private const double BulgeTolerance = 1e-9;

        private readonly IZoneGeometryService _geometryService;

        public DxfZoneImportService(IZoneGeometryService geometryService)
        {
            _geometryService = geometryService;
        }

        public IReadOnlyList<DxfImportLayerSummary> GetLayerSummaries(
            DxfDocument document,
            Matrix3D transform,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(document);
            cancellationToken.ThrowIfCancellationRequested();

            var entityCounts = CountEntitiesByLayer(document, cancellationToken);
            var summaries = new List<DxfImportLayerSummary>();

            foreach (var layer in document.Layers.OrderBy(layer => layer.Name, StringComparer.OrdinalIgnoreCase))
            {
                cancellationToken.ThrowIfCancellationRequested();
                var candidates = GetCandidates(document, layer.Name, transform, cancellationToken);
                summaries.Add(new DxfImportLayerSummary(
                    layer.Name,
                    ResolveLayerColorHex(document, layer.Name, layer.Color),
                    entityCounts.TryGetValue(layer.Name, out var count) ? count : 0,
                    candidates.Count(candidate => candidate.IsValid),
                    candidates.Count(candidate => !candidate.IsValid)));
            }

            return summaries;
        }

        public IReadOnlyList<DxfZoneCandidate> GetCandidates(
            DxfDocument document,
            string layerName,
            Matrix3D transform,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(document);
            cancellationToken.ThrowIfCancellationRequested();
            if (string.IsNullOrWhiteSpace(layerName))
                return Array.Empty<DxfZoneCandidate>();

            var candidates = new List<DxfZoneCandidate>();

            foreach (var polyline in document.Entities.Polylines2D)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (!string.Equals(polyline.Layer.Name, layerName, StringComparison.OrdinalIgnoreCase))
                    continue;

                candidates.Add(BuildPolylineCandidate(polyline, transform, cancellationToken));
            }

            foreach (var circle in document.Entities.Circles)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (!string.Equals(circle.Layer.Name, layerName, StringComparison.OrdinalIgnoreCase))
                    continue;

                candidates.Add(BuildCircleCandidate(circle, transform, cancellationToken));
            }

            foreach (var ellipse in document.Entities.Ellipses)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (!string.Equals(ellipse.Layer.Name, layerName, StringComparison.OrdinalIgnoreCase))
                    continue;

                candidates.Add(BuildEllipseCandidate(ellipse, transform, cancellationToken));
            }

            candidates.AddRange(BuildLineLoopCandidates(document, layerName, transform, cancellationToken));

            return candidates;
        }

        private DxfZoneCandidate BuildPolylineCandidate(
            Polyline2D polyline,
            Matrix3D transform,
            CancellationToken cancellationToken)
        {
            var issues = new List<string>();
            var transformed = TessellatePolyline(polyline, transform, cancellationToken);

            cancellationToken.ThrowIfCancellationRequested();
            var normalized = _geometryService.NormalizeVertices(transformed);
            var area = 0.0;
            var isValid = true;

            if (!polyline.IsClosed)
            {
                issues.Add("Polyline is open.");
                isValid = false;
            }

            if (isValid && !_geometryService.ValidatePolygon(normalized, out var warning))
            {
                issues.Add(warning ?? "Invalid polygon.");
                isValid = false;
            }

            if (isValid)
                area = Math.Abs(_geometryService.CalculateSignedArea2D(normalized));

            return new DxfZoneCandidate(
                Guid.NewGuid(),
                polyline.Layer.Name,
                normalized,
                area,
                "Polyline2D",
                isValid,
                issues);
        }

        private DxfZoneCandidate BuildCircleCandidate(
            Circle circle,
            Matrix3D transform,
            CancellationToken cancellationToken)
        {
            var vertices = Enumerable.Range(0, CurveSegments)
                .Select(index =>
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var angle = 2.0 * Math.PI * index / CurveSegments;
                    return Transform(
                        circle.Center.X + circle.Radius * Math.Cos(angle),
                        circle.Center.Y + circle.Radius * Math.Sin(angle),
                        circle.Center.Z,
                        transform);
                })
                .ToList();

            return BuildClosedCandidate(circle.Layer.Name, vertices, "Circle");
        }

        private DxfZoneCandidate BuildEllipseCandidate(
            Ellipse ellipse,
            Matrix3D transform,
            CancellationToken cancellationToken)
        {
            if (!ellipse.IsFullEllipse)
            {
                return new DxfZoneCandidate(
                    Guid.NewGuid(),
                    ellipse.Layer.Name,
                    Array.Empty<DomainPoint3D>(),
                    0,
                    "Ellipse",
                    false,
                    new[] { "Ellipse arc is open; only full closed ellipses can be imported as zones." });
            }

            var majorRadius = Math.Abs(ellipse.MajorAxis) / 2.0;
            var minorRadius = Math.Abs(ellipse.MinorAxis) / 2.0;
            var rotation = DegreesToRadians(ellipse.Rotation);
            var cosRotation = Math.Cos(rotation);
            var sinRotation = Math.Sin(rotation);
            var vertices = Enumerable.Range(0, CurveSegments)
                .Select(index =>
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var angle = 2.0 * Math.PI * index / CurveSegments;
                    var localX = majorRadius * Math.Cos(angle);
                    var localY = minorRadius * Math.Sin(angle);
                    return Transform(
                        ellipse.Center.X + localX * cosRotation - localY * sinRotation,
                        ellipse.Center.Y + localX * sinRotation + localY * cosRotation,
                        ellipse.Center.Z,
                        transform);
                })
                .ToList();

            return BuildClosedCandidate(ellipse.Layer.Name, vertices, "Ellipse");
        }

        private DxfZoneCandidate BuildClosedCandidate(string layerName, IReadOnlyList<DomainPoint3D> vertices, string sourceEntityType)
        {
            var normalized = _geometryService.NormalizeVertices(vertices);
            var issues = new List<string>();
            var area = 0.0;
            var isValid = true;

            if (!_geometryService.ValidatePolygon(normalized, out var warning))
            {
                issues.Add(warning ?? "Invalid polygon.");
                isValid = false;
            }

            if (isValid)
                area = Math.Abs(_geometryService.CalculateSignedArea2D(normalized));

            return new DxfZoneCandidate(
                Guid.NewGuid(),
                layerName,
                normalized,
                area,
                sourceEntityType,
                isValid,
                issues);
        }

        private static IReadOnlyList<DomainPoint3D> TessellatePolyline(
            Polyline2D polyline,
            Matrix3D transform,
            CancellationToken cancellationToken)
        {
            if (polyline.Vertexes.Count == 0)
                return Array.Empty<DomainPoint3D>();

            var vertices = new List<DomainPoint3D>();
            var segmentCount = polyline.IsClosed ? polyline.Vertexes.Count : polyline.Vertexes.Count - 1;

            for (var i = 0; i < segmentCount; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var current = polyline.Vertexes[i];
                var next = polyline.Vertexes[(i + 1) % polyline.Vertexes.Count];

                vertices.Add(Transform(current.Position.X, current.Position.Y, polyline.Elevation, transform));
                if (Math.Abs(current.Bulge) > BulgeTolerance)
                    AddBulgePoints(
                        vertices,
                        current,
                        next,
                        polyline.Elevation,
                        transform,
                        cancellationToken);
            }

            if (!polyline.IsClosed)
            {
                var last = polyline.Vertexes[^1];
                vertices.Add(Transform(last.Position.X, last.Position.Y, polyline.Elevation, transform));
            }

            return vertices;
        }

        private static void AddBulgePoints(
            List<DomainPoint3D> vertices,
            Polyline2DVertex current,
            Polyline2DVertex next,
            double elevation,
            Matrix3D transform,
            CancellationToken cancellationToken)
        {
            var x1 = current.Position.X;
            var y1 = current.Position.Y;
            var x2 = next.Position.X;
            var y2 = next.Position.Y;
            var dx = x2 - x1;
            var dy = y2 - y1;
            var chord = Math.Sqrt(dx * dx + dy * dy);

            if (chord <= BulgeTolerance)
                return;

            var theta = 4.0 * Math.Atan(current.Bulge);
            var radius = chord / (2.0 * Math.Sin(Math.Abs(theta) / 2.0));
            var midX = (x1 + x2) / 2.0;
            var midY = (y1 + y2) / 2.0;
            var normalX = -dy / chord;
            var normalY = dx / chord;
            var centerDistance = radius * Math.Cos(Math.Abs(theta) / 2.0);
            var sign = Math.Sign(current.Bulge);
            var centerX = midX + sign * centerDistance * normalX;
            var centerY = midY + sign * centerDistance * normalY;
            var startAngle = Math.Atan2(y1 - centerY, x1 - centerX);
            var segments = Math.Max(2, (int)Math.Ceiling(Math.Abs(theta) / (2.0 * Math.PI) * CurveSegments));

            for (var step = 1; step < segments; step++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var angle = startAngle + theta * step / segments;
                vertices.Add(Transform(
                    centerX + radius * Math.Cos(angle),
                    centerY + radius * Math.Sin(angle),
                    elevation,
                    transform));
            }
        }

        private IReadOnlyList<DxfZoneCandidate> BuildLineLoopCandidates(
            DxfDocument document,
            string layerName,
            Matrix3D transform,
            CancellationToken cancellationToken)
        {
            var segments = new List<LineSegment>();
            foreach (var line in document.Entities.Lines)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (!string.Equals(line.Layer.Name, layerName, StringComparison.OrdinalIgnoreCase))
                    continue;
                var segment = new LineSegment(
                    Transform(line.StartPoint.X, line.StartPoint.Y, line.StartPoint.Z, transform),
                    Transform(line.EndPoint.X, line.EndPoint.Y, line.EndPoint.Z, transform));
                if (!Near(segment.Start, segment.End))
                    segments.Add(segment);
            }

            var candidates = new List<DxfZoneCandidate>();
            var used = new bool[segments.Count];

            for (var i = 0; i < segments.Count; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (used[i]) continue;

                var chain = new List<DomainPoint3D> { segments[i].Start, segments[i].End };
                used[i] = true;

                var grew = true;
                while (grew)
                {
                    grew = false;
                    for (var j = 0; j < segments.Count; j++)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        if (used[j]) continue;

                        var end = chain[^1];
                        if (Near(end, segments[j].Start))
                        {
                            chain.Add(segments[j].End);
                            used[j] = true;
                            grew = true;
                            break;
                        }

                        if (Near(end, segments[j].End))
                        {
                            chain.Add(segments[j].Start);
                            used[j] = true;
                            grew = true;
                            break;
                        }
                    }
                }

                if (chain.Count >= 4 && Near(chain[0], chain[^1]))
                    candidates.Add(BuildLineLoopCandidate(layerName, chain));
            }

            return candidates;
        }

        private DxfZoneCandidate BuildLineLoopCandidate(string layerName, List<DomainPoint3D> closedChain)
        {
            var normalized = _geometryService.NormalizeVertices(closedChain);
            var issues = new List<string>();
            var area = 0.0;
            var isValid = true;

            if (!_geometryService.ValidatePolygon(normalized, out var warning))
            {
                issues.Add(warning ?? "Invalid line loop polygon.");
                isValid = false;
            }

            if (isValid)
                area = Math.Abs(_geometryService.CalculateSignedArea2D(normalized));

            return new DxfZoneCandidate(
                Guid.NewGuid(),
                layerName,
                normalized,
                area,
                "LineLoop",
                isValid,
                issues);
        }

        private static DomainPoint3D Transform(double x, double y, double z, Matrix3D transform)
        {
            var point = transform.Transform(new Point3D(x, y, z));
            return new DomainPoint3D(point.X, point.Y, point.Z);
        }

        private static double DegreesToRadians(double degrees) => degrees * Math.PI / 180.0;

        private static bool Near(DomainPoint3D a, DomainPoint3D b)
        {
            const double tolerance = 1e-6;
            return Math.Abs(a.X - b.X) <= tolerance
                && Math.Abs(a.Y - b.Y) <= tolerance
                && Math.Abs(a.Z - b.Z) <= tolerance;
        }

        private readonly record struct LineSegment(DomainPoint3D Start, DomainPoint3D End);

        private static Dictionary<string, int> CountEntitiesByLayer(
            DxfDocument document,
            CancellationToken cancellationToken)
        {
            var counts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

            void Count(string layerName)
            {
                cancellationToken.ThrowIfCancellationRequested();
                Inc(counts, layerName);
            }

            foreach (var entity in document.Entities.Lines) Count(entity.Layer.Name);
            foreach (var entity in document.Entities.Polylines2D) Count(entity.Layer.Name);
            foreach (var entity in document.Entities.Polylines3D) Count(entity.Layer.Name);
            foreach (var entity in document.Entities.Circles) Count(entity.Layer.Name);
            foreach (var entity in document.Entities.Arcs) Count(entity.Layer.Name);
            foreach (var entity in document.Entities.Ellipses) Count(entity.Layer.Name);
            foreach (var entity in document.Entities.Splines) Count(entity.Layer.Name);
            foreach (var entity in document.Entities.Texts) Count(entity.Layer.Name);
            foreach (var entity in document.Entities.MTexts) Count(entity.Layer.Name);
            foreach (var entity in document.Entities.Inserts) Count(entity.Layer.Name);

            return counts;
        }

        private static void Inc(Dictionary<string, int> counts, string layerName)
        {
            counts.TryGetValue(layerName, out var current);
            counts[layerName] = current + 1;
        }

        private static string ResolveLayerColorHex(DxfDocument document, string layerName, AciColor layerColor)
        {
            var layerHex = AciToHex(layerColor);
            if (layerHex != "#A0A0A0")
                return layerHex;

            foreach (var entityColor in GetEntityColors(document, layerName))
            {
                var entityHex = AciToHex(entityColor);
                if (entityHex != "#A0A0A0")
                    return entityHex;
            }

            return layerHex;
        }

        private static IEnumerable<AciColor> GetEntityColors(DxfDocument document, string layerName)
        {
            foreach (var entity in document.Entities.Lines.Where(entity => IsLayer(entity.Layer.Name, layerName))) yield return entity.Color;
            foreach (var entity in document.Entities.Polylines2D.Where(entity => IsLayer(entity.Layer.Name, layerName))) yield return entity.Color;
            foreach (var entity in document.Entities.Polylines3D.Where(entity => IsLayer(entity.Layer.Name, layerName))) yield return entity.Color;
            foreach (var entity in document.Entities.Circles.Where(entity => IsLayer(entity.Layer.Name, layerName))) yield return entity.Color;
            foreach (var entity in document.Entities.Arcs.Where(entity => IsLayer(entity.Layer.Name, layerName))) yield return entity.Color;
            foreach (var entity in document.Entities.Ellipses.Where(entity => IsLayer(entity.Layer.Name, layerName))) yield return entity.Color;
            foreach (var entity in document.Entities.Splines.Where(entity => IsLayer(entity.Layer.Name, layerName))) yield return entity.Color;
        }

        private static bool IsLayer(string candidateLayerName, string layerName) =>
            string.Equals(candidateLayerName, layerName, StringComparison.OrdinalIgnoreCase);

        private static string AciToHex(AciColor color)
        {
            try
            {
                if (!color.IsByLayer && !color.IsByBlock)
                    return $"#{color.R:X2}{color.G:X2}{color.B:X2}";
            }
            catch
            {
            }

            return "#A0A0A0";
        }
    }
}
