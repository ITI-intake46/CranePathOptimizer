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
                if (craneSpec == null || craneSpec.LoadChart == null || craneSpec.LoadChart.Count == 0)
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

                // 1. Find all load chart entries where Radius >= requiredRadius and BoomLength >= requiredBoomLength
                var validEntries = craneSpec.LoadChart
                    .Where(tp => tp.Radius >= radius && tp.BoomLength >= boomLength)
                    .ToList();

                if (validEntries.Count > 0)
                {
                    // 2. Select the most conservative nearest actual chart point:
                    //    - smallest valid Radius
                    double minRadius = validEntries.Min(tp => tp.Radius);
                    var minRadiusEntries = validEntries.Where(tp => tp.Radius == minRadius).ToList();

                    //    - then smallest valid BoomLength
                    double minBoom = minRadiusEntries.Min(tp => tp.BoomLength);
                    var finalEntry = minRadiusEntries.FirstOrDefault(tp => tp.BoomLength == minBoom);

                    if (finalEntry != null && finalEntry.Capacity > 0)
                    {
                        // 3. Return the capacity after applying derating/safety factor
                        calculatedCapacity = finalEntry.Capacity * settings.DeratingRatio * settings.SafetyFactor;
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
    }
}
