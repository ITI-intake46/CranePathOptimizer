using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Interfaces;
using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Models;
using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Settings;
using CraneOptimizer.Domain.Entities;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using CraneOptimizer.Application.Optimization.Utilities;
using System.Linq;

namespace CraneOptimizer.Application.Optimization.GeneticAlgorithms.Engines.CraneConfiguration
{
    public record CapacityCacheKey(
        object ChartIdentity,
        double Radius,
        double BoomLength,
        double DeratingRatio,
        double SafetyFactor);

    public class CraneConfigurationEngine : ICraneConfigurationEngine
    {
        private readonly ConcurrentDictionary<CapacityCacheKey, double> _capacityCache = new();

        public void ClearCache()
        {
            _capacityCache.Clear();
        }

        public double CalculateRequiredBoomLength(double radius, double height)
        {
            return Math.Sqrt(radius * radius + height * height);
        }

        public double CraneCapacity(
            CraneSpecification craneSpec,
            double radius,
            double boomLength,
            GASettings settings)
        {
            using (new GAProfiler.ProfileScope("CraneConfigurationEngine.CraneCapacity"))
            {
                if (craneSpec == null)
                    return 0;

                object chartIdentity = craneSpec.Id != 0
                    ? (object)craneSpec.Id
                    : (object?)craneSpec.LoadChart ?? craneSpec;

                var key = new CapacityCacheKey(
                    chartIdentity,
                    radius,
                    boomLength,
                    settings.DeratingRatio,
                    settings.SafetyFactor);

                if (_capacityCache.TryGetValue(key, out double cachedCapacity))
                {
                    if (GAProfiler.Enabled)
                    {
                        System.Threading.Interlocked.Increment(ref GAProfiler.CapacityCacheHits);
                    }
                    return cachedCapacity;
                }

                if (GAProfiler.Enabled)
                {
                    System.Threading.Interlocked.Increment(ref GAProfiler.CapacityCacheMisses);
                }

                double calculatedCapacity = 0;
                if (craneSpec.LoadChart != null && craneSpec.LoadChart.Count > 0)
                {
                    double? capacity = InterpolateCapacityFromChart(craneSpec, radius, boomLength);
                    if (capacity.HasValue && capacity.Value > 0)
                    {
                        calculatedCapacity = capacity.Value * settings.DeratingRatio * settings.SafetyFactor;
                    }
                }

                _capacityCache[key] = calculatedCapacity;
                return calculatedCapacity;
            }
        }

        public bool ExceedsMaxRadius(double radius, CraneSpecification craneSpec)
        {
            return radius > craneSpec.MaxRadius;
        }

        public bool ExceedsMaxBoomLength(double boomLength, CraneSpecification craneSpec)
        {
            return boomLength > craneSpec.MaxBoomLength;
        }

        private double? InterpolateCapacityFromChart(
            CraneSpecification craneSpec,
            double radius,
            double boomLength)
        {
            // Collect all distinct radius values present in the chart
            var distinctRadii = craneSpec.LoadChart
                .Select(tp => tp.Radius)
                .Distinct()
                .OrderBy(r => r)
                .ToList();

            if (distinctRadii.Count == 0)
                return null;

            // ── Step 1: Find radius brackets ──────────────────────────
            double? lowerRadius = null;
            double? upperRadius = null;

            foreach (var r in distinctRadii)
            {
                if (r <= radius)
                    lowerRadius = r;
                else if (upperRadius == null)
                    upperRadius = r;
            }

            // No chart data covers this radius range at all
            if (lowerRadius == null && upperRadius == null)
                return null;

            // ── Step 2: Interpolate boom for each bracketing row ──────
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

            // ── Step 3: Interpolate across radius ────────────────────
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

        private double? InterpolateCapacityAlongBoom(
            IReadOnlyList<TargetPoint> radiusRow,
            double boomLength)
        {
            if (radiusRow.Count == 0)
                return null;

            // Exact match
            var exactMatch = radiusRow.FirstOrDefault(tp => tp.BoomLength == boomLength);
            if (exactMatch != null)
                return exactMatch.Capacity;

            var lower = radiusRow.LastOrDefault(tp => tp.BoomLength < boomLength);
            var higher = radiusRow.FirstOrDefault(tp => tp.BoomLength > boomLength);

            // Boom exceeds all chart entries — use maximum entry
            if (higher == null)
                return radiusRow[^1].Capacity;

            // Boom is below all chart entries — use minimum entry
            if (lower == null)
                return radiusRow[0].Capacity;

            // Standard linear interpolation
            return Interpolate(
                boomLength,
                lower.BoomLength,
                lower.Capacity,
                higher.BoomLength,
                higher.Capacity);
        }

        private double Interpolate(
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
    }
}
