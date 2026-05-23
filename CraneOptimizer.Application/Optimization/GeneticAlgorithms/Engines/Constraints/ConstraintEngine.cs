using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Interfaces;
using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Services;
using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Settings;
using CraneOptimizer.Application.Optimization.Utilities;
using CraneOptimizer.Domain.Common;
using CraneOptimizer.Domain.Schemas;
using System;
using System.Collections.Generic;
using System.Linq;

namespace CraneOptimizer.Application.Optimization.GeneticAlgorithms.Engines.Constraints
{
    public class ConstraintEngine : IConstraintEngine
    {
        private const double ComparisonTolerance = 1e-7;

        public bool IsValidSetupPoint(
            DomainPoint3D point,
            IReadOnlyList<GreenAreaZone> allowedZones,
            IReadOnlyList<GreenAreaZone> forbiddenZones,
            GASettings settings)
        {
            bool result = ExecuteIsValidSetupPoint(point, allowedZones, forbiddenZones, settings);

#if DEBUG
            bool legacyResult = CranePlacementConstraintServiceLegacy.IsValidSetupPoint(point, allowedZones, forbiddenZones, settings);
            if (result != legacyResult)
                throw new InvalidOperationException($"IsValidSetupPoint mismatch! New: {result}, Legacy: {legacyResult}");
#endif

            return result;
        }

        private bool ExecuteIsValidSetupPoint(
            DomainPoint3D point,
            IReadOnlyList<GreenAreaZone> allowedZones,
            IReadOnlyList<GreenAreaZone> forbiddenZones,
            GASettings settings)
        {
            if (allowedZones == null || allowedZones.Count == 0)
                return false;

            double footprintRadius = Math.Max(0, settings.CraneFootprintRadius);
            bool fitsAllowed = allowedZones.Any(zone => IsCircleInsidePolygon(point, footprintRadius, zone.Vertices));

            if (!fitsAllowed)
                return false;

            return !forbiddenZones.Any(zone => DoesCircleOverlapPolygon(point, footprintRadius, zone.Vertices));
        }

        public bool IsMeaningfullySeparated(
            DomainPoint3D candidate,
            IReadOnlyList<DomainPoint3D> existingPoints,
            GASettings settings)
        {
            bool result = ExecuteIsMeaningfullySeparated(candidate, existingPoints, settings);

#if DEBUG
            bool legacyResult = CranePlacementConstraintServiceLegacy.IsMeaningfullySeparated(candidate, existingPoints, settings);
            if (result != legacyResult)
                throw new InvalidOperationException($"IsMeaningfullySeparated mismatch! New: {result}, Legacy: {legacyResult}");
#endif

            return result;
        }

        private bool ExecuteIsMeaningfullySeparated(
            DomainPoint3D candidate,
            IReadOnlyList<DomainPoint3D> existingPoints,
            GASettings settings)
        {
            double minimumDistance = Math.Max(0, settings.MinimumRelocationDistance);
            if (minimumDistance <= 0 || existingPoints == null || existingPoints.Count == 0)
                return true;

            return existingPoints.All(point => SpatialMath.Distance2D(candidate, point) >= minimumDistance);
        }

        public List<DomainPoint3D> FilterDistinctValidPoints(
            IEnumerable<DomainPoint3D> points,
            IReadOnlyList<GreenAreaZone> allowedZones,
            IReadOnlyList<GreenAreaZone> forbiddenZones,
            GASettings settings)
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

#if DEBUG
            var legacyResult = CranePlacementConstraintServiceLegacy.FilterDistinctValidPoints(points, allowedZones, forbiddenZones, settings);
            if (result.Count != legacyResult.Count)
                throw new InvalidOperationException($"FilterDistinctValidPoints mismatch on count! New: {result.Count}, Legacy: {legacyResult.Count}");
            for (int i = 0; i < result.Count; i++)
            {
                if (SpatialMath.Distance2D(result[i], legacyResult[i]) > ComparisonTolerance)
                    throw new InvalidOperationException($"FilterDistinctValidPoints mismatch at index {i}!");
            }
#endif

            return result;
        }

        public double CalculateSimilarityPenalty(
            IReadOnlyList<DomainPoint3D> points,
            GASettings settings)
        {
            double result = ExecuteCalculateSimilarityPenalty(points, settings);

#if DEBUG
            double legacyResult = CranePlacementConstraintServiceLegacy.CalculateSimilarityPenalty(points, settings);
            if (Math.Abs(result - legacyResult) > ComparisonTolerance)
                throw new InvalidOperationException($"CalculateSimilarityPenalty mismatch! New: {result}, Legacy: {legacyResult}");
#endif

            return result;
        }

        private double ExecuteCalculateSimilarityPenalty(
            IReadOnlyList<DomainPoint3D> points,
            GASettings settings)
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

        public double CalculateNormalizedRelocationDistance(
            IReadOnlyList<DomainPoint3D> points,
            GASettings settings)
        {
            double result = ExecuteCalculateNormalizedRelocationDistance(points, settings);

#if DEBUG
            double legacyResult = CranePlacementConstraintServiceLegacy.CalculateNormalizedRelocationDistance(points, settings);
            if (Math.Abs(result - legacyResult) > ComparisonTolerance)
                throw new InvalidOperationException($"CalculateNormalizedRelocationDistance mismatch! New: {result}, Legacy: {legacyResult}");
#endif

            return result;
        }

        private double ExecuteCalculateNormalizedRelocationDistance(
            IReadOnlyList<DomainPoint3D> points,
            GASettings settings)
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

        private bool IsCircleInsidePolygon(
            DomainPoint3D center,
            double radius,
            IReadOnlyList<DomainPoint3D> polygon)
        {
            if (polygon == null || polygon.Count < 3 || !SpatialMath.PointInPolygon(center, polygon))
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

            if (SpatialMath.PointInPolygon(center, polygon))
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
