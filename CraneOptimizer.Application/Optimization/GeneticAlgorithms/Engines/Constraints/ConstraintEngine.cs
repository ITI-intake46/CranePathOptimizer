using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Interfaces;
using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Services;
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
        private const double GeometryTolerance = 1e-6;
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
                return ValidateSetupPointDetailed(point, allowedZones, forbiddenZones, settings).IsValid;
            }
        }

        public SetupPointValidationResult ValidateSetupPointDetailed(
            DomainPoint3D point,
            IReadOnlyList<GreenAreaZone> allowedZones,
            IReadOnlyList<GreenAreaZone> forbiddenZones,
            GASettings settings)
        {
            if (allowedZones == null || allowedZones.Count == 0)
                return CreateValidationResult(false, false, false, null, settings, double.PositiveInfinity);

            double footprintRadius = Math.Max(0, settings.CraneFootprintRadius);
            double requiredContainmentRadius =
                CraneEnvelopeContainmentRadiusResolver.ResolveRequiredZoneContainmentRadius(settings);

            if (!IsFinite(point.X)
                || !IsFinite(point.Y)
                || !IsFinite(point.Z)
                || !IsFinite(footprintRadius)
                || !IsFinite(requiredContainmentRadius))
            {
                return CreateValidationResult(false, false, false, null, settings, double.PositiveInfinity);
            }

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
                }
            }

            Guid? containingAllowedZoneId = null;
            double minimumAllowedBoundaryDistance = double.PositiveInfinity;
            double bestRejectedCenterInsideBoundaryDistance = double.NegativeInfinity;
            double nearestAllowedBoundaryDistance = double.PositiveInfinity;
            foreach (var zone in validAllowedZones)
            {
                var vertices = _geometryService.NormalizeVertices(zone.Vertices);
                bool centerInsideAllowed = IsPointInsideOrOnPolygon(point, vertices);
                double boundaryDistance = MinimumDistanceToPolygonBoundary(point, vertices);
                nearestAllowedBoundaryDistance = Math.Min(nearestAllowedBoundaryDistance, boundaryDistance);
                if (centerInsideAllowed)
                {
                    if (boundaryDistance > bestRejectedCenterInsideBoundaryDistance)
                        bestRejectedCenterInsideBoundaryDistance = boundaryDistance;

                    if (boundaryDistance + GeometryTolerance >= requiredContainmentRadius)
                    {
                        containingAllowedZoneId = zone.ZoneId;
                        minimumAllowedBoundaryDistance = boundaryDistance;
                        break;
                    }
                }
            }

            if (!containingAllowedZoneId.HasValue && bestRejectedCenterInsideBoundaryDistance > double.NegativeInfinity)
            {
                minimumAllowedBoundaryDistance = bestRejectedCenterInsideBoundaryDistance;
            }
            else if (!containingAllowedZoneId.HasValue)
            {
                minimumAllowedBoundaryDistance = nearestAllowedBoundaryDistance;
            }

            bool insideAllowed = containingAllowedZoneId.HasValue;
            if (!insideAllowed)
            {
                var rejected = CreateValidationResult(
                    false,
                    false,
                    false,
                    null,
                    settings,
                    minimumAllowedBoundaryDistance);
                WriteZoneCheckDiagnostics(point, rejected);
                return rejected;
            }

            bool forbiddenOverlap = false;
            if (forbiddenZones != null)
            {
                foreach (var zone in forbiddenZones)
                {
                    if (!_geometryService.ValidatePolygon(zone.Vertices, out var warning))
                    {
                        System.Diagnostics.Debug.WriteLine($"[GA Warning] Forbidden zone '{zone.Name}' is invalid: {warning}");
                        continue;
                    }

                    if (DoesCircleOverlapPolygon(point, requiredContainmentRadius, _geometryService.NormalizeVertices(zone.Vertices)))
                    {
                        forbiddenOverlap = true;
                        break;
                    }
                }
            }

            var result = CreateValidationResult(
                insideAllowed && !forbiddenOverlap,
                insideAllowed,
                forbiddenOverlap,
                containingAllowedZoneId,
                settings,
                minimumAllowedBoundaryDistance);
            WriteZoneCheckDiagnostics(point, result);
            return result;
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

        private bool DoesCircleOverlapPolygon(
            DomainPoint3D center,
            double radius,
            IReadOnlyList<DomainPoint3D> polygon)
        {
            if (polygon == null || polygon.Count < 3)
                return false;

            if (IsPointInsideOrOnPolygon(center, polygon))
                return true;

            if (radius <= 0)
                return false;

            for (int i = 0; i < polygon.Count; i++)
            {
                var a = polygon[i];
                var b = polygon[(i + 1) % polygon.Count];

                if (SpatialMath.PointToSegmentDistance(center, a, b) <= radius + GeometryTolerance)
                    return true;
            }

            return false;
        }

        private bool IsPointInsideOrOnPolygon(DomainPoint3D point, IReadOnlyList<DomainPoint3D> polygon)
        {
            return MinimumDistanceToPolygonBoundary(point, polygon) <= GeometryTolerance
                || _geometryService.PointInPolygon(point, polygon);
        }

        private static double MinimumDistanceToPolygonBoundary(DomainPoint3D point, IReadOnlyList<DomainPoint3D> polygon)
        {
            if (polygon == null || polygon.Count < 2)
                return double.PositiveInfinity;

            double minDistance = double.PositiveInfinity;
            for (int i = 0; i < polygon.Count; i++)
            {
                var a = polygon[i];
                var b = polygon[(i + 1) % polygon.Count];
                minDistance = Math.Min(minDistance, SpatialMath.PointToSegmentDistance(point, a, b));
            }

            return minDistance;
        }

        private static bool IsFinite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);

        private static SetupPointValidationResult CreateValidationResult(
            bool isValid,
            bool insideAllowed,
            bool forbiddenOverlap,
            Guid? allowedZoneId,
            GASettings settings,
            double minimumAllowedBoundaryDistance)
        {
            double groundFootprintRadius = Math.Max(0, settings.CraneFootprintRadius);
            double requiredContainmentRadius =
                CraneEnvelopeContainmentRadiusResolver.ResolveRequiredZoneContainmentRadius(settings);

            return new SetupPointValidationResult(
                isValid,
                insideAllowed,
                forbiddenOverlap,
                allowedZoneId,
                groundFootprintRadius,
                requiredContainmentRadius,
                minimumAllowedBoundaryDistance);
        }

        private static void WriteZoneCheckDiagnostics(
            DomainPoint3D point,
            SetupPointValidationResult validation)
        {
            System.Diagnostics.Debug.WriteLine(
                "[GA_ZONE_CHECK] " +
                $"Position=({point.X:F4},{point.Y:F4},{point.Z:F4}) " +
                $"AllowedZoneId={(validation.AllowedZoneId.HasValue ? validation.AllowedZoneId.Value.ToString() : string.Empty)} " +
                $"GroundFootprintRadius={validation.GroundFootprintRadius:F4} " +
                $"RequiredContainmentRadius={validation.RequiredContainmentRadius:F4} " +
                $"MinimumAllowedBoundaryDistance={FormatDistance(validation.MinimumAllowedBoundaryDistance)} " +
                $"InsideAllowed={validation.InsideAllowed} " +
                $"ForbiddenOverlap={validation.ForbiddenOverlap} " +
                $"Valid={validation.IsValid}");
        }

        private static string FormatDistance(double distance) =>
            double.IsInfinity(distance) ? "Infinity" : distance.ToString("F4");
    }

    public sealed class SetupPointValidationResult
    {
        public SetupPointValidationResult(
            bool isValid,
            bool insideAllowed,
            bool forbiddenOverlap,
            Guid? allowedZoneId)
            : this(isValid, insideAllowed, forbiddenOverlap, allowedZoneId, 0, 0, double.PositiveInfinity)
        {
        }

        public SetupPointValidationResult(
            bool isValid,
            bool insideAllowed,
            bool forbiddenOverlap,
            Guid? allowedZoneId,
            double groundFootprintRadius,
            double requiredContainmentRadius,
            double minimumAllowedBoundaryDistance)
        {
            IsValid = isValid;
            InsideAllowed = insideAllowed;
            ForbiddenOverlap = forbiddenOverlap;
            AllowedZoneId = allowedZoneId;
            GroundFootprintRadius = groundFootprintRadius;
            RequiredContainmentRadius = requiredContainmentRadius;
            MinimumAllowedBoundaryDistance = minimumAllowedBoundaryDistance;
        }

        public bool IsValid { get; }
        public bool InsideAllowed { get; }
        public bool ForbiddenOverlap { get; }
        public Guid? AllowedZoneId { get; }
        public double GroundFootprintRadius { get; }
        public double RequiredContainmentRadius { get; }
        public double MinimumAllowedBoundaryDistance { get; }
    }
}
