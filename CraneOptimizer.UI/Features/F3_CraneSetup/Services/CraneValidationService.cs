using CraneOptimizer.Domain.Common;
using CraneOptimizer.Domain.Interfaces;
using CraneOptimizer.Domain.Schemas;
using CraneOptimizer.UI.Core.Interfaces;
using CraneOptimizer.UI.Core.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace CraneOptimizer.UI.Features.F3_CraneSetup.Services
{
    public class CraneValidationService : ICraneValidationService
    {
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
            var halfL = crane.CraneDefinition.VehicleLength / 2.0;
            var halfW = crane.CraneDefinition.VehicleWidth / 2.0;

            var footprint = new[]
            {
                RotateAndTranslate(-halfL, -halfW, crane.Rotation, center),
                RotateAndTranslate( halfL, -halfW, crane.Rotation, center),
                RotateAndTranslate( halfL,  halfW, crane.Rotation, center),
                RotateAndTranslate(-halfL,  halfW, crane.Rotation, center)
            };

            var radius = crane.ActiveSpecification?.MaxRadius ?? 0;

            foreach (var zone in _cache.Values)
            {
                if (!IsPointInBounds(center, zone.Bounds))
                    continue;

                var allFootprintInside = footprint.All(p => _geometryService.PointInPolygon(p, zone.Vertices));
                if (!allFootprintInside)
                    continue;

                if (radius > 0 && !IsCircleInsidePolygonFast(center, radius, zone))
                    continue;

                return true;
            }

            return false;
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
            if (!_geometryService.PointInPolygon(center, zone.Vertices))
                return false;

            foreach (var (a, b) in zone.Edges)
            {
                var dist = DistancePointToSegment2D(center, a, b);
                if (dist < radius)
                    return false;
            }

            return true;
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

        private static DomainPoint3D RotateAndTranslate(double x, double y, double angleDeg, DomainPoint3D c)
        {
            var a = angleDeg * Math.PI / 180.0;
            var cos = Math.Cos(a);
            var sin = Math.Sin(a);
            var rx = x * cos - y * sin;
            var ry = x * sin + y * cos;

            return new DomainPoint3D(c.X + rx, c.Y + ry, c.Z);
        }
    }
}
