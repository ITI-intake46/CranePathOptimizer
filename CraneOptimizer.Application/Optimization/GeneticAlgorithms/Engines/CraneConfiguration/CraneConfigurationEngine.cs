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

                var diagResult = EvaluateCraneCapacityDiagnosticInternal(craneSpec, radius, boomLength, settings);
                double calculatedCapacity = diagResult.Capacity;

                _capacityCache[key] = calculatedCapacity;
                return calculatedCapacity;
            }
        }

        public CraneCapacityDiagnosticResult EvaluateCraneCapacityDiagnostic(
            CraneSpecification craneSpec,
            double radius,
            double boomLength,
            GASettings settings)
        {
            using (new GAProfiler.ProfileScope("CraneConfigurationEngine.EvaluateCraneCapacityDiagnostic"))
            {
                return EvaluateCraneCapacityDiagnosticInternal(craneSpec, radius, boomLength, settings);
            }
        }

        private CraneCapacityDiagnosticResult EvaluateCraneCapacityDiagnosticInternal(
            CraneSpecification craneSpec,
            double radius,
            double boomLength,
            GASettings settings)
        {
            if (craneSpec == null || craneSpec.LoadChart == null || craneSpec.LoadChart.Count == 0)
            {
                return new CraneCapacityDiagnosticResult(0, 0, 0, CapacityFailureReason.MissingLoadChartData);
            }

            // Check if radius exceeds all radii in load chart
            bool hasValidRadius = craneSpec.LoadChart.Any(tp => tp.Radius >= radius);
            if (!hasValidRadius)
            {
                return new CraneCapacityDiagnosticResult(0, 0, 0, CapacityFailureReason.RadiusBeyondLoadChart);
            }

            // Check if any entry satisfies both Radius >= radius and BoomLength >= boomLength
            var validEntries = craneSpec.LoadChart
                .Where(tp => tp.Radius >= radius && tp.BoomLength >= boomLength)
                .ToList();

            if (validEntries.Count == 0)
            {
                // Check if any entry with BoomLength >= boomLength exists at all in the load chart
                bool hasAnyValidBoom = craneSpec.LoadChart.Any(tp => tp.BoomLength >= boomLength);
                if (!hasAnyValidBoom)
                {
                    return new CraneCapacityDiagnosticResult(0, 0, 0, CapacityFailureReason.BoomBeyondLoadChart);
                }
                else
                {
                    return new CraneCapacityDiagnosticResult(0, 0, 0, CapacityFailureReason.NoDiscreteLoadChartEntry);
                }
            }

            // Find the most conservative nearest actual chart point
            double minRadius = validEntries.Min(tp => tp.Radius);
            var minRadiusEntries = validEntries.Where(tp => tp.Radius == minRadius).ToList();

            double minBoom = minRadiusEntries.Min(tp => tp.BoomLength);
            var finalEntry = minRadiusEntries.FirstOrDefault(tp => tp.BoomLength == minBoom);

            if (finalEntry == null)
            {
                return new CraneCapacityDiagnosticResult(0, minRadius, minBoom, CapacityFailureReason.NoDiscreteLoadChartEntry);
            }

            if (finalEntry.Capacity <= 0)
            {
                return new CraneCapacityDiagnosticResult(0, finalEntry.Radius, finalEntry.BoomLength, CapacityFailureReason.ZeroCapacityChartEntry);
            }

            double calculatedCapacity = finalEntry.Capacity * settings.DeratingRatio * settings.SafetyFactor;
            return new CraneCapacityDiagnosticResult(calculatedCapacity, finalEntry.Radius, finalEntry.BoomLength, CapacityFailureReason.None);
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
