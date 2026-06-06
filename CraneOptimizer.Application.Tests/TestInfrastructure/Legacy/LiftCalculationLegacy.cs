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
            if (craneSpec == null || craneSpec.LoadChart == null || craneSpec.LoadChart.Count == 0)
                return 0;

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

            return calculatedCapacity;
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
