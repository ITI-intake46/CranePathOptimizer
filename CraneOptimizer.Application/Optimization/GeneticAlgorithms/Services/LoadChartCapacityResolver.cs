using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Interfaces;
using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Models;
using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Settings;
using CraneOptimizer.Domain.Entities;
using System;
using System.Collections.Generic;
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

            if (index != null)
            {
                return ResolveWithIndex(result, index, radius, boomLength, elementWeight, settings, buildDiagnostics);
            }

            if (craneSpec == null || craneSpec.LoadChart == null || craneSpec.LoadChart.Count == 0)
            {
                result.ChartStatus = ChartStatus.NoListedCapacity;
                result.LiftStatus = LiftStatus.NotCharted;
                if (buildDiagnostics) result.FailureReason = CapacityFailureReason.MissingLoadChartData.ToString();
                return result;
            }

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
                result.LiftStatus = LiftStatus.OutOfRadius;
                if (buildDiagnostics) result.FailureReason = CapacityFailureReason.RadiusBeyondLoadChart.ToString();
                return result;
            }

            if (boomLength > maxBoomLength)
            {
                result.ReachStatus = ReachStatus.OutOfHeight;
                result.ChartStatus = ChartStatus.OutsideChartEnvelope;
                result.LiftStatus = LiftStatus.HeightFail;
                if (buildDiagnostics) result.FailureReason = CapacityFailureReason.BoomBeyondLoadChart.ToString();
                return result;
            }

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

            var checkedBooms = buildDiagnostics ? new List<double>() : null;
            var rejections = buildDiagnostics ? new List<string>() : null;
            TargetPoint? selectedEntry = null;
            IReadOnlyList<TargetPoint>? selectedGroupEntries = null;
            TargetPoint? bestOverweightEntry = null;
            IReadOnlyList<TargetPoint>? bestOverweightGroupEntries = null;
            double bestOverweightCapacity = -1.0;
            TargetPoint? firstCandidate = null;
            IReadOnlyList<TargetPoint>? firstCandidateGroupEntries = null;

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

                var selectableEntries = ApplyOperatingConditionConstraint(groupEntries, settings);
                if (HasOperatingConditionConstraint(settings) && selectableEntries.Count == 0)
                {
                    if (buildDiagnostics)
                        rejections!.Add($"{boom:0.#} (ConditionMismatch: {FormatOperatingConditionConstraint(settings)})");
                    continue;
                }

                firstCandidate ??= selectableEntries.Count > 0 ? selectableEntries[0] : null;
                firstCandidateGroupEntries ??= groupEntries;

                var positiveEntries = selectableEntries
                    .Where(tp => tp.Capacity > 0.0)
                    .OrderBy(tp => tp.Capacity)
                    .ThenBy(tp => tp.VisualColumnIndex ?? int.MaxValue)
                    .ToList();

                if (positiveEntries.Count == 0)
                {
                    if (buildDiagnostics)
                    {
                        var invalidEntry = selectableEntries.FirstOrDefault(tp => tp.Capacity == 0.0)
                            ?? selectableEntries.FirstOrDefault()
                            ?? groupEntries.First();
                        string status = invalidEntry.Capacity <= -1.0 ? "InvalidBlankCell" : "InvalidZeroCell";
                        rejections!.Add($"{boom:0.#} ({status})");
                    }
                    continue;
                }

                var reportingEntries = groupEntries
                    .Where(tp => tp.Capacity > 0.0)
                    .OrderBy(tp => tp.Capacity)
                    .ThenBy(tp => tp.VisualColumnIndex ?? int.MaxValue)
                    .ToList();

                var tp = positiveEntries[0];
                double available = tp.Capacity * settings.DeratingRatio * settings.SafetyFactor;
                if (available >= elementWeight)
                {
                    selectedEntry = tp;
                    selectedGroupEntries = reportingEntries.Count > 0 ? reportingEntries : positiveEntries;
                    break;
                }

                if (buildDiagnostics)
                {
                    rejections!.Add($"{boom:0.#} (Overweight: {available:0.###} < {elementWeight:0.###})");
                }

                if (available > bestOverweightCapacity)
                {
                    bestOverweightCapacity = available;
                    bestOverweightEntry = tp;
                    bestOverweightGroupEntries = reportingEntries.Count > 0 ? reportingEntries : positiveEntries;
                }
            }

            if (HasOperatingConditionConstraint(settings)
                && selectedEntry == null
                && bestOverweightEntry == null
                && firstCandidate == null)
            {
                result.ChartStatus = ChartStatus.NoListedCapacity;
                result.LiftStatus = LiftStatus.NotCharted;
                if (buildDiagnostics)
                {
                    result.FailureReason = CapacityFailureReason.LoadChartConditionMismatch.ToString();
                    result.CandidateBoomsChecked = string.Join(", ", checkedBooms!.Select(b => b.ToString("0.#")));
                    result.RejectedCandidates = string.Join(" | ", rejections!);
                }
                return result;
            }

            if (buildDiagnostics)
            {
                result.CandidateBoomsChecked = string.Join(", ", checkedBooms!.Select(b => b.ToString("0.#")));
                result.RejectedCandidates = string.Join(" | ", rejections!);
            }

            return PopulateFinalResult(
                result,
                selectedEntry,
                bestOverweightEntry,
                firstCandidate ?? candidates.First(),
                radius,
                boomLength,
                elementWeight,
                settings,
                buildDiagnostics,
                selectedGroupEntries,
                bestOverweightGroupEntries,
                firstCandidateGroupEntries);
        }

        private static LoadChartResolverResult ResolveWithIndex(
            LoadChartResolverResult result,
            LoadChartIndex index,
            double radius,
            double boomLength,
            double elementWeight,
            GASettings settings,
            bool buildDiagnostics)
        {
            if (!index.IsValid)
            {
                result.ChartStatus = ChartStatus.NoListedCapacity;
                result.LiftStatus = LiftStatus.NotCharted;
                if (buildDiagnostics) result.FailureReason = CapacityFailureReason.MissingLoadChartData.ToString();
                return result;
            }

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

            if (!index.BoomGroupsByRadius.TryGetValue(selectedChartRadius, out var boomGroups))
            {
                result.ChartStatus = ChartStatus.OutsideChartEnvelope;
                result.LiftStatus = LiftStatus.NotCharted;
                if (buildDiagnostics) result.FailureReason = CapacityFailureReason.OutsideChartEnvelope.ToString();
                return result;
            }

            var checkedBooms = buildDiagnostics ? new List<double>() : null;
            var rejections = buildDiagnostics ? new List<string>() : null;
            TargetPoint? selectedEntry = null;
            IReadOnlyList<TargetPoint>? selectedGroupEntries = null;
            TargetPoint? bestOverweightEntry = null;
            IReadOnlyList<TargetPoint>? bestOverweightGroupEntries = null;
            double bestOverweightCapacity = -1.0;
            TargetPoint? firstCandidate = null;
            IReadOnlyList<TargetPoint>? firstCandidateGroupEntries = null;
            bool hasCandidates = false;

            foreach (var group in boomGroups)
            {
                if (group.BoomLength < boomLength)
                    continue;

                hasCandidates = true;

                checkedBooms?.Add(group.BoomLength);

                var selectableEntries = ApplyOperatingConditionConstraint(group.AllEntries, settings);
                if (HasOperatingConditionConstraint(settings) && selectableEntries.Count == 0)
                {
                    if (buildDiagnostics)
                        rejections!.Add($"{group.BoomLength:0.#} (ConditionMismatch: {FormatOperatingConditionConstraint(settings)})");
                    continue;
                }

                firstCandidate ??= selectableEntries.Count > 0 ? selectableEntries[0] : null;
                firstCandidateGroupEntries ??= group.AllEntries;

                var positiveEntries = selectableEntries
                    .Where(tp => tp.Capacity > 0.0)
                    .OrderBy(tp => tp.Capacity)
                    .ThenBy(tp => tp.VisualColumnIndex ?? int.MaxValue)
                    .ToList();

                if (positiveEntries.Count == 0)
                {
                    if (buildDiagnostics)
                    {
                        var invalidEntry = selectableEntries.FirstOrDefault(tp => tp.Capacity == 0.0)
                            ?? selectableEntries.FirstOrDefault()
                            ?? group.FirstInvalidEntry;
                        string status = (invalidEntry != null && invalidEntry.Capacity <= -1.0)
                            ? "InvalidBlankCell" : "InvalidZeroCell";
                        rejections!.Add($"{group.BoomLength:0.#} ({status})");
                    }
                    continue;
                }

                var reportingEntries = group.PositiveEntries.Count > 0 ? group.PositiveEntries : positiveEntries;
                var tp = positiveEntries[0];
                double available = tp.Capacity * settings.DeratingRatio * settings.SafetyFactor;
                if (available >= elementWeight)
                {
                    selectedEntry = tp;
                    selectedGroupEntries = reportingEntries;
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
                    bestOverweightGroupEntries = reportingEntries;
                }
            }

            if (!hasCandidates)
            {
                result.ChartStatus = ChartStatus.OutsideChartEnvelope;
                result.LiftStatus = LiftStatus.NotCharted;
                if (buildDiagnostics) result.FailureReason = CapacityFailureReason.OutsideChartEnvelope.ToString();
                return result;
            }

            if (HasOperatingConditionConstraint(settings)
                && selectedEntry == null
                && bestOverweightEntry == null
                && firstCandidate == null)
            {
                result.ChartStatus = ChartStatus.NoListedCapacity;
                result.LiftStatus = LiftStatus.NotCharted;
                if (buildDiagnostics)
                {
                    result.FailureReason = CapacityFailureReason.LoadChartConditionMismatch.ToString();
                    result.CandidateBoomsChecked = string.Join(", ", checkedBooms!.Select(b => b.ToString("0.#")));
                    result.RejectedCandidates = string.Join(" | ", rejections!);
                }
                return result;
            }

            if (buildDiagnostics)
            {
                result.CandidateBoomsChecked = string.Join(", ", checkedBooms!.Select(b => b.ToString("0.#")));
                result.RejectedCandidates = string.Join(" | ", rejections!);
            }

            return PopulateFinalResult(
                result,
                selectedEntry,
                bestOverweightEntry,
                firstCandidate!,
                radius,
                boomLength,
                elementWeight,
                settings,
                buildDiagnostics,
                selectedGroupEntries,
                bestOverweightGroupEntries,
                firstCandidateGroupEntries);
        }

        private static LoadChartResolverResult PopulateFinalResult(
            LoadChartResolverResult result,
            TargetPoint? selectedEntry,
            TargetPoint? bestOverweightEntry,
            TargetPoint fallbackEntry,
            double radius,
            double boomLength,
            double elementWeight,
            GASettings settings,
            bool buildDiagnostics,
            IReadOnlyList<TargetPoint>? selectedGroupEntries,
            IReadOnlyList<TargetPoint>? bestOverweightGroupEntries,
            IReadOnlyList<TargetPoint>? fallbackGroupEntries)
        {
            TargetPoint finalEntry;

            if (selectedEntry != null)
            {
                finalEntry = selectedEntry;
                ApplySelectedLoadChartMetadata(result, finalEntry, selectedGroupEntries, buildDiagnostics);
                result.SelectedChartBoomLength = finalEntry.BoomLength;
                result.IsExactMatch = Math.Abs(finalEntry.Radius - radius) < 1e-5 && Math.Abs(finalEntry.BoomLength - boomLength) < 1e-5;
                result.IsSteppedRadius = finalEntry.Radius > radius;

                double available = finalEntry.Capacity * settings.DeratingRatio * settings.SafetyFactor;
                result.RawChartCapacity = finalEntry.Capacity;
                result.AvailableCapacity = available;
                result.SafetyMargin = available - elementWeight;
                result.ChartStatus = ChartStatus.ListedCapacityFound;
                result.LiftStatus = LiftStatus.Liftable;
                if (buildDiagnostics) result.FailureReason = CapacityFailureReason.None.ToString();
            }
            else if (bestOverweightEntry != null)
            {
                finalEntry = bestOverweightEntry;
                ApplySelectedLoadChartMetadata(result, finalEntry, bestOverweightGroupEntries, buildDiagnostics);
                result.SelectedChartBoomLength = finalEntry.BoomLength;
                result.IsExactMatch = Math.Abs(finalEntry.Radius - radius) < 1e-5 && Math.Abs(finalEntry.BoomLength - boomLength) < 1e-5;
                result.IsSteppedRadius = finalEntry.Radius > radius;

                double available = finalEntry.Capacity * settings.DeratingRatio * settings.SafetyFactor;
                result.RawChartCapacity = finalEntry.Capacity;
                result.AvailableCapacity = available;
                result.SafetyMargin = available - elementWeight;
                result.ChartStatus = ChartStatus.ListedCapacityFound;
                result.LiftStatus = LiftStatus.Overweight;
                if (buildDiagnostics) result.FailureReason = CapacityFailureReason.NoListedCapacityForBoomRadius.ToString();
            }
            else
            {
                finalEntry = fallbackEntry;
                ApplySelectedLoadChartMetadata(result, finalEntry, fallbackGroupEntries, buildDiagnostics);
                result.SelectedChartBoomLength = finalEntry.BoomLength;
                result.IsExactMatch = Math.Abs(finalEntry.Radius - radius) < 1e-5 && Math.Abs(finalEntry.BoomLength - boomLength) < 1e-5;
                result.IsSteppedRadius = finalEntry.Radius > radius;
                result.RawChartCapacity = finalEntry.Capacity;

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

        private static void ApplySelectedLoadChartMetadata(
            LoadChartResolverResult result,
            TargetPoint finalEntry,
            IReadOnlyList<TargetPoint>? groupEntries,
            bool buildDiagnostics)
        {
            result.SelectedVisualColumnIndex = finalEntry.VisualColumnIndex;
            result.SelectedVisualColumnHeader = finalEntry.VisualColumnHeader ?? string.Empty;
            result.SelectedNumberOfLines = finalEntry.NumberOfLines;
            result.SelectedOperatingConditionLabel = ResolveOperatingConditionLabel(finalEntry);

            var alternatives = (groupEntries ?? Array.Empty<TargetPoint>())
                .Where(entry => entry != null)
                .Where(entry => Math.Abs(entry.Radius - finalEntry.Radius) < 1e-5)
                .Where(entry => Math.Abs(Math.Round(entry.BoomLength, 4) - Math.Round(finalEntry.BoomLength, 4)) < 1e-5)
                .OrderBy(entry => entry.VisualColumnIndex ?? int.MaxValue)
                .ThenBy(entry => entry.Capacity)
                .ToList();

            if (alternatives.Count == 0)
            {
                alternatives.Add(finalEntry);
            }

            var positiveAlternatives = alternatives
                .Where(entry => entry.Capacity > 0.0)
                .OrderBy(entry => entry.Capacity)
                .ThenBy(entry => entry.VisualColumnIndex ?? int.MaxValue)
                .ToList();

            var reportedAlternatives = positiveAlternatives.Count > 0
                ? positiveAlternatives
                : alternatives;

            result.DuplicateAlternativeCount = reportedAlternatives.Count;
            result.IsConservativeDuplicateFallback = positiveAlternatives.Count > 1
                && Math.Abs(finalEntry.Capacity - positiveAlternatives.Min(entry => entry.Capacity)) < 1e-5;

            result.DuplicateAlternativeSummary = buildDiagnostics && reportedAlternatives.Count > 1
                ? BuildDuplicateAlternativeSummary(reportedAlternatives)
                : string.Empty;
        }

        private static bool HasOperatingConditionConstraint(GASettings settings)
        {
            return settings.LoadChartNumberOfLinesConstraint.HasValue
                || !string.IsNullOrWhiteSpace(settings.LoadChartOperatingConditionConstraint);
        }

        private static List<TargetPoint> ApplyOperatingConditionConstraint(
            IReadOnlyList<TargetPoint> entries,
            GASettings settings)
        {
            if (!HasOperatingConditionConstraint(settings))
                return entries.ToList();

            return entries
                .Where(entry => MatchesOperatingConditionConstraint(entry, settings))
                .ToList();
        }

        private static bool MatchesOperatingConditionConstraint(TargetPoint entry, GASettings settings)
        {
            if (settings.LoadChartNumberOfLinesConstraint.HasValue
                && !MatchesNumberOfLinesConstraint(entry, settings.LoadChartNumberOfLinesConstraint.Value))
            {
                return false;
            }

            if (!string.IsNullOrWhiteSpace(settings.LoadChartOperatingConditionConstraint))
            {
                string requested = NormalizeConditionText(settings.LoadChartOperatingConditionConstraint);
                string label = NormalizeConditionText(NormalizeEngineeringOperatingConditionLabel(entry.OperatingConditionLabel, entry.VisualColumnHeader));
                string header = NormalizeConditionText(RemoveGeneratedVisualDuplicateDiscriminator(entry.VisualColumnHeader));

                if (requested.Length == 0)
                    return true;

                if (!label.Contains(requested) && !header.Contains(requested))
                    return false;
            }

            return true;
        }

        private static bool MatchesNumberOfLinesConstraint(TargetPoint entry, int requestedNumberOfLines)
        {
            if (requestedNumberOfLines <= 0)
                return false;

            if (entry.NumberOfLines.HasValue)
                return entry.NumberOfLines.Value > 0 && entry.NumberOfLines.Value <= requestedNumberOfLines;

            if (TryFindNumberOfLinesInSourceText(entry.OperatingConditionLabel, out int labelLines))
                return labelLines > 0 && labelLines <= requestedNumberOfLines;

            return TryFindNumberOfLinesInSourceText(entry.VisualColumnHeader, out int headerLines)
                && headerLines > 0
                && headerLines <= requestedNumberOfLines;
        }

        private static bool TryFindNumberOfLinesInSourceText(string? value, out int numberOfLines)
        {
            numberOfLines = 0;
            if (string.IsNullOrWhiteSpace(value))
                return false;

            var matches = System.Text.RegularExpressions.Regex.Matches(
                value,
                @"(?i)(?:^|[^0-9])(?<count>[1-9][0-9]?)(?:\s*)(?:lines?|parts?|falls?|reev(?:ing)?)\b");

            foreach (System.Text.RegularExpressions.Match match in matches)
            {
                if (!int.TryParse(match.Groups["count"].Value, out int candidate))
                    continue;

                if (candidate >= 1 && candidate <= 40)
                {
                    numberOfLines = candidate;
                    return true;
                }
            }

            return false;
        }

        private static string NormalizeConditionText(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return string.Empty;

            return System.Text.RegularExpressions.Regex.Replace(value.ToLowerInvariant(), @"[^a-z0-9]+", string.Empty);
        }

        private static string FormatOperatingConditionConstraint(GASettings settings)
        {
            var parts = new List<string>();
            if (settings.LoadChartNumberOfLinesConstraint.HasValue)
                parts.Add($"up to {settings.LoadChartNumberOfLinesConstraint.Value} lines");
            if (!string.IsNullOrWhiteSpace(settings.LoadChartOperatingConditionConstraint))
                parts.Add($"condition '{settings.LoadChartOperatingConditionConstraint}'");

            return parts.Count == 0 ? "none" : string.Join(", ", parts);
        }

        private static string ResolveOperatingConditionLabel(TargetPoint entry)
        {
            string label = NormalizeEngineeringOperatingConditionLabel(entry.OperatingConditionLabel, entry.VisualColumnHeader);
            if (!string.IsNullOrWhiteSpace(label))
                return label;

            string header = RemoveGeneratedVisualDuplicateDiscriminator(entry.VisualColumnHeader);
            if (entry.NumberOfLines.HasValue && !string.IsNullOrWhiteSpace(header))
                return $"{header} / {entry.NumberOfLines.Value} lines";

            if (entry.NumberOfLines.HasValue)
                return $"{entry.NumberOfLines.Value} lines";

            return header;
        }

        private static string NormalizeEngineeringOperatingConditionLabel(string? operatingConditionLabel, string? visualColumnHeader)
        {
            if (string.IsNullOrWhiteSpace(operatingConditionLabel))
                return string.Empty;

            string clean = NormalizeConditionDisplayText(operatingConditionLabel);
            string exactHeader = NormalizeConditionDisplayText(visualColumnHeader);
            string engineeringHeader = RemoveGeneratedVisualDuplicateDiscriminator(exactHeader);

            if (!string.IsNullOrWhiteSpace(exactHeader)
                && !string.Equals(exactHeader, engineeringHeader, StringComparison.Ordinal)
                && clean.StartsWith(exactHeader, StringComparison.OrdinalIgnoreCase))
            {
                clean = engineeringHeader + clean.Substring(exactHeader.Length);
                clean = NormalizeConditionDisplayText(clean);
            }

            return clean;
        }

        private static string RemoveGeneratedVisualDuplicateDiscriminator(string? value)
        {
            string clean = NormalizeConditionDisplayText(value);
            if (string.IsNullOrWhiteSpace(clean))
                return string.Empty;

            clean = System.Text.RegularExpressions.Regex.Replace(
                clean,
                @"\s*\(([2-9]|[1-9][0-9])\)(?=\s*(/|-|,|$))",
                string.Empty);

            clean = System.Text.RegularExpressions.Regex.Replace(
                clean,
                @"_([2-9]|[1-9][0-9])(?=\s*(/|-|,|$))",
                string.Empty);

            return NormalizeConditionDisplayText(clean);
        }

        private static string NormalizeConditionDisplayText(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return string.Empty;

            string clean = value.Trim().Replace('\u00A0', ' ');
            return System.Text.RegularExpressions.Regex.Replace(clean, @"\s+", " ").Trim();
        }

        private static string BuildDuplicateAlternativeSummary(IReadOnlyList<TargetPoint> alternatives)
        {
            return string.Join(" | ", alternatives.Select(FormatDuplicateAlternative));
        }

        private static string FormatDuplicateAlternative(TargetPoint entry)
        {
            string column = entry.VisualColumnIndex.HasValue
                ? $"col {entry.VisualColumnIndex.Value}"
                : "col n/a";
            string header = string.IsNullOrWhiteSpace(entry.VisualColumnHeader)
                ? string.Empty
                : $" '{entry.VisualColumnHeader}'";
            string lines = entry.NumberOfLines.HasValue
                ? $", lines {entry.NumberOfLines.Value}"
                : string.Empty;
            string operatingConditionLabel = ResolveOperatingConditionLabel(entry);
            string condition = string.IsNullOrWhiteSpace(operatingConditionLabel)
                ? string.Empty
                : $", condition '{operatingConditionLabel}'";

            return $"{column}{header}{lines}{condition}, capacity {entry.Capacity:0.###}";
        }
    }
}
