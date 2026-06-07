using CraneOptimizer.Application.Optimization.GeneticAlgorithms.DTOs;
using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Interfaces;
using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Models;
using CraneOptimizer.Application.Optimization.Utilities;
using CraneOptimizer.Domain.Common;
using CraneOptimizer.Domain.Diagnostics;
using CraneOptimizer.Domain.Schemas;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace CraneOptimizer.Application.Optimization.GeneticAlgorithms.Services
{
    internal sealed class BenchmarkV2CsvWriter
    {
        private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;
        private static readonly object CsvLock = new();
        private readonly IReachabilityMapService? _reachabilityMapService;

        public BenchmarkV2CsvWriter(IReachabilityMapService? reachabilityMapService)
        {
            _reachabilityMapService = reachabilityMapService;
        }

        public void Write(
            string reportsDirectory,
            string timestamp,
            PlacementInput input,
            PlacementResult result,
            bool includeResearchFiles = true)
        {
            BenchmarkDiagnosticsIdentity.Ensure(input);

            List<AssignmentMetric> assignments = BuildAssignmentMetrics(input, result);
            GAProfiler.StageLogEntry? stageLog = GAProfiler.StageLogs
                .LastOrDefault(stage => stage.StageRunId == input.Settings.StageRunId)
                ?? GAProfiler.StageLogs.LastOrDefault();

            // Summary-level files (always emitted for Summary and Research)
            WriteRunSummary(reportsDirectory, timestamp, input, result, stageLog);
            WriteSettingsSnapshot(reportsDirectory, timestamp, input);
            WriteStageSummary(reportsDirectory, timestamp, input, result, stageLog);
            WritePositions(reportsDirectory, timestamp, input, result);
            WriteElementAssignments(reportsDirectory, timestamp, input, assignments);

            // Research-only files
            if (includeResearchFiles)
            {
                WriteGenerationSummary(reportsDirectory, timestamp, input);
                WriteReachabilityHotSpots(reportsDirectory, timestamp, input);
                WriteProjectFeatures(reportsDirectory, timestamp, input);
                WriteSolutionQualityMetrics(reportsDirectory, timestamp, input, result, assignments, stageLog);
                WriteZoneReachabilitySummary(reportsDirectory, timestamp, input);
            }
        }

        private static void WriteRunSummary(
            string directory,
            string timestamp,
            PlacementInput input,
            PlacementResult result,
            GAProfiler.StageLogEntry? stageLog)
        {
            string[] header =
            {
                "OptimizationRunId", "StageRunId", "StageIndex", "DiagnosticsSchemaVersion",
                "Timestamp", "ProjectFeatureSnapshotId", "ProjectId", "ProjectName", "ScenarioId",
                "IfcFileName", "CraneId", "CraneName", "CraneSpecificationId", "RawElementCount",
                "OptimizationScopeElementCount", "GaReadyElementCount", "LiftedElementCount",
                "CoverageRatio", "CoveragePercent", "FitnessScore", "PositionCount",
                "MaxCranePositions", "RuntimeMs", "TerminationReason"
            };

            string[] row =
            {
                input.Settings.OptimizationRunId,
                input.Settings.StageRunId,
                input.Settings.StageIndex.ToString(Invariant),
                BenchmarkDiagnosticsIdentity.SchemaVersion,
                timestamp,
                input.ProjectFeatureSnapshotId ?? "N/A",
                input.ProjectId ?? "N/A",
                input.ProjectName ?? "N/A",
                input.ScenarioId ?? "N/A",
                input.IfcFileName ?? "N/A",
                GetCraneId(input),
                input.CraneSpec?.Crane?.Name ?? "N/A",
                GetCraneSpecificationId(input),
                input.RawElementCount.ToString(Invariant),
                input.OptimizationScopeElementCount.ToString(Invariant),
                (input.Elements?.Count ?? 0).ToString(Invariant),
                result.LiftedElementCount.ToString(Invariant),
                Format6(result.CoverageRatio),
                Format4(result.CoverageRatio * 100.0),
                Format4(result.FitnessScore),
                result.Positions.Count.ToString(Invariant),
                input.Settings.MaxCranePositions.ToString(Invariant),
                Format2(stageLog?.RuntimeMs ?? GAProfiler.GetElapsedTimeMs("GAEngine.Run")),
                ResolveTerminationReason(input, stageLog)
            };

            Append(Path.Combine(directory, "ga_run_summary.csv"), header, row);
        }

        private static void WriteSettingsSnapshot(
            string directory,
            string timestamp,
            PlacementInput input)
        {
            string[] header =
            {
                "OptimizationRunId", "StageRunId", "StageIndex", "DiagnosticsSchemaVersion",
                "Timestamp", "ProjectFeatureSnapshotId", "ProjectId", "ProjectName", "ScenarioId",
                "IfcFileName", "CraneId", "CraneName", "CraneSpecificationId",
                "CraneMaxRadius", "CraneMinRadius", "CraneMaxBoomLength", "CraneMinBoomLength",
                "CraneMaxCapacity", "CraneFootprintRadius", "RawElementCount",
                "OptimizationScopeElementCount", "GaReadyElementCount", "TargetElementTypes",
                "PopulationSize", "MaxGenerations", "MutationRate", "MutationSigma", "CrossoverRate",
                "RandomSeed", "GridStep", "MaxCranePositions", "RequestedMaxCranePositions",
                "EarlyStoppingGenerations", "ConvergenceThresholdPercent", "PositionSamplingAttempts",
                "EnableAdaptiveSigma", "SigmaCoolingExponent", "MinimumMutationSigma",
                "MutationExplorationMultiplier", "EnableConcurrentOptimization",
                "EnableReachabilitySeeding", "HotSpotSeedingRatio", "EnableMultiStageDiscovery",
                "MultiStageCoverageTarget", "AdaptiveRelocationScaleFactor", "UseHungarianAssignment",
                "CoverageWeight", "CoverageDominanceMultiplier", "RelocationWeight",
                "RadiusPenaltyWeight", "RadiusQualityPenaltyWeight", "OverweightPenaltyWeight",
                "HeightPenaltyWeight", "RadiusStdDevPenaltyWeight", "BalancePenaltyWeight",
                "SimilarPositionPenaltyWeight", "RelocationDistancePenaltyWeight", "DeratingRatio",
                "SafetyFactor", "WireLength", "AdditionalWt", "MinimumRelocationDistance",
                "DuplicatePositionDistance", "MaxRadiusForNormalisation", "SlewOffsetX", "SlewOffsetY",
                "SlewOffsetZ", "BoomHingeOffsetX", "BoomHingeOffsetY", "BoomHingeOffsetZ",
                "EnableProfiling", "ProfilingLogDirectory", "ProjectFolderPath",
                "DiagnosticsReportsDirectory"
            };

            string[] row =
            {
                input.Settings.OptimizationRunId,
                input.Settings.StageRunId,
                input.Settings.StageIndex.ToString(Invariant),
                BenchmarkDiagnosticsIdentity.SchemaVersion,
                timestamp,
                input.ProjectFeatureSnapshotId ?? "N/A",
                input.ProjectId ?? "N/A",
                input.ProjectName ?? "N/A",
                input.ScenarioId ?? "N/A",
                input.IfcFileName ?? "N/A",
                GetCraneId(input),
                input.CraneSpec?.Crane?.Name ?? "N/A",
                GetCraneSpecificationId(input),
                Format4(input.CraneSpec?.MaxRadius ?? 0),
                Format4(input.CraneSpec?.MinRadius ?? 0),
                Format4(input.CraneSpec?.MaxBoomLength ?? 0),
                Format4(input.CraneSpec?.MinBoomLength ?? 0),
                Format4(input.CraneSpec?.MaxCapacity ?? 0),
                Format4(input.Settings.CraneFootprintRadius),
                input.RawElementCount.ToString(Invariant),
                input.OptimizationScopeElementCount.ToString(Invariant),
                (input.Elements?.Count ?? 0).ToString(Invariant),
                input.Settings.TargetElementTypes == null
                    ? "N/A"
                    : string.Join(";", input.Settings.TargetElementTypes.OrderBy(value => value)),
                input.Settings.PopulationSize.ToString(Invariant),
                input.Settings.MaxGenerations.ToString(Invariant),
                Format4(input.Settings.MutationRate),
                Format4(input.Settings.MutationSigma),
                Format4(input.Settings.CrossoverRate),
                input.Settings.RandomSeed?.ToString(Invariant) ?? "N/A",
                Format4(input.Settings.GridStep),
                input.Settings.MaxCranePositions.ToString(Invariant),
                input.RequestedMaxCranePositions.ToString(Invariant),
                input.Settings.EarlyStoppingGenerations.ToString(Invariant),
                Format4(input.Settings.ConvergenceThresholdPercent),
                input.Settings.PositionSamplingAttempts.ToString(Invariant),
                FormatBool(input.Settings.EnableAdaptiveSigma),
                Format4(input.Settings.SigmaCoolingExponent),
                Format4(input.Settings.MinimumMutationSigma),
                Format4(input.Settings.MutationExplorationMultiplier),
                FormatBool(input.Settings.EnableConcurrentOptimization),
                FormatBool(input.Settings.EnableReachabilitySeeding),
                Format4(input.Settings.HotSpotSeedingRatio),
                FormatBool(input.Settings.EnableMultiStageDiscovery),
                Format4(input.Settings.MultiStageCoverageTarget),
                Format4(input.Settings.AdaptiveRelocationScaleFactor),
                FormatBool(input.Settings.UseHungarianAssignment),
                Format4(input.Settings.CoverageWeight),
                Format4(input.Settings.CoverageDominanceMultiplier),
                Format4(input.Settings.RelocationWeight),
                Format4(input.Settings.RadiusPenaltyWeight),
                Format4(input.Settings.RadiusQualityPenaltyWeight),
                Format4(input.Settings.OverweightPenaltyWeight),
                Format4(input.Settings.HeightPenaltyWeight),
                Format4(input.Settings.RadiusStdDevPenaltyWeight),
                Format4(input.Settings.BalancePenaltyWeight),
                Format4(input.Settings.SimilarPositionPenaltyWeight),
                Format4(input.Settings.RelocationDistancePenaltyWeight),
                Format4(input.Settings.DeratingRatio),
                Format4(input.Settings.SafetyFactor),
                Format4(input.Settings.WireLength),
                Format4(input.Settings.AdditionalWt),
                Format4(input.Settings.MinimumRelocationDistance),
                Format4(input.Settings.DuplicatePositionDistance),
                Format4(input.Settings.MaxRadiusForNormalisation),
                Format4(input.Settings.SlewOffsetX),
                Format4(input.Settings.SlewOffsetY),
                Format4(input.Settings.SlewOffsetZ),
                Format4(input.Settings.BoomHingeOffsetX),
                Format4(input.Settings.BoomHingeOffsetY),
                Format4(input.Settings.BoomHingeOffsetZ),
                FormatBool(input.Settings.EnableProfiling),
                input.Settings.ProfilingLogDirectory ?? "N/A",
                input.ProjectFolderPath ?? "N/A",
                input.DiagnosticsReportsDirectory ?? "N/A"
            };

            Append(Path.Combine(directory, "ga_settings_snapshot.csv"), header, row);
        }

        private static void WriteStageSummary(
            string directory,
            string timestamp,
            PlacementInput input,
            PlacementResult result,
            GAProfiler.StageLogEntry? stageLog)
        {
            (double coverageGain, double gainPerPosition) = CalculateCoverageGain(input, result);
            string usedZoneIds = GetUsedZoneIds(input, result);
            int usedZoneCount = CountIds(usedZoneIds);

            string[] header =
            {
                "OptimizationRunId", "StageRunId", "StageIndex", "DiagnosticsSchemaVersion",
                "Timestamp", "ProjectFeatureSnapshotId", "ProjectId", "ProjectName", "ScenarioId",
                "CraneId", "CraneName", "CraneSpecificationId", "MaxPositions",
                "RelocationWeight", "CoverageTarget", "BestCoverageRatio", "BestCoveragePercent",
                "BestLiftedCount", "BestFitnessScore", "RawPositionCount", "FinalPositionCount",
                "UsedZoneIds", "UsedZoneCount", "RuntimeMs", "Escalated", "TerminationReason",
                "UseHungarianAssignment", "AssignmentEngineUsed", "PopulationSize", "MaxGenerations",
                "MutationSigma", "MutationRate", "CoverageDominanceMultiplier",
                "CoverageGainFromPreviousStage", "CoverageGainPerExtraPosition"
            };

            string[] row =
            {
                input.Settings.OptimizationRunId,
                input.Settings.StageRunId,
                input.Settings.StageIndex.ToString(Invariant),
                BenchmarkDiagnosticsIdentity.SchemaVersion,
                timestamp,
                input.ProjectFeatureSnapshotId ?? "N/A",
                input.ProjectId ?? "N/A",
                input.ProjectName ?? "N/A",
                input.ScenarioId ?? "N/A",
                GetCraneId(input),
                input.CraneSpec?.Crane?.Name ?? "N/A",
                GetCraneSpecificationId(input),
                input.Settings.MaxCranePositions.ToString(Invariant),
                Format4(input.Settings.RelocationWeight),
                Format4(input.Settings.MultiStageCoverageTarget),
                Format6(result.CoverageRatio),
                Format4(result.CoverageRatio * 100.0),
                result.LiftedElementCount.ToString(Invariant),
                Format4(result.FitnessScore),
                (stageLog?.RawPositionCount ?? result.Positions.Count).ToString(Invariant),
                result.Positions.Count.ToString(Invariant),
                usedZoneIds,
                usedZoneCount.ToString(Invariant),
                Format2(stageLog?.RuntimeMs ?? GAProfiler.GetElapsedTimeMs("GAEngine.Run")),
                FormatBool(stageLog?.Escalated ?? result.CoverageRatio < input.Settings.MultiStageCoverageTarget),
                ResolveTerminationReason(input, stageLog),
                FormatBool(input.Settings.UseHungarianAssignment),
                GetAssignmentEngine(input),
                input.Settings.PopulationSize.ToString(Invariant),
                input.Settings.MaxGenerations.ToString(Invariant),
                Format4(input.Settings.MutationSigma),
                Format4(input.Settings.MutationRate),
                Format4(input.Settings.CoverageDominanceMultiplier),
                Format6(coverageGain),
                Format6(gainPerPosition)
            };

            Append(Path.Combine(directory, "ga_stage_summary.csv"), header, row);
        }

        private static void WriteGenerationSummary(
            string directory,
            string timestamp,
            PlacementInput input)
        {
            string[] header =
            {
                "OptimizationRunId", "StageRunId", "StageIndex", "DiagnosticsSchemaVersion",
                "Timestamp", "ProjectFeatureSnapshotId", "ProjectId", "ProjectName", "ScenarioId",
                "CraneId", "CraneName", "CraneSpecificationId", "GenerationIndex",
                "BestFitness", "AverageFitness", "BestCoverageRatio", "AverageCoverageRatio",
                "BestPositionCount", "Count1Position", "Count2Positions", "Count3Positions",
                "MutationSuccessRate", "InvalidMutationCount", "StagnationCounter"
            };

            string path = Path.Combine(directory, "ga_generation_summary.csv");
            IEnumerable<GAProfiler.GenerationLogEntry> generations = GAProfiler.GenerationLogs
                .Where(generation => generation.StageRunId == input.Settings.StageRunId);

            foreach (GAProfiler.GenerationLogEntry generation in generations)
            {
                string[] row =
                {
                    input.Settings.OptimizationRunId,
                    input.Settings.StageRunId,
                    input.Settings.StageIndex.ToString(Invariant),
                    BenchmarkDiagnosticsIdentity.SchemaVersion,
                    timestamp,
                    input.ProjectFeatureSnapshotId ?? "N/A",
                    input.ProjectId ?? "N/A",
                    input.ProjectName ?? "N/A",
                    input.ScenarioId ?? "N/A",
                    GetCraneId(input),
                    input.CraneSpec?.Crane?.Name ?? "N/A",
                    GetCraneSpecificationId(input),
                    generation.GenerationIndex.ToString(Invariant),
                    Format4(generation.BestFitness),
                    Format4(generation.AverageFitness),
                    Format6(generation.BestCoverageRatio),
                    Format6(generation.AverageCoverageRatio),
                    generation.BestPositionCount.ToString(Invariant),
                    generation.Count1Position.ToString(Invariant),
                    generation.Count2Positions.ToString(Invariant),
                    generation.Count3Positions.ToString(Invariant),
                    Format4(generation.MutationSuccessRate),
                    generation.InvalidMutationCount.ToString(Invariant),
                    generation.StagnationCounter.ToString(Invariant)
                };

                Append(path, header, row);
            }
        }

        private void WriteReachabilityHotSpots(
            string directory,
            string timestamp,
            PlacementInput input)
        {
            string[] header =
            {
                "OptimizationRunId", "StageRunId", "StageIndex", "DiagnosticsSchemaVersion",
                "Timestamp", "ProjectFeatureSnapshotId", "ProjectId", "ProjectName", "ScenarioId",
                "CraneId", "CraneName", "CraneSpecificationId", "ZoneId", "X", "Y", "Z",
                "ReachableCount", "RankWithinZone", "GlobalRank", "SelectedAsSeed",
                "HotSpotSeedingRatio", "EnableReachabilitySeeding"
            };

            IReadOnlyList<ScoredCoordinate> hotSpots =
                _reachabilityMapService?.LastHotSpots ?? Array.Empty<ScoredCoordinate>();
            Dictionary<Guid, List<ScoredCoordinate>> zoneGroups = hotSpots
                .GroupBy(hotSpot => hotSpot.ZoneId)
                .ToDictionary(
                    group => group.Key,
                    group => group
                        .OrderByDescending(hotSpot => hotSpot.ReachableCount)
                        .ThenBy(hotSpot => hotSpot.X)
                        .ThenBy(hotSpot => hotSpot.Y)
                        .ToList());

            string path = Path.Combine(directory, "ga_reachability_hotspots.csv");
            for (int index = 0; index < hotSpots.Count; index++)
            {
                ScoredCoordinate hotSpot = hotSpots[index];
                int rankWithinZone = zoneGroups[hotSpot.ZoneId].IndexOf(hotSpot) + 1;
                double z = GetZoneElevation(input, hotSpot.ZoneId);
                string[] row =
                {
                    input.Settings.OptimizationRunId,
                    input.Settings.StageRunId,
                    input.Settings.StageIndex.ToString(Invariant),
                    BenchmarkDiagnosticsIdentity.SchemaVersion,
                    timestamp,
                    input.ProjectFeatureSnapshotId ?? "N/A",
                    input.ProjectId ?? "N/A",
                    input.ProjectName ?? "N/A",
                    input.ScenarioId ?? "N/A",
                    GetCraneId(input),
                    input.CraneSpec?.Crane?.Name ?? "N/A",
                    GetCraneSpecificationId(input),
                    hotSpot.ZoneId.ToString(),
                    Format4(hotSpot.X),
                    Format4(hotSpot.Y),
                    Format4(z),
                    hotSpot.ReachableCount.ToString(Invariant),
                    rankWithinZone.ToString(Invariant),
                    (index + 1).ToString(Invariant),
                    _reachabilityMapService == null
                        ? "N/A"
                        : FormatBool(_reachabilityMapService.WasSelectedAsSeed(hotSpot)),
                    Format4(input.Settings.HotSpotSeedingRatio),
                    FormatBool(input.Settings.EnableReachabilitySeeding)
                };

                Append(path, header, row);
            }

            EnsureHeader(path, header);
        }

        private static void WritePositions(
            string directory,
            string timestamp,
            PlacementInput input,
            PlacementResult result)
        {
            string[] header =
            {
                "OptimizationRunId", "StageRunId", "StageIndex", "DiagnosticsSchemaVersion",
                "Timestamp", "ProjectFeatureSnapshotId", "ProjectId", "ProjectName", "ScenarioId",
                "CraneId", "CraneName", "CraneSpecificationId", "PositionIndex", "X", "Y",
                "Z", "ZoneId", "LiftedCount", "UniqueLiftedCount", "AssignmentEngineUsed",
                "UseHungarianAssignment", "MaxCranePositions", "CoverageContributionRatio",
                "DistanceFromPreviousPosition", "AverageRadius", "MaxRadius", "P95Radius",
                "AverageCapacityUtilization", "MaxCapacityUtilization", "P95CapacityUtilization",
                "AverageRequiredBoomLength", "MaxRequiredBoomLength"
            };

            string path = Path.Combine(directory, "ga_positions.csv");
            for (int index = 0; index < result.Positions.Count; index++)
            {
                CranePositionResult position = result.Positions[index];
                int totalElementCount = input.Elements?.Count ?? 0;
                List<ElementLiftResult> lifted = position.ElementResults
                    .Where(item => item.IsFinalAssignedToPoint && item.Status == LiftStatus.Liftable)
                    .GroupBy(item => item.ElementId, StringComparer.OrdinalIgnoreCase)
                    .Select(group => group.First())
                    .ToList();
                List<double> utilization = lifted
                    .Where(item => item.AvailableCapacity > 0)
                    .Select(item => item.RequiredCapacity / item.AvailableCapacity)
                    .ToList();
                string zoneId = FindZoneId(input, position.X, position.Y, position.Z);

                double distance = index == 0
                    ? 0
                    : Distance3D(result.Positions[index - 1], position);

                string[] row =
                {
                    input.Settings.OptimizationRunId,
                    input.Settings.StageRunId,
                    input.Settings.StageIndex.ToString(Invariant),
                    BenchmarkDiagnosticsIdentity.SchemaVersion,
                    timestamp,
                    input.ProjectFeatureSnapshotId ?? "N/A",
                    input.ProjectId ?? "N/A",
                    input.ProjectName ?? "N/A",
                    input.ScenarioId ?? "N/A",
                    GetCraneId(input),
                    input.CraneSpec?.Crane?.Name ?? "N/A",
                    GetCraneSpecificationId(input),
                    (index + 1).ToString(Invariant),
                    Format4(position.X),
                    Format4(position.Y),
                    Format4(position.Z),
                    zoneId,
                    position.LiftableCount.ToString(Invariant),
                    lifted.Count.ToString(Invariant),
                    GetAssignmentEngine(input),
                    FormatBool(input.Settings.UseHungarianAssignment),
                    input.Settings.MaxCranePositions.ToString(Invariant),
                    Format6(totalElementCount == 0
                        ? 0
                        : (double)lifted.Count / totalElementCount),
                    Format4(distance),
                    FormatOptional(Average(lifted.Select(item => item.Radius))),
                    FormatOptional(Max(lifted.Select(item => item.Radius))),
                    FormatOptional(Percentile(lifted.Select(item => item.Radius), 0.95)),
                    FormatOptional(Average(utilization)),
                    FormatOptional(Max(utilization)),
                    FormatOptional(Percentile(utilization, 0.95)),
                    FormatOptional(Average(lifted.Select(item => item.BoomLength))),
                    FormatOptional(Max(lifted.Select(item => item.BoomLength)))
                };

                Append(path, header, row);
            }

            EnsureHeader(path, header);
        }

        private static void WriteElementAssignments(
            string directory,
            string timestamp,
            PlacementInput input,
            IReadOnlyList<AssignmentMetric> assignments)
        {
            string[] header =
            {
                "OptimizationRunId", "StageRunId", "StageIndex", "DiagnosticsSchemaVersion",
                "Timestamp", "ProjectFeatureSnapshotId", "ProjectId", "ProjectName", "ScenarioId",
                "CraneId", "CraneName", "CraneSpecificationId", "ElementId", "ElementType",
                "AssignedPositionIndex", "AssignedZoneId", "LiftStatus", "Radius",
                "RequiredBoomLength", "ElementWeight", "AdditionalWt", "RequiredCapacity",
                "ChartCapacity", "AvailableCapacity", "CapacityUtilization", "CapacityMarginTons",
                "RadiusUtilization", "BoomUtilization", "CraneMaxRadius", "CraneMaxBoomLength",
                "DeratingRatio", "SafetyFactor", "CombinedCapacityFactor", "FailureReason",
                "IsFinalLifted"
            };

            string path = Path.Combine(directory, "ga_element_assignments.csv");
            foreach (AssignmentMetric assignment in assignments)
            {
                string[] row =
                {
                    input.Settings.OptimizationRunId,
                    input.Settings.StageRunId,
                    input.Settings.StageIndex.ToString(Invariant),
                    BenchmarkDiagnosticsIdentity.SchemaVersion,
                    timestamp,
                    input.ProjectFeatureSnapshotId ?? "N/A",
                    input.ProjectId ?? "N/A",
                    input.ProjectName ?? "N/A",
                    input.ScenarioId ?? "N/A",
                    GetCraneId(input),
                    input.CraneSpec?.Crane?.Name ?? "N/A",
                    GetCraneSpecificationId(input),
                    assignment.ElementId,
                    assignment.ElementType,
                    assignment.AssignedPositionIndex.ToString(Invariant),
                    assignment.AssignedZoneId,
                    assignment.LiftStatus,
                    FormatOptional(assignment.Radius),
                    FormatOptional(assignment.RequiredBoomLength),
                    Format4(assignment.ElementWeight),
                    Format4(input.Settings.AdditionalWt),
                    Format4(assignment.RequiredCapacity),
                    FormatOptional(assignment.ChartCapacity),
                    FormatOptional(assignment.AvailableCapacity),
                    FormatOptional(assignment.CapacityUtilization),
                    FormatOptional(assignment.CapacityMarginTons),
                    FormatOptional(assignment.RadiusUtilization),
                    FormatOptional(assignment.BoomUtilization),
                    Format4(input.CraneSpec?.MaxRadius ?? 0),
                    Format4(input.CraneSpec?.MaxBoomLength ?? 0),
                    Format4(input.Settings.DeratingRatio),
                    Format4(input.Settings.SafetyFactor),
                    Format4(input.Settings.DeratingRatio * input.Settings.SafetyFactor),
                    assignment.FailureReason,
                    FormatBool(assignment.IsFinalLifted)
                };

                Append(path, header, row);
            }

            EnsureHeader(path, header);
        }

        private void WriteProjectFeatures(
            string directory,
            string timestamp,
            PlacementInput input)
        {
            List<PrecastElement> scopeElements = input.FeatureScopeElements ?? new List<PrecastElement>();
            List<PrecastElement> gaElements = input.Elements ?? new List<PrecastElement>();
            List<double> weights = gaElements
                .Where(element => element.ElementWT.HasValue)
                .Select(element => element.ElementWT!.Value)
                .ToList();
            List<double> heights = gaElements
                .Select(GetElementHeight)
                .Where(height => height.HasValue)
                .Select(height => height!.Value)
                .ToList();
            List<double> volumes = gaElements
                .Select(element => element.ElementVolume ?? element.VolumeM3)
                .Where(volume => volume.HasValue)
                .Select(volume => volume!.Value)
                .ToList();

            Dictionary<string, int> categories = new(StringComparer.OrdinalIgnoreCase);
            foreach (PrecastElement element in scopeElements)
            {
                string category = NormalizeCategory(element);
                categories[category] = categories.GetValueOrDefault(category) + 1;
            }

            int trustedWeightCount = scopeElements.Count(IsTrustedWeight);
            int unknownWeightTrustCount = scopeElements.Count(element =>
                element.WeightTrustLevel == IfcWeightTrustLevel.Missing);
            int untrustedWeightCount = Math.Max(
                0,
                scopeElements.Count - trustedWeightCount - unknownWeightTrustCount);

            List<double> areas = (input.Zones ?? new List<GreenAreaZone>())
                .Select(GetZoneArea)
                .Where(area => area > 0)
                .ToList();
            double totalArea = areas.Sum();
            ReachabilityDiagnostics? reachability = _reachabilityMapService?.LastDiagnostics;
            IReadOnlyList<ZoneReachabilitySummary> zoneSummaries =
                _reachabilityMapService?.LastZoneSummaries ?? Array.Empty<ZoneReachabilitySummary>();
            double zoneSkew = CalculateZoneReachabilitySkew(zoneSummaries);

            string[] header =
            {
                "ProjectFeatureSnapshotId", "OptimizationRunId", "DiagnosticsSchemaVersion",
                "Timestamp", "ProjectId", "ProjectName", "ScenarioId", "IfcFileName",
                "ScopeDefinitionHash", "CraneId", "CraneName", "CraneSpecificationId",
                "RawElementCount", "OptimizationScopeElementCount", "GaReadyElementCount",
                "WallCount", "ColumnCount", "BeamCount", "FootingCount", "AssemblyCount",
                "SlabCount", "OtherCount", "AverageWeightTons", "P95WeightTons",
                "MaxWeightTons", "TrustedWeightCount", "UntrustedWeightCount",
                "UnknownWeightTrustCount", "AverageHeightM", "MaxHeightM", "HeightDefinition",
                "AverageVolumeM3", "MaxVolumeM3", "CraneMaxRadius", "CraneMinRadius",
                "CraneMaxBoomLength", "CraneMinBoomLength", "CraneMaxCapacity", "GridStepM",
                "CraneFootprintRadiusM", "AllowedZoneCount", "TotalAllowedAreaM2",
                "AreaDefinition", "MinAllowedZoneAreaM2", "MaxAllowedZoneAreaM2",
                "AllowedAreaPerGaReadyElementM2", "CandidateGridPointCount",
                "ValidGridPointCount", "RejectedGridPointCount", "RejectedGridPointRatio",
                "ReachabilityMax", "ReachabilityMean", "ReachabilityStdDev",
                "ZeroReachabilityRatio", "ZoneReachabilitySkew"
            };

            string[] row =
            {
                input.ProjectFeatureSnapshotId ?? "N/A",
                input.Settings.OptimizationRunId,
                BenchmarkDiagnosticsIdentity.SchemaVersion,
                timestamp,
                input.ProjectId ?? "N/A",
                input.ProjectName ?? "N/A",
                input.ScenarioId ?? "N/A",
                input.IfcFileName ?? "N/A",
                input.ScopeDefinitionHash ?? "N/A",
                GetCraneId(input),
                input.CraneSpec?.Crane?.Name ?? "N/A",
                GetCraneSpecificationId(input),
                input.RawElementCount.ToString(Invariant),
                input.OptimizationScopeElementCount.ToString(Invariant),
                gaElements.Count.ToString(Invariant),
                GetCategoryCount(categories, "Wall").ToString(Invariant),
                GetCategoryCount(categories, "Column").ToString(Invariant),
                GetCategoryCount(categories, "Beam").ToString(Invariant),
                GetCategoryCount(categories, "Footing").ToString(Invariant),
                GetCategoryCount(categories, "Assembly").ToString(Invariant),
                GetCategoryCount(categories, "Slab").ToString(Invariant),
                GetCategoryCount(categories, "Other").ToString(Invariant),
                FormatOptional(Average(weights)),
                FormatOptional(Percentile(weights, 0.95)),
                FormatOptional(Max(weights)),
                trustedWeightCount.ToString(Invariant),
                untrustedWeightCount.ToString(Invariant),
                unknownWeightTrustCount.ToString(Invariant),
                FormatOptional(Average(heights)),
                FormatOptional(Max(heights)),
                heights.Count == 0 ? "N/A" : "ElementHeightM_WithBBoxHeightFallback",
                FormatOptional(Average(volumes)),
                FormatOptional(Max(volumes)),
                Format4(input.CraneSpec?.MaxRadius ?? 0),
                Format4(input.CraneSpec?.MinRadius ?? 0),
                Format4(input.CraneSpec?.MaxBoomLength ?? 0),
                Format4(input.CraneSpec?.MinBoomLength ?? 0),
                Format4(input.CraneSpec?.MaxCapacity ?? 0),
                Format4(input.Settings.GridStep),
                Format4(input.Settings.CraneFootprintRadius),
                (input.Zones?.Count ?? 0).ToString(Invariant),
                Format4(totalArea),
                "SumOfAllowedZoneAreas_NoOverlapUnion",
                FormatOptional(Min(areas)),
                FormatOptional(Max(areas)),
                FormatOptional(gaElements.Count == 0 ? null : totalArea / gaElements.Count),
                reachability?.TotalCandidatePoints.ToString(Invariant) ?? "N/A",
                reachability?.ValidCandidatePoints.ToString(Invariant) ?? "N/A",
                reachability?.RejectedCandidatePoints.ToString(Invariant) ?? "N/A",
                reachability == null || reachability.TotalCandidatePoints == 0
                    ? "N/A"
                    : Format6((double)reachability.RejectedCandidatePoints / reachability.TotalCandidatePoints),
                zoneSummaries.Count == 0
                    ? "N/A"
                    : zoneSummaries.Max(summary => summary.ReachabilityMax).ToString(Invariant),
                reachability == null ? "N/A" : Format4(reachability.ReachabilityMean),
                reachability == null ? "N/A" : Format4(reachability.ReachabilityStdDev),
                reachability == null || reachability.ValidCandidatePoints == 0
                    ? "N/A"
                    : Format6((double)reachability.ZeroCoveragePoints / reachability.ValidCandidatePoints),
                zoneSummaries.Count == 0 ? "N/A" : Format6(zoneSkew)
            };

            AppendUnique(
                Path.Combine(directory, "ga_project_features.csv"),
                header,
                row,
                0,
                1);
        }

        private static void WriteSolutionQualityMetrics(
            string directory,
            string timestamp,
            PlacementInput input,
            PlacementResult result,
            IReadOnlyList<AssignmentMetric> assignments,
            GAProfiler.StageLogEntry? stageLog)
        {
            List<AssignmentMetric> lifted = assignments
                .Where(assignment => assignment.IsFinalLifted)
                .ToList();
            List<double> capacityUtilization = lifted
                .Where(assignment => assignment.CapacityUtilization.HasValue)
                .Select(assignment => assignment.CapacityUtilization!.Value)
                .ToList();
            List<double> capacityMargins = lifted
                .Where(assignment => assignment.CapacityMarginTons.HasValue)
                .Select(assignment => assignment.CapacityMarginTons!.Value)
                .ToList();
            List<double> radii = lifted
                .Where(assignment => assignment.Radius.HasValue)
                .Select(assignment => assignment.Radius!.Value)
                .ToList();
            List<double> booms = lifted
                .Where(assignment => assignment.RequiredBoomLength.HasValue)
                .Select(assignment => assignment.RequiredBoomLength!.Value)
                .ToList();
            string usedZoneIds = GetUsedZoneIds(input, result);
            (double coverageGain, double gainPerPosition) = CalculateCoverageGain(input, result);
            double totalRelocation = CalculateTotalRelocationDistance(result.Positions);
            double averageRelocation = result.Positions.Count < 2
                ? 0
                : totalRelocation / (result.Positions.Count - 1);
            bool isBestStage = IsCurrentBestStage(input, result);

            string[] header =
            {
                "OptimizationRunId", "StageRunId", "StageIndex", "ProjectFeatureSnapshotId",
                "ProjectId", "ProjectName", "ScenarioId", "CraneId", "CraneName",
                "CraneSpecificationId", "DiagnosticsSchemaVersion", "Timestamp", "CoverageRatio",
                "CoveragePercent", "LiftedElementCount", "FailedElementCount", "TotalElementCount",
                "PositionCount", "CoverageGainFromPreviousStage", "CoverageGainPerExtraPosition",
                "TotalRelocationDistance", "AverageRelocationDistance",
                "RelocationDistanceDefinition", "UsedZoneCount", "UsedZoneIds", "FitnessScore",
                "CoverageDominanceMultiplier", "RuntimeMs", "TerminationReason", "IsBestStage",
                "UseHungarianAssignment", "AssignmentEngineUsed", "AverageRadius", "MaxRadius",
                "P95Radius", "RadiusStdDev", "AverageRequiredBoomLength", "MaxRequiredBoomLength",
                "P95RequiredBoomLength", "AverageCapacityUtilization", "MaxCapacityUtilization",
                "P95CapacityUtilization", "MinCapacityMarginTons", "AverageCapacityMarginTons",
                "P05CapacityMarginTons", "OverweightCount", "OutOfRadiusCount", "HeightFailCount",
                "UnreachableCount", "SpecialCaseCount"
            };

            string[] row =
            {
                input.Settings.OptimizationRunId,
                input.Settings.StageRunId,
                input.Settings.StageIndex.ToString(Invariant),
                input.ProjectFeatureSnapshotId ?? "N/A",
                input.ProjectId ?? "N/A",
                input.ProjectName ?? "N/A",
                input.ScenarioId ?? "N/A",
                GetCraneId(input),
                input.CraneSpec?.Crane?.Name ?? "N/A",
                GetCraneSpecificationId(input),
                BenchmarkDiagnosticsIdentity.SchemaVersion,
                timestamp,
                Format6(result.CoverageRatio),
                Format4(result.CoverageRatio * 100.0),
                result.LiftedElementCount.ToString(Invariant),
                Math.Max(0, result.TotalElementCount - result.LiftedElementCount).ToString(Invariant),
                result.TotalElementCount.ToString(Invariant),
                result.Positions.Count.ToString(Invariant),
                Format6(coverageGain),
                Format6(gainPerPosition),
                Format4(totalRelocation),
                Format4(averageRelocation),
                "AdjacentPositionIndexPolyline3D_Meters",
                CountIds(usedZoneIds).ToString(Invariant),
                usedZoneIds,
                Format4(result.FitnessScore),
                Format4(input.Settings.CoverageDominanceMultiplier),
                Format2(stageLog?.RuntimeMs ?? GAProfiler.GetElapsedTimeMs("GAEngine.Run")),
                ResolveTerminationReason(input, stageLog),
                FormatBool(isBestStage),
                FormatBool(input.Settings.UseHungarianAssignment),
                GetAssignmentEngine(input),
                FormatOptional(Average(radii)),
                FormatOptional(Max(radii)),
                FormatOptional(Percentile(radii, 0.95)),
                FormatOptional(StandardDeviation(radii)),
                FormatOptional(Average(booms)),
                FormatOptional(Max(booms)),
                FormatOptional(Percentile(booms, 0.95)),
                FormatOptional(Average(capacityUtilization)),
                FormatOptional(Max(capacityUtilization)),
                FormatOptional(Percentile(capacityUtilization, 0.95)),
                FormatOptional(Min(capacityMargins)),
                FormatOptional(Average(capacityMargins)),
                FormatOptional(Percentile(capacityMargins, 0.05)),
                result.Statistics.Overweight.ToString(Invariant),
                result.Statistics.OutOfRadius.ToString(Invariant),
                result.Statistics.HeightFail.ToString(Invariant),
                "N/A",
                result.Statistics.SpecialCase.ToString(Invariant)
            };

            string path = Path.Combine(directory, "ga_solution_quality_metrics.csv");
            Append(path, header, row);
            RewriteBestStageFlags(path, input.Settings.OptimizationRunId);
        }

        private void WriteZoneReachabilitySummary(
            string directory,
            string timestamp,
            PlacementInput input)
        {
            string[] header =
            {
                "OptimizationRunId", "StageRunId", "StageIndex", "DiagnosticsSchemaVersion",
                "Timestamp", "ProjectFeatureSnapshotId", "ProjectId", "ProjectName", "ScenarioId",
                "CraneId", "CraneName", "CraneSpecificationId", "ZoneId",
                "CandidateGridPointCount", "ValidGridPointCount", "RejectedGridPointCount",
                "RejectedGridPointRatio", "ZeroReachabilityPointCount", "ReachabilityMax",
                "ReachabilityMean", "ReachabilityStdDev", "SelectedSeedCount",
                "RejectedOutsideAllowedCount", "RejectedForbiddenOverlapCount",
                "RejectedFootprintInvalidCount", "RejectedOtherCount"
            };

            IReadOnlyList<ZoneReachabilitySummary> summaries =
                _reachabilityMapService?.LastZoneSummaries ?? Array.Empty<ZoneReachabilitySummary>();
            string path = Path.Combine(directory, "ga_zone_reachability_summary.csv");

            foreach (ZoneReachabilitySummary summary in summaries)
            {
                string[] row =
                {
                    input.Settings.OptimizationRunId,
                    input.Settings.StageRunId,
                    input.Settings.StageIndex.ToString(Invariant),
                    BenchmarkDiagnosticsIdentity.SchemaVersion,
                    timestamp,
                    input.ProjectFeatureSnapshotId ?? "N/A",
                    input.ProjectId ?? "N/A",
                    input.ProjectName ?? "N/A",
                    input.ScenarioId ?? "N/A",
                    GetCraneId(input),
                    input.CraneSpec?.Crane?.Name ?? "N/A",
                    GetCraneSpecificationId(input),
                    summary.ZoneId.ToString(),
                    summary.CandidateGridPointCount.ToString(Invariant),
                    summary.ValidGridPointCount.ToString(Invariant),
                    summary.RejectedGridPointCount.ToString(Invariant),
                    summary.CandidateGridPointCount == 0
                        ? "N/A"
                        : Format6((double)summary.RejectedGridPointCount / summary.CandidateGridPointCount),
                    summary.ZeroReachabilityPointCount.ToString(Invariant),
                    summary.ReachabilityMax.ToString(Invariant),
                    Format4(summary.ReachabilityMean),
                    Format4(summary.ReachabilityStdDev),
                    summary.SelectedSeedCount.ToString(Invariant),
                    "N/A",
                    "N/A",
                    "N/A",
                    summary.RejectedGridPointCount.ToString(Invariant)
                };

                Append(path, header, row);
            }

            EnsureHeader(path, header);
        }

        private static List<AssignmentMetric> BuildAssignmentMetrics(
            PlacementInput input,
            PlacementResult result)
        {
            List<AssignmentMetric> metrics = new();
            foreach (PrecastElement element in input.Elements ?? new List<PrecastElement>())
            {
                (ElementLiftResult? evaluation, int positionIndex, string zoneId) =
                    FindBestEvaluation(input, result, element.ElementID ?? string.Empty);
                double elementWeight = element.ElementWT ?? 0;
                double requiredCapacity = elementWeight + input.Settings.AdditionalWt;

                if (evaluation == null)
                {
                    metrics.Add(new AssignmentMetric
                    {
                        ElementId = element.ElementID ?? "N/A",
                        ElementType = element.ElementType ?? "N/A",
                        AssignedPositionIndex = -1,
                        AssignedZoneId = Guid.Empty.ToString(),
                        LiftStatus = "NoEvaluation",
                        ElementWeight = elementWeight,
                        RequiredCapacity = requiredCapacity,
                        FailureReason = "NoEvaluation"
                    });
                    continue;
                }

                double combinedFactor = input.Settings.DeratingRatio * input.Settings.SafetyFactor;
                bool isFinalLifted = positionIndex > 0 && evaluation.Status == LiftStatus.Liftable;
                string failureReason = evaluation.Status == LiftStatus.Liftable
                    ? "Liftable"
                    : LiftClassificationPolicy.Classify(
                        evaluation.Status,
                        evaluation.ChartStatus,
                        element.ElementWT,
                        evaluation.BoomLength,
                        input.CraneSpec?.MaxBoomLength ?? 0,
                        result.Positions.Count > 0).FailureReason;

                metrics.Add(new AssignmentMetric
                {
                    ElementId = element.ElementID ?? "N/A",
                    ElementType = element.ElementType ?? "N/A",
                    AssignedPositionIndex = positionIndex,
                    AssignedZoneId = zoneId,
                    LiftStatus = evaluation.Status.ToString(),
                    Radius = evaluation.Radius,
                    RequiredBoomLength = evaluation.BoomLength,
                    ElementWeight = elementWeight,
                    RequiredCapacity = requiredCapacity,
                    ChartCapacity = combinedFactor > 0
                        ? evaluation.AvailableCapacity / combinedFactor
                        : null,
                    AvailableCapacity = evaluation.AvailableCapacity,
                    CapacityUtilization = evaluation.AvailableCapacity > 0
                        ? requiredCapacity / evaluation.AvailableCapacity
                        : null,
                    CapacityMarginTons = evaluation.AvailableCapacity - requiredCapacity,
                    RadiusUtilization = input.CraneSpec?.MaxRadius > 0
                        ? evaluation.Radius / input.CraneSpec.MaxRadius
                        : null,
                    BoomUtilization = input.CraneSpec?.MaxBoomLength > 0
                        ? evaluation.BoomLength / input.CraneSpec.MaxBoomLength
                        : null,
                    FailureReason = failureReason,
                    IsFinalLifted = isFinalLifted
                });
            }

            return metrics;
        }

        private static (ElementLiftResult? Evaluation, int PositionIndex, string ZoneId) FindBestEvaluation(
            PlacementInput input,
            PlacementResult result,
            string elementId)
        {
            List<(ElementLiftResult Result, int PositionIndex, CranePositionResult Position)> candidates = new();
            for (int index = 0; index < result.Positions.Count; index++)
            {
                CranePositionResult position = result.Positions[index];
                ElementLiftResult? evaluation = position.ElementResults.FirstOrDefault(item =>
                    string.Equals(item.ElementId, elementId, StringComparison.OrdinalIgnoreCase));
                if (evaluation != null)
                {
                    candidates.Add((evaluation, index + 1, position));
                }
            }

            var owner = candidates.FirstOrDefault(candidate => candidate.Result.IsFinalAssignedToPoint);
            if (owner.Result != null)
            {
                return (
                    owner.Result,
                    owner.PositionIndex,
                    FindZoneId(input, owner.Position.X, owner.Position.Y, owner.Position.Z));
            }

            if (candidates.Count == 0)
                return (null, -1, Guid.Empty.ToString());

            var bestFailed = candidates.FirstOrDefault(candidate =>
                string.Equals(
                    candidate.Result.CandidatePointId,
                    candidate.Result.BestFailedPointId,
                    StringComparison.OrdinalIgnoreCase));
            if (bestFailed.Result == null)
            {
                bestFailed = candidates
                    .OrderByDescending(candidate => candidate.Result.AssignmentScore)
                    .ThenBy(candidate => candidate.Result.Radius)
                    .First();
            }

            return (bestFailed.Result, -1, Guid.Empty.ToString());
        }

        private static (double CoverageGain, double GainPerPosition) CalculateCoverageGain(
            PlacementInput input,
            PlacementResult result)
        {
            GAProfiler.StageLogEntry? previous = GAProfiler.StageLogs
                .Where(stage =>
                    stage.OptimizationRunId == input.Settings.OptimizationRunId
                    && stage.StageIndex < input.Settings.StageIndex)
                .OrderByDescending(stage => stage.StageIndex)
                .FirstOrDefault();
            if (previous == null)
                return (0, 0);

            double gain = result.CoverageRatio - previous.BestCoverageRatio;
            int extraPositions = result.Positions.Count - previous.FinalPositionCount;
            return (gain, extraPositions > 0 ? gain / extraPositions : 0);
        }

        private static bool IsCurrentBestStage(PlacementInput input, PlacementResult result)
        {
            IEnumerable<GAProfiler.StageLogEntry> stages = GAProfiler.StageLogs
                .Where(stage => stage.OptimizationRunId == input.Settings.OptimizationRunId);
            GAProfiler.StageLogEntry? best = stages
                .OrderByDescending(stage => stage.BestCoverageRatio)
                .ThenBy(stage => stage.FinalPositionCount)
                .ThenByDescending(stage => stage.BestFitnessScore)
                .FirstOrDefault();

            return best == null || best.StageRunId == input.Settings.StageRunId;
        }

        private static string ResolveTerminationReason(
            PlacementInput input,
            GAProfiler.StageLogEntry? stageLog)
        {
            if (!string.IsNullOrWhiteSpace(stageLog?.TerminationReason))
                return stageLog.TerminationReason;
            if (GAProfiler.StoppedByConvergence)
                return "Convergence";
            if (GAProfiler.ActualGenerationLoopCount < input.Settings.EarlyStoppingGenerations)
                return "EarlyStopping";
            return "MaxGenerationsReached";
        }

        private static string GetUsedZoneIds(PlacementInput input, PlacementResult result)
        {
            return string.Join(
                ";",
                result.Positions
                    .Select(position => FindZoneId(input, position.X, position.Y, position.Z))
                    .Where(zoneId => zoneId != Guid.Empty.ToString())
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(zoneId => zoneId, StringComparer.OrdinalIgnoreCase));
        }

        private static string FindZoneId(PlacementInput input, double x, double y, double z)
        {
            GreenAreaZone? zone = input.Zones?.FirstOrDefault(candidate =>
                candidate.Vertices != null
                && SpatialMath.PointInPolygon(new DomainPoint3D(x, y, z), candidate.Vertices));
            return (zone?.ZoneId ?? Guid.Empty).ToString();
        }

        private static double GetZoneElevation(PlacementInput input, Guid zoneId)
        {
            GreenAreaZone? zone = input.Zones?.FirstOrDefault(candidate => candidate.ZoneId == zoneId);
            return zone?.Vertices == null || zone.Vertices.Count == 0
                ? 0
                : zone.Vertices.Average(vertex => vertex.Z);
        }

        private static double CalculateTotalRelocationDistance(IReadOnlyList<CranePositionResult> positions)
        {
            double total = 0;
            for (int index = 1; index < positions.Count; index++)
            {
                total += Distance3D(positions[index - 1], positions[index]);
            }

            return total;
        }

        private static double Distance3D(CranePositionResult first, CranePositionResult second)
        {
            double dx = second.X - first.X;
            double dy = second.Y - first.Y;
            double dz = second.Z - first.Z;
            return Math.Sqrt(dx * dx + dy * dy + dz * dz);
        }

        private static double GetZoneArea(GreenAreaZone zone)
        {
            if (zone.Area > 0)
                return zone.Area;
            if (zone.Vertices == null || zone.Vertices.Count < 3)
                return 0;

            double twiceArea = 0;
            for (int index = 0; index < zone.Vertices.Count; index++)
            {
                DomainPoint3D current = zone.Vertices[index];
                DomainPoint3D next = zone.Vertices[(index + 1) % zone.Vertices.Count];
                twiceArea += current.X * next.Y - next.X * current.Y;
            }

            return Math.Abs(twiceArea) / 2.0;
        }

        private static string NormalizeCategory(PrecastElement element)
        {
            string value = !string.IsNullOrWhiteSpace(element.ResolvedCategory)
                ? element.ResolvedCategory
                : element.ElementType ?? string.Empty;
            string normalized = value.Trim().ToLowerInvariant();

            if (normalized.Contains("wall")) return "Wall";
            if (normalized.Contains("column")) return "Column";
            if (normalized.Contains("beam")) return "Beam";
            if (normalized.Contains("footing") || normalized.Contains("foundation")) return "Footing";
            if (normalized.Contains("assembly")) return "Assembly";
            if (normalized.Contains("slab") || normalized.Contains("floor")) return "Slab";
            return "Other";
        }

        private static bool IsTrustedWeight(PrecastElement element)
        {
            return element.WeightTrustLevel == IfcWeightTrustLevel.ExplicitQuantity
                || element.WeightTrustLevel == IfcWeightTrustLevel.ExplicitProperty;
        }

        private static double? GetElementHeight(PrecastElement element)
        {
            if (element.ElementHeight is > 0)
                return element.ElementHeight.Value;
            if (element.BBoxHeight is > 0)
                return element.BBoxHeight.Value;
            return null;
        }

        private static int GetCategoryCount(Dictionary<string, int> categories, string category)
        {
            return categories.GetValueOrDefault(category);
        }

        private static double CalculateZoneReachabilitySkew(
            IReadOnlyList<ZoneReachabilitySummary> summaries)
        {
            List<double> means = summaries
                .Where(summary => summary.ValidGridPointCount > 0)
                .Select(summary => summary.ReachabilityMean)
                .ToList();
            double? average = Average(means);
            if (!average.HasValue || average.Value <= 0)
                return 0;

            return StandardDeviation(means)!.Value / average.Value;
        }

        private static string GetCraneId(PlacementInput input)
        {
            int craneId = input.CraneSpec?.Crane?.Id ?? input.CraneSpec?.CraneId ?? 0;
            return craneId > 0 ? craneId.ToString(Invariant) : "N/A";
        }

        private static string GetCraneSpecificationId(PlacementInput input)
        {
            int specificationId = input.CraneSpec?.Id ?? 0;
            return specificationId > 0 ? specificationId.ToString(Invariant) : "N/A";
        }

        private static string GetAssignmentEngine(PlacementInput input)
        {
            return input.Settings.UseHungarianAssignment ? "Hungarian" : "Greedy";
        }

        private static int CountIds(string ids)
        {
            return string.IsNullOrWhiteSpace(ids)
                ? 0
                : ids.Split(';', StringSplitOptions.RemoveEmptyEntries).Length;
        }

        private static double? Average(IEnumerable<double> values)
        {
            double[] materialized = values.ToArray();
            return materialized.Length == 0 ? null : materialized.Average();
        }

        private static double? Max(IEnumerable<double> values)
        {
            double[] materialized = values.ToArray();
            return materialized.Length == 0 ? null : materialized.Max();
        }

        private static double? Min(IEnumerable<double> values)
        {
            double[] materialized = values.ToArray();
            return materialized.Length == 0 ? null : materialized.Min();
        }

        private static double? Percentile(IEnumerable<double> values, double percentile)
        {
            double[] sorted = values.OrderBy(value => value).ToArray();
            if (sorted.Length == 0)
                return null;
            if (sorted.Length == 1)
                return sorted[0];

            double index = (sorted.Length - 1) * percentile;
            int lower = (int)Math.Floor(index);
            int upper = (int)Math.Ceiling(index);
            if (lower == upper)
                return sorted[lower];

            double fraction = index - lower;
            return sorted[lower] + (sorted[upper] - sorted[lower]) * fraction;
        }

        private static double? StandardDeviation(IEnumerable<double> values)
        {
            double[] materialized = values.ToArray();
            if (materialized.Length == 0)
                return null;

            double mean = materialized.Average();
            double variance = materialized.Average(value => Math.Pow(value - mean, 2));
            return Math.Sqrt(variance);
        }

        private static string FormatOptional(double? value)
        {
            return value.HasValue && double.IsFinite(value.Value)
                ? Format6(value.Value)
                : "N/A";
        }

        private static string FormatBool(bool value) => value ? "true" : "false";
        private static string Format2(double value) => value.ToString("F2", Invariant);
        private static string Format4(double value) => value.ToString("F4", Invariant);
        private static string Format6(double value) => value.ToString("F6", Invariant);

        private static void Append(string path, IReadOnlyList<string> header, IReadOnlyList<string> row)
        {
            if (header.Count != row.Count)
            {
                throw new InvalidOperationException(
                    $"CSV schema mismatch for {Path.GetFileName(path)}: header={header.Count}, row={row.Count}.");
            }

            lock (CsvLock)
            {
                PrepareFile(path, header);
                using var writer = new StreamWriter(path, true, new UTF8Encoding(true));
                writer.WriteLine(string.Join(",", row.Select(Escape)));
            }
        }

        private static void AppendUnique(
            string path,
            IReadOnlyList<string> header,
            IReadOnlyList<string> row,
            params int[] keyIndexes)
        {
            if (header.Count != row.Count)
            {
                throw new InvalidOperationException(
                    $"CSV schema mismatch for {Path.GetFileName(path)}: header={header.Count}, row={row.Count}.");
            }

            lock (CsvLock)
            {
                PrepareFile(path, header);
                bool exists = File.ReadLines(path)
                    .Skip(1)
                    .Where(line => !string.IsNullOrWhiteSpace(line))
                    .Select(ParseCsvLine)
                    .Any(existing => existing.Length == row.Count
                        && keyIndexes.All(index => existing[index] == row[index]));
                if (exists)
                    return;

                using var writer = new StreamWriter(path, true, new UTF8Encoding(true));
                writer.WriteLine(string.Join(",", row.Select(Escape)));
            }
        }

        private static void EnsureHeader(string path, IReadOnlyList<string> header)
        {
            lock (CsvLock)
            {
                PrepareFile(path, header);
            }
        }

        private static void PrepareFile(string path, IReadOnlyList<string> header)
        {
            string expectedHeader = string.Join(",", header);
            if (File.Exists(path))
            {
                string? existingHeader;
                using (var reader = new StreamReader(path, Encoding.UTF8, true))
                {
                    existingHeader = reader.ReadLine();
                }

                if (string.Equals(existingHeader, expectedHeader, StringComparison.Ordinal))
                    return;

                string legacyPath = Path.Combine(
                    Path.GetDirectoryName(path) ?? string.Empty,
                    $"{Path.GetFileNameWithoutExtension(path)}.legacy_{DateTime.UtcNow:yyyyMMdd_HHmmssfff}{Path.GetExtension(path)}");
                File.Move(path, legacyPath);
            }

            using var writer = new StreamWriter(path, false, new UTF8Encoding(true));
            writer.WriteLine(expectedHeader);
        }

        private static void RewriteBestStageFlags(string path, string optimizationRunId)
        {
            lock (CsvLock)
            {
                if (!File.Exists(path))
                    return;

                string[] lines = File.ReadAllLines(path);
                if (lines.Length < 2)
                    return;

                List<string[]> rows = lines
                    .Skip(1)
                    .Where(line => !string.IsNullOrWhiteSpace(line))
                    .Select(ParseCsvLine)
                    .ToList();
                string[] header = ParseCsvLine(lines[0]);
                int optimizationIndex = Array.IndexOf(header, "OptimizationRunId");
                int coverageIndex = Array.IndexOf(header, "CoverageRatio");
                int positionIndex = Array.IndexOf(header, "PositionCount");
                int fitnessIndex = Array.IndexOf(header, "FitnessScore");
                int bestIndex = Array.IndexOf(header, "IsBestStage");
                if (optimizationIndex < 0 || coverageIndex < 0 || positionIndex < 0
                    || fitnessIndex < 0 || bestIndex < 0)
                {
                    return;
                }

                string[]? best = rows
                    .Where(row => row.Length == header.Length
                        && row[optimizationIndex] == optimizationRunId)
                    .OrderByDescending(row => ParseDouble(row[coverageIndex]))
                    .ThenBy(row => ParseInt(row[positionIndex]))
                    .ThenByDescending(row => ParseDouble(row[fitnessIndex]))
                    .FirstOrDefault();

                if (best == null)
                    return;

                foreach (string[] row in rows.Where(row =>
                    row.Length == header.Length && row[optimizationIndex] == optimizationRunId))
                {
                    row[bestIndex] = ReferenceEquals(row, best) ? "true" : "false";
                }

                using var writer = new StreamWriter(path, false, new UTF8Encoding(true));
                writer.WriteLine(string.Join(",", header.Select(Escape)));
                foreach (string[] row in rows)
                {
                    writer.WriteLine(string.Join(",", row.Select(Escape)));
                }
            }
        }

        private static string[] ParseCsvLine(string line)
        {
            List<string> values = new();
            StringBuilder current = new();
            bool quoted = false;

            for (int index = 0; index < line.Length; index++)
            {
                char character = line[index];
                if (character == '"')
                {
                    if (quoted && index + 1 < line.Length && line[index + 1] == '"')
                    {
                        current.Append('"');
                        index++;
                    }
                    else
                    {
                        quoted = !quoted;
                    }
                }
                else if (character == ',' && !quoted)
                {
                    values.Add(current.ToString());
                    current.Clear();
                }
                else
                {
                    current.Append(character);
                }
            }

            values.Add(current.ToString());
            return values.ToArray();
        }

        private static double ParseDouble(string value)
        {
            return double.TryParse(value, NumberStyles.Float, Invariant, out double parsed)
                ? parsed
                : double.MinValue;
        }

        private static int ParseInt(string value)
        {
            return int.TryParse(value, NumberStyles.Integer, Invariant, out int parsed)
                ? parsed
                : int.MaxValue;
        }

        private static string Escape(string? value)
        {
            if (string.IsNullOrEmpty(value))
                return string.Empty;
            if (!value.Contains(',') && !value.Contains('"')
                && !value.Contains('\r') && !value.Contains('\n'))
            {
                return value;
            }

            return $"\"{value.Replace("\"", "\"\"")}\"";
        }

        private sealed class AssignmentMetric
        {
            public string ElementId { get; init; } = string.Empty;
            public string ElementType { get; init; } = string.Empty;
            public int AssignedPositionIndex { get; init; }
            public string AssignedZoneId { get; init; } = string.Empty;
            public string LiftStatus { get; init; } = string.Empty;
            public double? Radius { get; init; }
            public double? RequiredBoomLength { get; init; }
            public double ElementWeight { get; init; }
            public double RequiredCapacity { get; init; }
            public double? ChartCapacity { get; init; }
            public double? AvailableCapacity { get; init; }
            public double? CapacityUtilization { get; init; }
            public double? CapacityMarginTons { get; init; }
            public double? RadiusUtilization { get; init; }
            public double? BoomUtilization { get; init; }
            public string FailureReason { get; init; } = string.Empty;
            public bool IsFinalLifted { get; init; }
        }
    }
}
