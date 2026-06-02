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
            GASettings settings,
            bool buildDiagnostics = true,
            LoadChartIndex? index = null)
        {
            var result = new LoadChartResolverResult
            {
                RequiredRadius = radius,
                RequiredBoomLength = boomLength,
                ElementWeight = elementWeight,
                LookupMode = "ConservativeStep",
                ReachStatus = ReachStatus.Reachable
            };

            // Use pre-indexed path when available
            if (index != null)
            {
                return ResolveWithIndex(result, index, radius, boomLength, elementWeight, settings, buildDiagnostics);
            }

            // ── Legacy path (unchanged logic, diagnostics gated) ──

            // 1. Validate Crane Spec & Load Chart presence
            if (craneSpec == null || craneSpec.LoadChart == null || craneSpec.LoadChart.Count == 0)
            {
                result.ChartStatus = ChartStatus.NoListedCapacity;
                result.LiftStatus = LiftStatus.NotCharted;
                if (buildDiagnostics) result.FailureReason = CapacityFailureReason.MissingLoadChartData.ToString();
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
                if (buildDiagnostics) result.FailureReason = CapacityFailureReason.RadiusBeyondLoadChart.ToString();
                return result;
            }

            if (boomLength > maxBoomLength)
            {
                result.ReachStatus = ReachStatus.OutOfHeight;
                result.ChartStatus = ChartStatus.OutsideChartEnvelope;
                result.LiftStatus = LiftStatus.HeightFail; // Keep LiftStatus value for GA backward-compatibility
                if (buildDiagnostics) result.FailureReason = CapacityFailureReason.BoomBeyondLoadChart.ToString();
                return result;
            }

            // 3. Envelope Check
            var radiusEntries = craneSpec.LoadChart.Where(tp => tp.Radius >= radius).ToList();
            if (radiusEntries.Count == 0)
            {
                result.ChartStatus = ChartStatus.OutsideChartEnvelope;
                result.LiftStatus = LiftStatus.NotCharted;
                if (buildDiagnostics) result.FailureReason = CapacityFailureReason.OutsideChartEnvelope.ToString();
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
                if (buildDiagnostics) result.FailureReason = CapacityFailureReason.OutsideChartEnvelope.ToString();
                return result;
            }

            // 4. Candidate Selection Loop
            var checkedBooms = buildDiagnostics ? new System.Collections.Generic.List<double>() : null;
            var rejections = buildDiagnostics ? new System.Collections.Generic.List<string>() : null;
            TargetPoint? selectedEntry = null;
            TargetPoint? bestOverweightEntry = null;
            double bestOverweightCapacity = -1.0;

            // Full-fidelity PDF charts can preserve repeated visual columns with the same numeric boom length.
            // Those duplicate columns may represent manufacturer-specific conditions such as line count,
            // counterweight, operating mode, or region. Resolve each numeric boom group conservatively by
            // using the minimum positive capacity in the duplicate group instead of an optimistic envelope.
            var candidateGroups = candidates
                .GroupBy(tp => Math.Round(tp.BoomLength, 4))
                .OrderBy(group => group.Min(tp => tp.BoomLength));

            foreach (var group in candidateGroups)
            {
                var groupEntries = group
                    .OrderBy(tp => tp.VisualColumnIndex ?? int.MaxValue)
                    .ThenBy(tp => tp.Capacity)
                    .ToList();

                double boom = groupEntries.Min(tp => tp.BoomLength);
                checkedBooms?.Add(boom);

                var positiveEntries = groupEntries
                    .Where(tp => tp.Capacity > 0.0)
                    .OrderBy(tp => tp.Capacity)
                    .ThenBy(tp => tp.VisualColumnIndex ?? int.MaxValue)
                    .ToList();

                if (positiveEntries.Count == 0)
                {
                    if (buildDiagnostics)
                    {
                        var invalidEntry = groupEntries.FirstOrDefault(tp => tp.Capacity == 0.0)
                            ?? groupEntries.First();
                        string status = invalidEntry.Capacity <= -1.0 ? "InvalidBlankCell" : "InvalidZeroCell";
                        rejections!.Add($"{boom:0.#} ({status})");
                    }
                    continue;
                }

                var tp = positiveEntries[0];
                double available = tp.Capacity * settings.DeratingRatio * settings.SafetyFactor;
                if (available >= elementWeight)
                {
                    selectedEntry = tp;
                    break; // Shortest compliant boom selected after conservative duplicate-column grouping.
                }

                if (buildDiagnostics)
                {
                    rejections!.Add($"{boom:0.#} (Overweight: {available:0.###} < {elementWeight:0.###})");
                }
                if (available > bestOverweightCapacity)
                {
                    bestOverweightCapacity = available;
                    bestOverweightEntry = tp;
                }
            }

            if (buildDiagnostics)
            {
                result.CandidateBoomsChecked = string.Join(", ", checkedBooms!.Select(b => b.ToString("0.#")));
                result.RejectedCandidates = string.Join(" | ", rejections!);
            }

            return PopulateFinalResult(result, selectedEntry, bestOverweightEntry, candidates.First(),
                radius, boomLength, elementWeight, settings, buildDiagnostics);
        }

        // ══════════════════════════════════════════════════════════════
        // INDEXED PATH — uses pre-built LoadChartIndex
        // ══════════════════════════════════════════════════════════════

        private static LoadChartResolverResult ResolveWithIndex(
            LoadChartResolverResult result,
            LoadChartIndex index,
            double radius,
            double boomLength,
            double elementWeight,
            GASettings settings,
            bool buildDiagnostics)
        {
            // 1. Validate index
            if (!index.IsValid)
            {
                result.ChartStatus = ChartStatus.NoListedCapacity;
                result.LiftStatus = LiftStatus.NotCharted;
                if (buildDiagnostics) result.FailureReason = CapacityFailureReason.MissingLoadChartData.ToString();
                return result;
            }

            // 2. Geometric reach checks
            if (radius > index.MaxRadius)
            {
                result.ReachStatus = ReachStatus.OutOfRadius;
                result.ChartStatus = ChartStatus.OutsideChartEnvelope;
                result.LiftStatus = LiftStatus.OutOfRadius;
                if (buildDiagnostics) result.FailureReason = CapacityFailureReason.RadiusBeyondLoadChart.ToString();
                return result;
            }

            if (boomLength > index.MaxBoomLength)
            {
                result.ReachStatus = ReachStatus.OutOfHeight;
                result.ChartStatus = ChartStatus.OutsideChartEnvelope;
                result.LiftStatus = LiftStatus.HeightFail;
                if (buildDiagnostics) result.FailureReason = CapacityFailureReason.BoomBeyondLoadChart.ToString();
                return result;
            }

            // 3. Binary search for smallest chart radius >= required
            int radiusIdx = index.FindFirstRadiusIndex(radius);
            if (radiusIdx < 0)
            {
                result.ChartStatus = ChartStatus.OutsideChartEnvelope;
                result.LiftStatus = LiftStatus.NotCharted;
                if (buildDiagnostics) result.FailureReason = CapacityFailureReason.OutsideChartEnvelope.ToString();
                return result;
            }

            double selectedChartRadius = index.SortedRadii[radiusIdx];
            result.SelectedChartRadius = selectedChartRadius;

            // 4. Get pre-grouped boom configurations at selected radius
            if (!index.BoomGroupsByRadius.TryGetValue(selectedChartRadius, out var boomGroups))
            {
                result.ChartStatus = ChartStatus.OutsideChartEnvelope;
                result.LiftStatus = LiftStatus.NotCharted;
                if (buildDiagnostics) result.FailureReason = CapacityFailureReason.OutsideChartEnvelope.ToString();
                return result;
            }

            // 5. Candidate selection loop — iterate pre-sorted boom groups
            var checkedBooms = buildDiagnostics ? new System.Collections.Generic.List<double>() : null;
            var rejections = buildDiagnostics ? new System.Collections.Generic.List<string>() : null;
            TargetPoint? selectedEntry = null;
            TargetPoint? bestOverweightEntry = null;
            double bestOverweightCapacity = -1.0;
            TargetPoint? firstCandidate = null;
            bool hasCandidates = false;

            foreach (var group in boomGroups)
            {
                // Skip boom groups shorter than required
                if (group.BoomLength < boomLength)
                    continue;

                hasCandidates = true;
                if (firstCandidate == null)
                {
                    firstCandidate = group.AllEntries.Count > 0 ? group.AllEntries[0] : null;
                }

                checkedBooms?.Add(group.BoomLength);

                if (!group.HasPositiveCapacity)
                {
                    if (buildDiagnostics)
                    {
                        var invalidEntry = group.FirstInvalidEntry;
                        string status = (invalidEntry != null && invalidEntry.Capacity <= -1.0)
                            ? "InvalidBlankCell" : "InvalidZeroCell";
                        rejections!.Add($"{group.BoomLength:0.#} ({status})");
                    }
                    continue;
                }

                var tp = group.MinPositiveEntry!;
                double available = tp.Capacity * settings.DeratingRatio * settings.SafetyFactor;
                if (available >= elementWeight)
                {
                    selectedEntry = tp;
                    break;
                }

                if (buildDiagnostics)
                {
                    rejections!.Add($"{group.BoomLength:0.#} (Overweight: {available:0.###} < {elementWeight:0.###})");
                }
                if (available > bestOverweightCapacity)
                {
                    bestOverweightCapacity = available;
                    bestOverweightEntry = tp;
                }
            }

            if (!hasCandidates)
            {
                result.ChartStatus = ChartStatus.OutsideChartEnvelope;
                result.LiftStatus = LiftStatus.NotCharted;
                if (buildDiagnostics) result.FailureReason = CapacityFailureReason.OutsideChartEnvelope.ToString();
                return result;
            }

            if (buildDiagnostics)
            {
                result.CandidateBoomsChecked = string.Join(", ", checkedBooms!.Select(b => b.ToString("0.#")));
                result.RejectedCandidates = string.Join(" | ", rejections!);
            }

            return PopulateFinalResult(result, selectedEntry, bestOverweightEntry, firstCandidate!,
                radius, boomLength, elementWeight, settings, buildDiagnostics);
        }

        // ══════════════════════════════════════════════════════════════
        // SHARED FINAL RESULT POPULATION
        // ══════════════════════════════════════════════════════════════

        private static LoadChartResolverResult PopulateFinalResult(
            LoadChartResolverResult result,
            TargetPoint? selectedEntry,
            TargetPoint? bestOverweightEntry,
            TargetPoint fallbackEntry,
            double radius,
            double boomLength,
            double elementWeight,
            GASettings settings,
            bool buildDiagnostics)
        {
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
                if (buildDiagnostics) result.FailureReason = CapacityFailureReason.None.ToString();
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
                if (buildDiagnostics) result.FailureReason = CapacityFailureReason.NoListedCapacityForBoomRadius.ToString();
            }
            else
            {
                // Case C: All candidates are blank/zero
                finalEntry = fallbackEntry;
                result.SelectedChartBoomLength = finalEntry.BoomLength;
                result.IsExactMatch = Math.Abs(finalEntry.Radius - radius) < 1e-5 && Math.Abs(finalEntry.BoomLength - boomLength) < 1e-5;
                result.IsSteppedRadius = finalEntry.Radius > radius;

                if (finalEntry.Capacity <= -1.0)
                {
                    result.IsBlankCell = true;
                    result.ChartStatus = ChartStatus.InvalidBlankCell;
                    result.LiftStatus = LiftStatus.NotCharted;
                    if (buildDiagnostics) result.FailureReason = CapacityFailureReason.InvalidBlankCell.ToString();
                }
                else
                {
                    result.IsZeroCell = true;
                    result.ChartStatus = ChartStatus.InvalidZeroCell;
                    result.LiftStatus = LiftStatus.NotCharted;
                    if (buildDiagnostics) result.FailureReason = CapacityFailureReason.InvalidZeroCell.ToString();
                }
            }

            return result;
        }
    }
}
