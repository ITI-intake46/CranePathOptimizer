using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Interfaces;
using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Models;
using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Settings;
using CraneOptimizer.Domain.Entities;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using CraneOptimizer.Application.Optimization.Utilities;
using System.Linq;
using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Services;
using CraneOptimizer.Domain.Common;
using CraneOptimizer.Domain.Schemas;

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
        private readonly ILoadChartCapacityResolver _capacityResolver;

        public CraneConfigurationEngine() : this(null)
        {
        }

        public CraneConfigurationEngine(ILoadChartCapacityResolver? capacityResolver)
        {
            _capacityResolver = capacityResolver ?? new LoadChartCapacityResolver();
        }

        public void ClearCache()
        {
            _capacityCache.Clear();
        }

        public double CalculateRequiredBoomLength(double radius, double height)
        {
            return Math.Sqrt(radius * radius + height * height);
        }

        public double CalculateRequiredBoomLength(DomainPoint3D cranePosition, PrecastElement element, GASettings settings)
        {
            double hingeX = cranePosition.X + settings.BoomHingeOffsetX;
            double hingeY = cranePosition.Y + settings.BoomHingeOffsetY;
            double hingeZ = cranePosition.Z + settings.BoomHingeOffsetZ;

            double dx = element.ElementCOG.X - hingeX;
            double dy = element.ElementCOG.Y - hingeY;
            double dHinge = Math.Sqrt(dx * dx + dy * dy);

            double elementHalfHeight = (element.ElementHeight ?? 0) / 2.0;
            double zHook = element.ElementCOG.Z + elementHalfHeight + settings.WireLength;
            double hHinge = Math.Max(0.0, zHook - hingeZ);

            return Math.Sqrt(dHinge * dHinge + hHinge * hHinge);
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
            var resolveResult = _capacityResolver.Resolve(craneSpec, radius, boomLength, 0.0, settings);

            CapacityFailureReason reason = CapacityFailureReason.None;
            if (resolveResult.LiftStatus != LiftStatus.Liftable)
            {
                if (Enum.TryParse<CapacityFailureReason>(resolveResult.FailureReason, out var parsedReason))
                {
                    reason = parsedReason;
                }
                else
                {
                    reason = resolveResult.ChartStatus switch
                    {
                        ChartStatus.InvalidBlankCell => CapacityFailureReason.InvalidBlankCell,
                        ChartStatus.InvalidZeroCell => CapacityFailureReason.InvalidZeroCell,
                        ChartStatus.OutsideChartEnvelope => CapacityFailureReason.OutsideChartEnvelope,
                        ChartStatus.NoListedCapacity => CapacityFailureReason.NoListedCapacityForBoomRadius,
                        _ => CapacityFailureReason.MissingLoadChartData
                    };
                }
            }

return new CraneCapacityDiagnosticResult(
                resolveResult.AvailableCapacity,
                resolveResult.SelectedChartRadius,
                resolveResult.SelectedChartBoomLength,
                reason);
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
