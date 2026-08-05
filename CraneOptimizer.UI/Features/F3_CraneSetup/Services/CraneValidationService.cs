using CraneOptimizer.Domain.Common;
using CraneOptimizer.Domain.Interfaces;
using CraneOptimizer.Domain.Schemas;
using CraneOptimizer.Domain.Schemas.Enums;
using CraneOptimizer.UI.Core.Interfaces;
using CraneOptimizer.UI.Core.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace CraneOptimizer.UI.Features.F3_CraneSetup.Services
{
    public class CraneValidationService : ICraneValidationService
    {
        private const double GeometryTolerance = 1e-6;

        private sealed class ZoneCache
        {
            public required GreenAreaZone Zone { get; init; }
            public required List<DomainPoint3D> Vertices { get; init; }
            public required List<(DomainPoint3D A, DomainPoint3D B)> Edges { get; init; }
            public required (double MinX, double MinY, double MaxX, double MaxY) Bounds { get; init; }
        }

        private readonly Dictionary<Guid, ZoneCache> _cache = new();
        private readonly IZoneGeometryService _geometryService;

        public CraneValidationService(IZoneGeometryService? geometryService = null)
        {
            _geometryService = geometryService ?? ZoneGeometryServiceHolder.Instance ?? new CraneOptimizer.Domain.Services.ZoneGeometryService();
        }

        public bool ValidatePlacement(SceneCrane crane, IReadOnlyList<GreenAreaZone> zones)
        {
            if (zones.Count == 0)
                return true;

            EnsureCache(zones);

            var center = crane.Position;
            double radius = CalculateConservativeFootprintRadius(crane);
            var allowedZones = _cache.Values.Where(zone => zone.Zone.Type == ZoneType.Allowed).ToList();
            var forbiddenZones = _cache.Values.Where(zone => zone.Zone.Type == ZoneType.Forbidden).ToList();

            if (allowedZones.Count == 0)
                return false;

            bool insideAllowed = false;
            foreach (var zone in allowedZones)
            {
                if (!IsPointInBounds(center, zone.Bounds))
                    continue;

                if (IsCircleInsidePolygonFast(center, radius, zone))
                {
                    insideAllowed = true;
                    break;
                }
            }

            return insideAllowed && !forbiddenZones.Any(zone => DoesCircleOverlapPolygon(center, radius, zone));
        }

        public LiftFeasibilityResult ValidateLiftFeasibility(SceneCrane crane, PrecastElement element)
        {
            var cog = element.ElementCOG;

            var pivot = crane.GetBoomPivotWorld();
            var dx = cog.X - pivot.X;
            var dy = cog.Y - pivot.Y;
            var dz = cog.Z - pivot.Z;

            var requiredRadius = Math.Sqrt(dx * dx + dy * dy);
            var requiredHeight = dz;

            var spec = crane.ActiveSpecification;
            if (spec == null)
                return new LiftFeasibilityResult(false, "No active crane specification.", requiredRadius, requiredHeight);

            if (requiredRadius > spec.MaxRadius)
                return new LiftFeasibilityResult(false, "Out of radius.", requiredRadius, requiredHeight);

            if (requiredRadius < spec.MinRadius)
                return new LiftFeasibilityResult(false, "Inside minimum radius exclusion.", requiredRadius, requiredHeight);

            if (crane.BoomLength > spec.MaxBoomLength || crane.BoomLength < spec.MinBoomLength)
                return new LiftFeasibilityResult(false, "Boom length out of specification.", requiredRadius, requiredHeight);

            // simple angle-height consistency check
            var achievableHeight = Math.Sin(crane.BoomAngle * Math.PI / 180.0) * crane.BoomLength;
            if (requiredHeight > achievableHeight + 1e-6)
                return new LiftFeasibilityResult(false, "Height not reachable with current boom kinematics.", requiredRadius, requiredHeight);

            return new LiftFeasibilityResult(true, "Feasible", requiredRadius, requiredHeight);
        }

        public double EvaluateCraneFitness(SceneCrane crane, IReadOnlyList<PrecastElement> elements, IReadOnlyList<GreenAreaZone> zones)
        {
            var placementValid = ValidatePlacement(crane, zones);
            if (!placementValid || elements.Count == 0)
                return 0;

            int feasible = 0;
            foreach (var e in elements)
            {
                if (ValidateLiftFeasibility(crane, e).IsFeasible)
                    feasible++;
            }

            var coverage = (double)feasible / elements.Count;
            return coverage;
        }

        private static double CalculateConservativeFootprintRadius(SceneCrane crane)
        {
            bool isOnRubber = string.Equals(
                crane.ActiveSpecification?.OutriggerConfig?.Trim(),
                "On Rubber",
                StringComparison.OrdinalIgnoreCase);
            double footprintLength = !isOnRubber && crane.CraneDefinition.OutriggerLength is > 0d
                ? crane.CraneDefinition.OutriggerLength.Value
                : crane.CraneDefinition.VehicleLength;
            double footprintWidth = !isOnRubber && crane.CraneDefinition.OutriggerWidth is > 0d
                ? crane.CraneDefinition.OutriggerWidth.Value
                : crane.CraneDefinition.VehicleWidth;

            if (footprintLength <= 0
                || footprintWidth <= 0
                || double.IsNaN(footprintLength)
                || double.IsNaN(footprintWidth)
                || double.IsInfinity(footprintLength)
                || double.IsInfinity(footprintWidth))
            {
                return double.PositiveInfinity;
            }

            return 0.5 * Math.Sqrt((footprintLength * footprintLength) + (footprintWidth * footprintWidth));
        }

        private void EnsureCache(IReadOnlyList<GreenAreaZone> zones)
        {
            _cache.Clear();
            foreach (var z in zones)
            {
                var id = z.ZoneId == Guid.Empty ? Guid.NewGuid() : z.ZoneId;
                var vertices = _geometryService.NormalizeVertices(z.Vertices);
                var edges = new List<(DomainPoint3D A, DomainPoint3D B)>();

                for (int i = 0; i < vertices.Count; i++)
                {
                    var a = vertices[i];
                    var b = vertices[(i + 1) % vertices.Count];
                    edges.Add((a, b));
                }

                if (vertices.Count == 0) continue;

                var minX = vertices.Min(v => v.X);
                var minY = vertices.Min(v => v.Y);
                var maxX = vertices.Max(v => v.X);
                var maxY = vertices.Max(v => v.Y);

                _cache[id] = new ZoneCache
                {
                    Zone = z,
                    Vertices = vertices,
                    Edges = edges,
                    Bounds = (minX, minY, maxX, maxY)
                };
            }
        }

        private bool IsCircleInsidePolygonFast(DomainPoint3D center, double radius, ZoneCache zone)
        {
            // Must be inside and at least radius away from every edge
            if (!IsPointInsideOrOnPolygon(center, zone.Vertices))
                return false;

            foreach (var (a, b) in zone.Edges)
            {
                var dist = DistancePointToSegment2D(center, a, b);
                if (dist + GeometryTolerance < radius)
                    return false;
            }

            return true;
        }

        private bool DoesCircleOverlapPolygon(DomainPoint3D center, double radius, ZoneCache zone)
        {
            if (IsPointInsideOrOnPolygon(center, zone.Vertices))
                return true;

            foreach (var (a, b) in zone.Edges)
            {
                double dist = DistancePointToSegment2D(center, a, b);
                if (dist <= radius + GeometryTolerance)
                    return true;
            }

            return false;
        }

        private static double DistancePointToSegment2D(DomainPoint3D p, DomainPoint3D a, DomainPoint3D b)
        {
            var vx = b.X - a.X;
            var vy = b.Y - a.Y;
            var wx = p.X - a.X;
            var wy = p.Y - a.Y;

            var c1 = vx * wx + vy * wy;
            if (c1 <= 0) return Math.Sqrt((p.X - a.X) * (p.X - a.X) + (p.Y - a.Y) * (p.Y - a.Y));

            var c2 = vx * vx + vy * vy;
            if (c2 <= c1) return Math.Sqrt((p.X - b.X) * (p.X - b.X) + (p.Y - b.Y) * (p.Y - b.Y));

            var t = c1 / c2;
            var projX = a.X + t * vx;
            var projY = a.Y + t * vy;

            var dx = p.X - projX;
            var dy = p.Y - projY;
            return Math.Sqrt(dx * dx + dy * dy);
        }

        private static bool IsPointInBounds(DomainPoint3D p, (double MinX, double MinY, double MaxX, double MaxY) b)
            => p.X >= b.MinX && p.X <= b.MaxX && p.Y >= b.MinY && p.Y <= b.MaxY;

        private bool IsPointInsideOrOnPolygon(DomainPoint3D point, IReadOnlyList<DomainPoint3D> polygon)
        {
            if (polygon == null || polygon.Count < 3)
                return false;

            for (int i = 0; i < polygon.Count; i++)
            {
                var a = polygon[i];
                var b = polygon[(i + 1) % polygon.Count];
                if (DistancePointToSegment2D(point, a, b) <= GeometryTolerance)
                    return true;
            }

            return _geometryService.PointInPolygon(point, polygon);
        }
    }
}
