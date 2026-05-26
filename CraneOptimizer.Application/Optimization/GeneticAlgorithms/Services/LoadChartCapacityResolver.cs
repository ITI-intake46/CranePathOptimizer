using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Interfaces;
using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Models;
using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Settings;
using CraneOptimizer.Domain.Entities;
using System;
using System.Linq;

namespace CraneOptimizer.Application.Optimization.GeneticAlgorithms.Services
{
    public class LoadChartCapacityResolver : ILoadChartCapacityResolver
    {
        public LoadChartResolverResult Resolve(
            CraneSpecification craneSpec,
            double radius,
            double boomLength,
            double elementWeight,
            GASettings settings)
        {
            var result = new LoadChartResolverResult
            {
                RequiredRadius = radius,
                RequiredBoomLength = boomLength,
                ElementWeight = elementWeight,
                LookupMode = "ConservativeStep",
                ReachStatus = ReachStatus.Reachable
            };

            // 1. Validate Crane Spec & Load Chart presence
            if (craneSpec == null || craneSpec.LoadChart == null || craneSpec.LoadChart.Count == 0)
            {
                result.ChartStatus = ChartStatus.NoListedCapacity;
                result.LiftStatus = LiftStatus.NotCharted;
                result.FailureReason = CapacityFailureReason.MissingLoadChartData.ToString();
                return result;
            }

            // 2. Separate Geometric Reach from Lifting Capacity
            double maxRadius = craneSpec.MaxRadius;
            if (maxRadius <= 0 && craneSpec.LoadChart.Count > 0)
            {
                maxRadius = craneSpec.LoadChart.Max(tp => tp.Radius);
            }

            double maxBoomLength = craneSpec.MaxBoomLength;
            if (maxBoomLength <= 0 && craneSpec.LoadChart.Count > 0)
            {
                maxBoomLength = craneSpec.LoadChart.Max(tp => tp.BoomLength);
            }

            if (radius > maxRadius)
            {
                result.ReachStatus = ReachStatus.OutOfRadius;
                result.ChartStatus = ChartStatus.OutsideChartEnvelope;
                result.LiftStatus = LiftStatus.OutOfRadius; // Keep LiftStatus value for GA backward-compatibility
                result.FailureReason = CapacityFailureReason.RadiusBeyondLoadChart.ToString();
                return result;
            }

            if (boomLength > maxBoomLength)
            {
                result.ReachStatus = ReachStatus.OutOfHeight;
                result.ChartStatus = ChartStatus.OutsideChartEnvelope;
                result.LiftStatus = LiftStatus.HeightFail; // Keep LiftStatus value for GA backward-compatibility
                result.FailureReason = CapacityFailureReason.BoomBeyondLoadChart.ToString();
                return result;
            }

            // 3. Envelope Check
            // Check if required radius or boom length exceeds any entry in the entire load chart
            bool exceedsMaxRadiusInChart = !craneSpec.LoadChart.Any(tp => tp.Radius >= radius);
            bool exceedsMaxBoomInChart = !craneSpec.LoadChart.Any(tp => tp.BoomLength >= boomLength);

            if (exceedsMaxRadiusInChart || exceedsMaxBoomInChart)
            {
                result.ChartStatus = ChartStatus.OutsideChartEnvelope;
                result.LiftStatus = LiftStatus.NotCharted;
                result.FailureReason = CapacityFailureReason.OutsideChartEnvelope.ToString();
                return result;
            }

            // 4. Find valid discrete entries: tp.Radius >= radius && tp.BoomLength >= boomLength
            var validEntries = craneSpec.LoadChart
                .Where(tp => tp.Radius >= radius && tp.BoomLength >= boomLength)
                .ToList();

            if (validEntries.Count == 0)
            {
                result.ChartStatus = ChartStatus.NoListedCapacity;
                result.LiftStatus = LiftStatus.NotCharted;
                result.FailureReason = CapacityFailureReason.NoDiscreteLoadChartEntry.ToString();
                return result;
            }

            // Find conservative nearest chart point
            double minRadius = validEntries.Min(tp => tp.Radius);
            var minRadiusEntries = validEntries.Where(tp => tp.Radius == minRadius).ToList();

            double minBoom = minRadiusEntries.Min(tp => tp.BoomLength);
            var finalEntry = minRadiusEntries.FirstOrDefault(tp => tp.BoomLength == minBoom);

            if (finalEntry == null)
            {
                result.ChartStatus = ChartStatus.NoListedCapacity;
                result.LiftStatus = LiftStatus.NotCharted;
                result.FailureReason = CapacityFailureReason.NoDiscreteLoadChartEntry.ToString();
                return result;
            }

            result.SelectedChartRadius = finalEntry.Radius;
            result.SelectedChartBoomLength = finalEntry.BoomLength;
            result.IsExactMatch = Math.Abs(finalEntry.Radius - radius) < 1e-5 && Math.Abs(finalEntry.BoomLength - boomLength) < 1e-5;
            result.IsSteppedRadius = finalEntry.Radius > radius;

            // 5. Evaluate blank/zero capacity cell status
            if (finalEntry.Capacity <= -1.0)
            {
                result.IsBlankCell = true;
                result.ChartStatus = ChartStatus.InvalidBlankCell;
                result.LiftStatus = LiftStatus.NotCharted;
                result.FailureReason = CapacityFailureReason.InvalidBlankCell.ToString();
                return result;
            }

            if (finalEntry.Capacity == 0.0)
            {
                result.IsZeroCell = true;
                result.ChartStatus = ChartStatus.InvalidZeroCell;
                result.LiftStatus = LiftStatus.NotCharted;
                result.FailureReason = CapacityFailureReason.InvalidZeroCell.ToString();
                return result;
            }

            // 6. Valid Listed Capacity Found - Apply Derating & Safety Factors
            double available = finalEntry.Capacity * settings.DeratingRatio * settings.SafetyFactor;
            result.AvailableCapacity = available;
            result.SafetyMargin = available - elementWeight;
            result.ChartStatus = ChartStatus.ListedCapacityFound;

            if (elementWeight > available + 0.001)
            {
                result.LiftStatus = LiftStatus.Overweight;
                result.FailureReason = CapacityFailureReason.NoListedCapacityForBoomRadius.ToString(); // Not liftable due to overweight capacity limit
            }
            else
            {
                result.LiftStatus = LiftStatus.Liftable;
                result.FailureReason = CapacityFailureReason.None.ToString();
            }

            return result;
        }
    }
}
