using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Interfaces;
using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Settings;
using CraneOptimizer.Application.Optimization.Utilities;
using CraneOptimizer.Domain.Common;
using CraneOptimizer.Domain.Interfaces;
using CraneOptimizer.Domain.Schemas;
using System;
using System.Collections.Generic;
using System.Linq;

namespace CraneOptimizer.Application.Optimization.GeneticAlgorithms.Engines.Constraints
{
    public class ConstraintEngine : IConstraintEngine
    {
        private readonly IZoneGeometryService _geometryService;

        public ConstraintEngine(IZoneGeometryService? geometryService = null)
        {
            _geometryService = geometryService ?? ZoneGeometryServiceHolder.Instance ?? new CraneOptimizer.Domain.Services.ZoneGeometryService();
        }

        public bool IsValidSetupPoint(
            DomainPoint3D point,
            IReadOnlyList<GreenAreaZone> allowedZones,
            IReadOnlyList<GreenAreaZone> forbiddenZones,
            GASettings settings)
        {
            using (new GAProfiler.ProfileScope("ConstraintEngine.IsValidSetupPoint"))
            {
                if (allowedZones == null || allowedZones.Count == 0)
                    return false;

                // Validate and filter zones, exposing warnings for invalid ones
                var validAllowedZones = new List<GreenAreaZone>();
                foreach (var zone in allowedZones)
                {
                    if (_geometryService.ValidatePolygon(zone.Vertices, out var warning))
                    {
                        validAllowedZones.Add(zone);
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine($"[GA Warning] Allowed zone '{zone.Name}' is invalid: {warning}");
                        Console.WriteLine($"[GA Warning] Allowed zone '{zone.Name}' is invalid: {warning}");
                    }
                }

                if (validAllowedZones.Count == 0)
                    return false;

                double footprintRadius = Math.Max(0, settings.CraneFootprintRadius);
                bool fitsAllowed = validAllowedZones.Any(zone => IsCircleInsidePolygon(point, footprintRadius, _geometryService.NormalizeVertices(zone.Vertices)));

                if (!fitsAllowed)
                    return false;

                var validForbiddenZones = new List<GreenAreaZone>();
                foreach (var zone in forbiddenZones)
                {
                    if (_geometryService.ValidatePolygon(zone.Vertices, out var warning))
                    {
                        validForbiddenZones.Add(zone);
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine($"[GA Warning] Forbidden zone '{zone.Name}' is invalid: {warning}");
                        Console.WriteLine($"[GA Warning] Forbidden zone '{zone.Name}' is invalid: {warning}");
                    }
                }

                return !validForbiddenZones.Any(zone => DoesCircleOverlapPolygon(point, footprintRadius, _geometryService.NormalizeVertices(zone.Vertices)));
            }
        }

        public bool IsMeaningfullySeparated(
            DomainPoint3D candidate,
            IReadOnlyList<DomainPoint3D> existingPoints,
            GASettings settings)
        {
            using (new GAProfiler.ProfileScope("ConstraintEngine.IsMeaningfullySeparated"))
            {
                double minimumDistance = Math.Max(0, settings.MinimumRelocationDistance);
                if (minimumDistance <= 0 || existingPoints == null || existingPoints.Count == 0)
                    return true;

                return existingPoints.All(point => SpatialMath.Distance2D(candidate, point) >= minimumDistance);
            }
        }

        public List<DomainPoint3D> FilterDistinctValidPoints(
            IEnumerable<DomainPoint3D> points,
            IReadOnlyList<GreenAreaZone> allowedZones,
            IReadOnlyList<GreenAreaZone> forbiddenZones,
            GASettings settings)
        {
            using (new GAProfiler.ProfileScope("ConstraintEngine.FilterDistinctValidPoints"))
            {
                var result = new List<DomainPoint3D>();
                if (points == null)
                    return result;

                foreach (var point in points)
                {
                    if (!IsValidSetupPoint(point, allowedZones, forbiddenZones, settings))
                        continue;

                    if (!IsMeaningfullySeparated(point, result, settings))
                        continue;

                    result.Add(point);
                }

                return result;
            }
        }

        public double CalculateSimilarityPenalty(
            IReadOnlyList<DomainPoint3D> points,
            GASettings settings)
        {
            using (new GAProfiler.ProfileScope("ConstraintEngine.CalculateSimilarityPenalty"))
            {
                if (points == null || points.Count < 2)
                    return 0;

                double minimumDistance = Math.Max(0, settings.MinimumRelocationDistance);
                if (minimumDistance <= 0)
                    return 0;

                double penalty = 0;
                int pairCount = 0;

                for (int i = 0; i < points.Count; i++)
                {
                    for (int j = i + 1; j < points.Count; j++)
                    {
                        pairCount++;
                        double distance = SpatialMath.Distance2D(points[i], points[j]);
                        if (distance < minimumDistance)
                            penalty += (minimumDistance - distance) / minimumDistance;
                    }
                }

                return pairCount == 0 ? 0 : penalty / pairCount;
            }
        }

        public double CalculateNormalizedRelocationDistance(
            IReadOnlyList<DomainPoint3D> points,
            GASettings settings)
        {
            using (new GAProfiler.ProfileScope("ConstraintEngine.CalculateNormalizedRelocationDistance"))
            {
                if (points == null || points.Count < 2)
                    return 0;

                double totalDistance = 0;
                for (int i = 1; i < points.Count; i++)
                    totalDistance += SpatialMath.Distance2D(points[i - 1], points[i]);

                double normalizer = Math.Max(1, settings.MaxRadiusForNormalisation)
                    * Math.Max(1, settings.MaxCranePositions - 1);

                return Math.Min(totalDistance / normalizer, 1.0);
            }
        }

        private bool IsCircleInsidePolygon(
            DomainPoint3D center,
            double radius,
            IReadOnlyList<DomainPoint3D> polygon)
        {
            if (polygon == null || polygon.Count < 3 || !_geometryService.PointInPolygon(center, polygon))
                return false;

            if (radius <= 0)
                return true;

            for (int i = 0; i < polygon.Count; i++)
            {
                var a = polygon[i];
                var b = polygon[(i + 1) % polygon.Count];

                if (SpatialMath.PointToSegmentDistance(center, a, b) < radius)
                    return false;
            }

            return true;
        }

        private bool DoesCircleOverlapPolygon(
            DomainPoint3D center,
            double radius,
            IReadOnlyList<DomainPoint3D> polygon)
        {
            if (polygon == null || polygon.Count < 3)
                return false;

            if (_geometryService.PointInPolygon(center, polygon))
                return true;

            if (radius <= 0)
                return false;

            for (int i = 0; i < polygon.Count; i++)
            {
                var a = polygon[i];
                var b = polygon[(i + 1) % polygon.Count];

                if (SpatialMath.PointToSegmentDistance(center, a, b) < radius)
                    return true;
            }

            return false;
        }
    }
}
