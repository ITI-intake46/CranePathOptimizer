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
            var radiusEntries = craneSpec.LoadChart.Where(tp => tp.Radius >= radius).ToList();
            if (radiusEntries.Count == 0)
            {
                result.ChartStatus = ChartStatus.OutsideChartEnvelope;
                result.LiftStatus = LiftStatus.NotCharted;
                result.FailureReason = CapacityFailureReason.OutsideChartEnvelope.ToString();
                return result;
            }

            double selectedChartRadius = radiusEntries.Min(tp => tp.Radius);
            result.SelectedChartRadius = selectedChartRadius;

            // Gather all boom configurations >= RequiredBoomLength at selectedChartRadius, sorted ascending
            var candidates = craneSpec.LoadChart
                .Where(tp => tp.Radius == selectedChartRadius && tp.BoomLength >= boomLength)
                .OrderBy(tp => tp.BoomLength)
                .ToList();

            if (candidates.Count == 0)
            {
                result.ChartStatus = ChartStatus.OutsideChartEnvelope;
                result.LiftStatus = LiftStatus.NotCharted;
                result.FailureReason = CapacityFailureReason.OutsideChartEnvelope.ToString();
                return result;
            }

            // 4. Candidate Selection Loop
            var checkedBooms = new System.Collections.Generic.List<double>();
            var rejections = new System.Collections.Generic.List<string>();
            TargetPoint? selectedEntry = null;
            TargetPoint? bestOverweightEntry = null;
            double bestOverweightCapacity = -1.0;

            foreach (var tp in candidates)
            {
                checkedBooms.Add(tp.BoomLength);

                if (tp.Capacity <= -1.0)
                {
                    rejections.Add($"{tp.BoomLength:0.#} (InvalidBlankCell)");
                    continue;
                }
                if (tp.Capacity == 0.0)
                {
                    rejections.Add($"{tp.BoomLength:0.#} (InvalidZeroCell)");
                    continue;
                }

                // Positive capacity candidate found!
                double available = tp.Capacity * settings.DeratingRatio * settings.SafetyFactor;
                if (available >= elementWeight)
                {
                    selectedEntry = tp;
                    break; // Shortest compliant boom selected!
                }
                else
                {
                    rejections.Add($"{tp.BoomLength:0.#} (Overweight: {available:0.###} < {elementWeight:0.###})");
                    if (available > bestOverweightCapacity)
                    {
                        bestOverweightCapacity = available;
                        bestOverweightEntry = tp;
                    }
                }
            }

            result.CandidateBoomsChecked = string.Join(", ", checkedBooms.Select(b => b.ToString("0.#")));
            result.RejectedCandidates = string.Join(" | ", rejections);

            TargetPoint finalEntry;

            if (selectedEntry != null)
            {
                // Case A: Shortest Compliant Boom Selected
                finalEntry = selectedEntry;
                result.SelectedChartBoomLength = finalEntry.BoomLength;
                result.IsExactMatch = Math.Abs(finalEntry.Radius - radius) < 1e-5 && Math.Abs(finalEntry.BoomLength - boomLength) < 1e-5;
                result.IsSteppedRadius = finalEntry.Radius > radius;

                double available = finalEntry.Capacity * settings.DeratingRatio * settings.SafetyFactor;
                result.AvailableCapacity = available;
                result.SafetyMargin = available - elementWeight;
                result.ChartStatus = ChartStatus.ListedCapacityFound;
                result.LiftStatus = LiftStatus.Liftable;
                result.FailureReason = CapacityFailureReason.None.ToString();
            }
            else if (bestOverweightEntry != null)
            {
                // Case B: No compliant boom, but there are overweight positive capacity candidates
                finalEntry = bestOverweightEntry;
                result.SelectedChartBoomLength = finalEntry.BoomLength;
                result.IsExactMatch = Math.Abs(finalEntry.Radius - radius) < 1e-5 && Math.Abs(finalEntry.BoomLength - boomLength) < 1e-5;
                result.IsSteppedRadius = finalEntry.Radius > radius;

                double available = finalEntry.Capacity * settings.DeratingRatio * settings.SafetyFactor;
                result.AvailableCapacity = available;
                result.SafetyMargin = available - elementWeight;
                result.ChartStatus = ChartStatus.ListedCapacityFound;
                result.LiftStatus = LiftStatus.Overweight;
                result.FailureReason = CapacityFailureReason.NoListedCapacityForBoomRadius.ToString();
            }
            else
            {
                // Case C: All candidates are blank/zero
                finalEntry = candidates.First();
                result.SelectedChartBoomLength = finalEntry.BoomLength;
                result.IsExactMatch = Math.Abs(finalEntry.Radius - radius) < 1e-5 && Math.Abs(finalEntry.BoomLength - boomLength) < 1e-5;
                result.IsSteppedRadius = finalEntry.Radius > radius;

                if (finalEntry.Capacity <= -1.0)
                {
                    result.IsBlankCell = true;
                    result.ChartStatus = ChartStatus.InvalidBlankCell;
                    result.LiftStatus = LiftStatus.NotCharted;
                    result.FailureReason = CapacityFailureReason.InvalidBlankCell.ToString();
                }
                else
                {
                    result.IsZeroCell = true;
                    result.ChartStatus = ChartStatus.InvalidZeroCell;
                    result.LiftStatus = LiftStatus.NotCharted;
                    result.FailureReason = CapacityFailureReason.InvalidZeroCell.ToString();
                }
            }

            return result;
        }
    }
}
