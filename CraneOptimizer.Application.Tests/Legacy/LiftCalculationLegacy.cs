using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Models;
using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Settings;
using CraneOptimizer.Domain.Common;
using CraneOptimizer.Domain.Entities;
using CraneOptimizer.Domain.Schemas;
using System;
using System.Collections.Generic;
using System.Linq;

namespace CraneOptimizer.Application.Tests.Legacy
{
    /// <summary>
    /// Legacy engineering calculations preserved for Golden Master comparison.
    /// </summary>
    public static class LiftCalculationLegacy
    {
        private const double CapacityToleranceTons = 0.001;

        public static double CalculateRadius(
            DomainPoint3D cranePosition,
            PrecastElement element)
        {
            double dx = cranePosition.X - element.ElementCOG.X;
            double dy = cranePosition.Y - element.ElementCOG.Y;

            return Math.Sqrt(dx * dx + dy * dy);
        }

        public static double CalculateLiftHeight(
            PrecastElement element,
            DomainPoint3D cranePosition,
            GASettings settings)
        {
            double elementHalfHeight = (element.ElementHeight ?? 0) / 2.0;
            double relativeTopElevation = element.ElementCOG.Z + elementHalfHeight - cranePosition.Z;

            return Math.Max(0d, relativeTopElevation) + settings.WireLength;
        }

        public static double CalculateRequiredBoomLength(
            double radius,
            double height)
        {
            return Math.Sqrt(radius * radius + height * height);
        }

        public static double CraneCapacity(
            CraneSpecification craneSpec,
            double radius,
            double boomLength,
            GASettings settings)
        {
            if (craneSpec.LoadChart == null || craneSpec.LoadChart.Count == 0)
                return 0;

            double? capacity = InterpolateCapacityFromChart(craneSpec, radius, boomLength);
            if (capacity == null || capacity <= 0)
                return 0;

            return capacity.Value * settings.DeratingRatio * settings.SafetyFactor;
        }

        private static double? InterpolateCapacityFromChart(
            CraneSpecification craneSpec,
            double radius,
            double boomLength)
        {
            var distinctRadii = craneSpec.LoadChart
                .Select(tp => tp.Radius)
                .Distinct()
                .OrderBy(r => r)
                .ToList();

            if (distinctRadii.Count == 0)
                return null;

            double? lowerRadius = null;
            double? upperRadius = null;

            foreach (var r in distinctRadii)
            {
                if (r <= radius)
                    lowerRadius = r;
                else if (upperRadius == null)
                    upperRadius = r;
            }

            if (lowerRadius == null && upperRadius == null)
                return null;

            double? lowerCapacity = null;
            double? upperCapacity = null;

            if (lowerRadius.HasValue)
            {
                var lowerRow = craneSpec.LoadChart
                    .Where(tp => tp.Radius == lowerRadius.Value)
                    .OrderBy(tp => tp.BoomLength)
                    .ToList();
                lowerCapacity = InterpolateCapacityAlongBoom(lowerRow, boomLength);
            }

            if (upperRadius.HasValue)
            {
                var upperRow = craneSpec.LoadChart
                    .Where(tp => tp.Radius == upperRadius.Value)
                    .OrderBy(tp => tp.BoomLength)
                    .ToList();
                upperCapacity = InterpolateCapacityAlongBoom(upperRow, boomLength);
            }

            if (lowerRadius.HasValue && upperRadius.HasValue
                && lowerCapacity.HasValue && upperCapacity.HasValue)
            {
                double fraction = (radius - lowerRadius.Value)
                                / (upperRadius.Value - lowerRadius.Value);

                return lowerCapacity.Value + fraction * (upperCapacity.Value - lowerCapacity.Value);
            }

            if (upperCapacity.HasValue)
                return upperCapacity.Value;

            if (lowerCapacity.HasValue)
                return lowerCapacity.Value;

            return null;
        }

        private static double? InterpolateCapacityAlongBoom(
            IReadOnlyList<TargetPoint> radiusRow,
            double boomLength)
        {
            if (radiusRow.Count == 0)
                return null;

            var exactMatch = radiusRow.FirstOrDefault(tp => tp.BoomLength == boomLength);
            if (exactMatch != null)
                return exactMatch.Capacity;

            var lower = radiusRow.LastOrDefault(tp => tp.BoomLength < boomLength);
            var higher = radiusRow.FirstOrDefault(tp => tp.BoomLength > boomLength);

            if (higher == null)
                return radiusRow[^1].Capacity;

            if (lower == null)
                return radiusRow[0].Capacity;

            return Interpolate(
                boomLength,
                lower.BoomLength,
                lower.Capacity,
                higher.BoomLength,
                higher.Capacity);
        }

        private static double Interpolate(
            double value,
            double x0,
            double y0,
            double x1,
            double y1)
        {
            if (Math.Abs(x1 - x0) < double.Epsilon)
                return y0;

            double fraction = (value - x0) / (x1 - x0);
            return y0 + fraction * (y1 - y0);
        }

        public static LiftStatus DetermineLiftStatus(
            double radius,
            double boomLength,
            double requiredCapacity,
            CraneSpecification craneSpec,
            GASettings settings)
        {
            if (radius > craneSpec.MaxRadius)
                return LiftStatus.OutOfRadius;

            if (boomLength > craneSpec.MaxBoomLength)
                return LiftStatus.HeightFail;

            double available = CraneCapacity(craneSpec, radius, boomLength, settings);

            if (available <= 0)
                return LiftStatus.SpecialCase;

            if (requiredCapacity > available + CapacityToleranceTons)
                return LiftStatus.Overweight;

            return LiftStatus.Liftable;
        }
    }
}
