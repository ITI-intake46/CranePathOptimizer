using CraneOptimizer.Application.Optimization.GeneticAlgorithms.DTOs;
using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Interfaces;
using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Models;
using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Settings;
using CraneOptimizer.Domain.Common;
using CraneOptimizer.Domain.Entities;
using CraneOptimizer.Domain.Schemas;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using CraneOptimizer.Application.Optimization.Utilities;
using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Services;

namespace CraneOptimizer.Application.Optimization.GeneticAlgorithms.Engines.PlacementOrchestration
{
    public class CranePlacementEngine : ICranePlacementEngine
    {
        private readonly IGeneticAlgorithmEngine _gaEngine;
        private readonly IElementAssignmentService _assignmentService;
        private readonly ICoverageEngine _coverageEngine;
        private readonly ICraneConfigurationEngine _configEngine;
        private readonly IGACsvDiagnosticsService _diagnosticsService;
        private readonly IReachabilityMapService? _reachabilityMapService;

        public CranePlacementEngine(
            IGeneticAlgorithmEngine gaEngine,
            IElementAssignmentService assignmentService,
            ICoverageEngine coverageEngine,
            ICraneConfigurationEngine configEngine,
            IGACsvDiagnosticsService diagnosticsService)
            : this(gaEngine, assignmentService, coverageEngine, configEngine, diagnosticsService, null)
        {
        }

        public CranePlacementEngine(
            IGeneticAlgorithmEngine gaEngine,
            IElementAssignmentService assignmentService,
            ICoverageEngine coverageEngine,
            ICraneConfigurationEngine configEngine,
            IGACsvDiagnosticsService diagnosticsService,
            IReachabilityMapService? reachabilityMapService)
        {
            _gaEngine = gaEngine;
            _assignmentService = assignmentService;
            _coverageEngine = coverageEngine;
            _configEngine = configEngine;
            _diagnosticsService = diagnosticsService;
            _reachabilityMapService = reachabilityMapService;
        }

        public PlacementResult RunOptimization(
            PlacementInput input,
            CancellationToken cancellationToken = default)
        {
            var sw = Stopwatch.StartNew();
            if (string.IsNullOrEmpty(input.Settings.OptimizationRunId))
            {
                input.Settings.OptimizationRunId = Guid.NewGuid().ToString();
            }
            if (string.IsNullOrEmpty(input.Settings.StageRunId))
            {
                input.Settings.StageRunId = Guid.NewGuid().ToString();
            }

            GAProfiler.Enabled = input.Settings.EnableProfiling;

            if (GAProfiler.CurrentRunId != input.Settings.OptimizationRunId)
            {
                GAProfiler.Reset();
                GAProfiler.CurrentRunId = input.Settings.OptimizationRunId;
            }

            _configEngine.ClearCache();

            if (GAProfiler.Enabled)
            {
                GAProfiler.ProjectName = input.ProjectName;
                GAProfiler.ProjectFolderPath = input.ProjectFolderPath;
                GAProfiler.IfcFileName = input.IfcFileName;
                GAProfiler.CraneNameOrId = input.CraneSpec?.Crane?.Name ?? (input.CraneSpec != null ? $"Spec ID: {input.CraneSpec.Id}" : null);
                GAProfiler.TotalTargetElementsCount = input.TotalElementsCount;
                GAProfiler.SelectedFilteredElementsCount = input.Elements?.Count ?? 0;
                GAProfiler.PopulationSize = input.Settings.PopulationSize;
                GAProfiler.ConfiguredGenerationCount = input.Settings.MaxGenerations;

                // Capture managed memory and GC counts before optimization (approximate managed memory metrics)
                GAProfiler.MemoryBeforeBytes = GC.GetTotalMemory(false);
                GAProfiler.GcCount0Before = GC.CollectionCount(0);
                GAProfiler.GcCount1Before = GC.CollectionCount(1);
                GAProfiler.GcCount2Before = GC.CollectionCount(2);
            }

            using (new GAProfiler.ProfileScope("CranePlacementEngine.TotalRun"))
            {
                cancellationToken.ThrowIfCancellationRequested();

                var allElements = input.Elements;
                var remainingElements = new List<PrecastElement>(allElements);
                var coveredElements = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                var finalResult = new PlacementResult();
                LiftStatistics? concurrentStats = null;

                double previousCoverage = 0;
                int maxPoints = 4;
                double targetCoverage = 0.95;
                double minImprovement = 0.05;
                int iteration = 0;

                if (input.Settings.EnableConcurrentOptimization)
                {
                    using (new GAProfiler.ProfileScope("CranePlacementEngine.ConcurrentRun"))
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        iteration = 1;

                        input.Elements = allElements; // Optimizing all elements together

                        var solution = _gaEngine.Run(input, cancellationToken);

                        var concurrentResult = MapResult(
                            solution,
                            allElements,
                            input.CraneSpec,
                            input.Settings);

                        concurrentStats = concurrentResult.Statistics;
                        finalResult.FitnessScore = concurrentResult.FitnessScore;

                        foreach (var pos in concurrentResult.Positions)
                        {
                            finalResult.Positions.Add(new CranePositionResult
                            {
                                X = pos.X,
                                Y = pos.Y,
                                Z = pos.Z,
                                NewLiftableCount = pos.ElementResults.Count(r => r.Status == LiftStatus.Liftable),
                                ElementResults = pos.ElementResults
                            });
                        }
                    }
                }
                else
                {
                    while (remainingElements.Count > 0 && iteration < maxPoints)
                    {
                        using (new GAProfiler.ProfileScope("CranePlacementEngine.SetCoverIteration"))
                        {
                            cancellationToken.ThrowIfCancellationRequested();
                            iteration++;

                            input.Elements = remainingElements;

                            var solution = _gaEngine.Run(input, cancellationToken);

                            var iterationResult = MapResult(
                                solution,
                                allElements,
                                input.CraneSpec,
                                input.Settings);

                            if (iterationResult.Positions.Count == 0)
                                break;

                            var bestPoint = iterationResult.Positions
                                .OrderByDescending(point => point.ElementResults.Count(result => result.Status == LiftStatus.Liftable && !coveredElements.Contains(result.ElementId)))
                                .ThenByDescending(point => point.LiftableCount)
                                .First();

                            var newElements = bestPoint.ElementResults
                                .Where(result => result.Status == LiftStatus.Liftable && !coveredElements.Contains(result.ElementId))
                                .ToList();

                            if (newElements.Count == 0)
                                break;

                            foreach (var element in newElements)
                                coveredElements.Add(element.ElementId);

                            finalResult.Positions.Add(new CranePositionResult
                            {
                                X = bestPoint.X,
                                Y = bestPoint.Y,
                                Z = bestPoint.Z,
                                NewLiftableCount = newElements.Count,
                                ElementResults = bestPoint.ElementResults
                            });

                            double currentCoverage = allElements.Count == 0
                                ? 0
                                : (double)coveredElements.Count / allElements.Count;

                            double improvement = currentCoverage - previousCoverage;

                            if (currentCoverage >= targetCoverage)
                                break;

                            if (improvement < minImprovement)
                                break;

                            previousCoverage = currentCoverage;

                            remainingElements = _coverageEngine.FilterRemainingElements(allElements, coveredElements);
                        }
                    }
                }

#if DEBUG
                // ══════════════════════════════════════════════════════════════
                // GOLDEN MASTER COMPARISON (Phase 4 Verification)
                // ══════════════════════════════════════════════════════════════
                var cloneLegacy = ClonePlacementResult(finalResult);
                var cloneNew = ClonePlacementResult(finalResult);

                LegacyDeduplicateFinalPositions(cloneLegacy, input.Settings);
                LegacyRecalculateCoverage(cloneLegacy, allElements.Count);

                _coverageEngine.DeduplicateFinalPositions(cloneNew, input.Settings);
                _coverageEngine.RecalculateCoverage(cloneNew, allElements.Count);

                if (cloneLegacy.LiftedElementCount != cloneNew.LiftedElementCount)
                    throw new InvalidOperationException($"LiftedElementCount mismatch! New: {cloneNew.LiftedElementCount}, Legacy: {cloneLegacy.LiftedElementCount}");
                if (Math.Abs(cloneLegacy.CoverageRatio - cloneNew.CoverageRatio) > 1e-7)
                    throw new InvalidOperationException($"CoverageRatio mismatch! New: {cloneNew.CoverageRatio}, Legacy: {cloneLegacy.CoverageRatio}");
                if (cloneLegacy.Positions.Count != cloneNew.Positions.Count)
                    throw new InvalidOperationException($"Deduplicated Positions Count mismatch! New: {cloneNew.Positions.Count}, Legacy: {cloneLegacy.Positions.Count}");
                for (int i = 0; i < cloneLegacy.Positions.Count; i++)
                {
                    if (cloneLegacy.Positions[i].NewLiftableCount != cloneNew.Positions[i].NewLiftableCount)
                        throw new InvalidOperationException($"NewLiftableCount mismatch at index {i}! New: {cloneNew.Positions[i].NewLiftableCount}, Legacy: {cloneLegacy.Positions[i].NewLiftableCount}");
                }
                // ══════════════════════════════════════════════════════════════
#endif

                _coverageEngine.DeduplicateFinalPositions(finalResult, input.Settings);
                _coverageEngine.RecalculateCoverage(finalResult, allElements.Count);
                finalResult.Positions = finalResult.Positions
                    .OrderByDescending(position => position.NewLiftableCount)
                    .ToList();

                // Resolve final unique assignments
                AssignmentPolicy.ResolveFinalAssignments(finalResult.Positions, allElements, input.CraneSpec!, input.Settings);

                // Validation: no ElementGlobalId has more than one final AssignedPointId
                var assignedTracker = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                foreach (var pos in finalResult.Positions)
                {
                    foreach (var er in pos.ElementResults)
                    {
                        if (er.IsFinalAssignedToPoint)
                        {
                            if (assignedTracker.TryGetValue(er.ElementId, out var existingPointId))
                            {
                                throw new InvalidOperationException($"Duplicate assignment detected! Element {er.ElementId} is assigned to both {existingPointId} and {er.AssignedPointId}.");
                            }
                            assignedTracker[er.ElementId] = er.AssignedPointId;
                        }
                    }
                }

                _coverageEngine.RecalculateCoverage(finalResult, allElements.Count);

                // Filter out any setup positions that do not lift any elements uniquely (zero lifting points)
                finalResult.Positions = finalResult.Positions
                    .Where(p => p.NewLiftableCount > 0)
                    .ToList();

                if (GAProfiler.Enabled)
                {
                    // Capture managed memory and GC counts after optimization (approximate managed memory metrics)
                    GAProfiler.MemoryAfterBytes = GC.GetTotalMemory(false);
                    GAProfiler.GcCount0After = GC.CollectionCount(0);
                    GAProfiler.GcCount1After = GC.CollectionCount(1);
                    GAProfiler.GcCount2After = GC.CollectionCount(2);

                    // Set remaining results metadata
                    GAProfiler.GreedySetCoverIterationCount = iteration;
                    GAProfiler.FinalCraneSetupCount = finalResult.Positions.Count;
                    GAProfiler.LiftedElementCount = finalResult.LiftedElementCount;
                    GAProfiler.CoverageRatio = finalResult.CoverageRatio;

                    // Determine log file directory and file name
                    string logDir = input.Settings.ProfilingLogDirectory;
                    if (string.IsNullOrWhiteSpace(logDir))
                    {
                        logDir = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CraneOptimizer", "Logs", "GA");
                    }
                    System.IO.Directory.CreateDirectory(logDir);
                    string fileName = $"ga_profile_{DateTime.Now:yyyyMMdd_HHmmss}.txt";
                    string fullPath = System.IO.Path.Combine(logDir, fileName);

                    GAProfiler.ProfilingLogPath = fullPath;

                    string summary = GAProfiler.GetSummaryReport();

                    // Main persisted profiling output: export final summary to a text file
                    try
                    {
                        System.IO.File.WriteAllText(fullPath, summary, System.Text.Encoding.UTF8);
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Failed to write profiling log: {ex.Message}");
                    }

                    // Keep Console and Debug output optional (do not add console spam)
                    Console.WriteLine(summary);
                }

                // Populate final Statistics
                if (input.Settings.EnableConcurrentOptimization && concurrentStats != null)
                {
                    finalResult.Statistics = concurrentStats;
                    finalResult.Statistics.Liftable = finalResult.LiftedElementCount;
                }
                else
                {
                    finalResult.Statistics = new LiftStatistics
                    {
                        Liftable = finalResult.LiftedElementCount
                    };
                }

                // Generate project-scoped diagnostics reports post-run (Phase 8)
                try
                {
                    input.Elements = allElements;

                    if (GAProfiler.Enabled)
                    {
                        var usedZoneIdsList = finalResult.Positions.Select(pos => {
                            var containingZone = input.Zones?.FirstOrDefault(z => SpatialMath.PointInPolygon(new DomainPoint3D(pos.X, pos.Y, 0), z.Vertices));
                            return containingZone?.ZoneId.ToString() ?? Guid.Empty.ToString();
                        }).Distinct().ToList();

                        string usedZoneIds = string.Join(";", usedZoneIdsList);

                        string stageTermReason = "MaxGenerationsReached";
                        if (cancellationToken.IsCancellationRequested) stageTermReason = "Cancelled";
                        else if (GAProfiler.StoppedByConvergence) stageTermReason = "Convergence";
                        else if (GAProfiler.ActualGenerationLoopCount < input.Settings.EarlyStoppingGenerations) stageTermReason = "EarlyStopping";

                        double stageTime = sw.ElapsedMilliseconds;

                        var stageLog = new GAProfiler.StageLogEntry
                        {
                            RunId = input.Settings.RunId ?? string.Empty,
                            OptimizationRunId = input.Settings.OptimizationRunId ?? string.Empty,
                            StageRunId = input.Settings.StageRunId ?? string.Empty,
                            StageIndex = input.Settings.EnableMultiStageDiscovery ? input.Settings.MaxCranePositions : 1,
                            MaxPositions = input.Settings.MaxCranePositions,
                            RelocationWeight = input.Settings.RelocationWeight,
                            BestCoverageRatio = finalResult.CoverageRatio,
                            BestCoveragePercent = finalResult.CoverageRatio * 100.0,
                            BestLiftedCount = finalResult.LiftedElementCount,
                            BestFitnessScore = finalResult.FitnessScore,
                            RawPositionCount = finalResult.Positions.Count,
                            FinalPositionCount = finalResult.Positions.Count,
                            UsedZoneIds = usedZoneIds,
                            RuntimeMs = stageTime,
                            Escalated = finalResult.CoverageRatio < input.Settings.MultiStageCoverageTarget,
                            TerminationReason = stageTermReason
                        };
                        GAProfiler.StageLogs.Add(stageLog);
                    }

                    finalResult.DiagnosticsReportPath = _diagnosticsService.GenerateDiagnostics(input, finalResult);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Failed to generate GA CSV diagnostics: {ex.Message}");
                    Console.WriteLine($"Failed to generate GA CSV diagnostics: {ex.Message}");
                }

                finalResult.ReachabilityDiagnostics = _reachabilityMapService?.LastDiagnostics;

                if (!input.Settings.EnableConcurrentOptimization)
                {
                    finalResult.FitnessScore = finalResult.CoverageRatio;
                }

                return finalResult;
            }
        }

        private PlacementResult MapResult(
            CraneSolution solution,
            List<PrecastElement> elements,
            CraneSpecification craneSpec,
            GASettings settings)
        {
            var result = new PlacementResult
            {
                FitnessScore = solution.Fitness,
                Positions = new List<CranePositionResult>(solution.Chromosome.CranePositions.Count)
            };

            var elementLookup = elements
                .Where(element => !string.IsNullOrWhiteSpace(element.ElementID))
                .GroupBy(element => element.ElementID, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);

            for (int i = 0; i < solution.Chromosome.CranePositions.Count; i++)
            {
                var chromosomePoint = solution.Chromosome.CranePositions[i];
                var evaluations = _assignmentService.EvaluatePosition(
                    solution,
                    i,
                    elements,
                    craneSpec,
                    settings);

                var point = new CranePositionResult
                {
                    X = chromosomePoint.X,
                    Y = chromosomePoint.Y,
                    Z = chromosomePoint.Z,
                    ElementResults = evaluations
                        .Select(evaluation => MapElementResult(evaluation, elementLookup))
                        .ToList()
                };

                result.Positions.Add(point);

                foreach (var evaluation in evaluations)
                    UpdateStatistics(result.Statistics, evaluation.Status);
            }

            int totalElements = result.Positions.Sum(position => position.ElementResults.Count);
            result.CoverageRatio = totalElements == 0
                ? 0
                : (double)result.Statistics.Liftable / totalElements;

            return result;
        }

        private ElementLiftResult MapElementResult(
            ElementEvaluationResult evaluation,
            IReadOnlyDictionary<string, PrecastElement> elementLookup)
        {
            elementLookup.TryGetValue(evaluation.ElementId, out var element);

            var cog = element == null
                ? new Domain.Common.DomainPoint3D()
                : element.ElementCOG;

            return new ElementLiftResult
            {
                ElementId = evaluation.ElementId,
                ElementMark = element?.ElementMark,
                ElementType = element?.ElementType,
                Weight = element?.ElementWT,
                CogX = cog.X,
                CogY = cog.Y,
                CogZ = cog.Z,
                Status = evaluation.Status,
                Radius = evaluation.Radius,
                BoomLength = evaluation.BoomLength,
                AvailableCapacity = evaluation.AvailableCapacity,
                RequiredCapacity = evaluation.RequiredCapacity,
                ReachStatus = evaluation.ReachStatus,
                ChartStatus = evaluation.ChartStatus,
                IsExactMatch = evaluation.IsExactMatch,
                IsSteppedRadius = evaluation.IsSteppedRadius,
                IsBlankCell = evaluation.IsBlankCell,
                IsZeroCell = evaluation.IsZeroCell,
                SafetyMargin = evaluation.SafetyMargin,
                LookupMode = evaluation.LookupMode,
                RequiredRadius = evaluation.RequiredRadius,
                SelectedChartRadius = evaluation.SelectedChartRadius,
                SelectedChartBoomLength = evaluation.SelectedChartBoomLength,
                CraneBaseX = evaluation.CraneBaseX,
                CraneBaseY = evaluation.CraneBaseY,
                CraneBaseZ = evaluation.CraneBaseZ,
                SlewCenterX = evaluation.SlewCenterX,
                SlewCenterY = evaluation.SlewCenterY,
                SlewCenterZ = evaluation.SlewCenterZ,
                BoomHingeX = evaluation.BoomHingeX,
                BoomHingeY = evaluation.BoomHingeY,
                BoomHingeZ = evaluation.BoomHingeZ,
                LoadPointX = evaluation.LoadPointX,
                LoadPointY = evaluation.LoadPointY,
                LoadPointZ = evaluation.LoadPointZ,
                RadiusOriginType = evaluation.RadiusOriginType,
                BoomOriginType = evaluation.BoomOriginType,
                CandidateBoomsChecked = evaluation.CandidateBoomsChecked,
                RejectedCandidates = evaluation.RejectedCandidates
            };
        }

        private static void UpdateStatistics(LiftStatistics statistics, LiftStatus status)
        {
            LiftClassificationPolicy.UpdateStatistics(statistics, status);
        }

#if DEBUG
        // ══════════════════════════════════════════════════════════════
        // LEGACY COPIES Guarantees 100% Verification (Phase 4 Verification)
        // ══════════════════════════════════════════════════════════════

        private static PlacementResult ClonePlacementResult(PlacementResult src)
        {
            var dst = new PlacementResult
            {
                FitnessScore = src.FitnessScore,
                CoverageRatio = src.CoverageRatio,
                LiftedElementCount = src.LiftedElementCount,
                TotalElementCount = src.TotalElementCount,
                Statistics = new LiftStatistics
                {
                    Liftable = src.Statistics.Liftable,
                    OutOfRadius = src.Statistics.OutOfRadius,
                    Overweight = src.Statistics.Overweight,
                    HeightFail = src.Statistics.HeightFail,
                    SpecialCase = src.Statistics.SpecialCase,
                    NotCharted = src.Statistics.NotCharted
                },
                Positions = src.Positions.Select(p => new CranePositionResult
                {
                    X = p.X,
                    Y = p.Y,
                    Z = p.Z,
                    NewLiftableCount = p.NewLiftableCount,
                    ElementResults = p.ElementResults.Select(er => new ElementLiftResult
                    {
                        ElementId = er.ElementId,
                        ElementMark = er.ElementMark,
                        ElementType = er.ElementType,
                        Weight = er.Weight,
                        CogX = er.CogX,
                        CogY = er.CogY,
                        CogZ = er.CogZ,
                        Status = er.Status,
                        Radius = er.Radius,
                        BoomLength = er.BoomLength,
                        AvailableCapacity = er.AvailableCapacity,
                        RequiredCapacity = er.RequiredCapacity,
                        ReachStatus = er.ReachStatus,
                        ChartStatus = er.ChartStatus,
                        IsExactMatch = er.IsExactMatch,
                        IsSteppedRadius = er.IsSteppedRadius,
                        IsBlankCell = er.IsBlankCell,
                        IsZeroCell = er.IsZeroCell,
                        SafetyMargin = er.SafetyMargin,
                        LookupMode = er.LookupMode,
                        RequiredRadius = er.RequiredRadius,
                        SelectedChartRadius = er.SelectedChartRadius,
                        SelectedChartBoomLength = er.SelectedChartBoomLength,
                        CraneBaseX = er.CraneBaseX,
                        CraneBaseY = er.CraneBaseY,
                        CraneBaseZ = er.CraneBaseZ,
                        SlewCenterX = er.SlewCenterX,
                        SlewCenterY = er.SlewCenterY,
                        SlewCenterZ = er.SlewCenterZ,
                        BoomHingeX = er.BoomHingeX,
                        BoomHingeY = er.BoomHingeY,
                        BoomHingeZ = er.BoomHingeZ,
                        LoadPointX = er.LoadPointX,
                        LoadPointY = er.LoadPointY,
                        LoadPointZ = er.LoadPointZ,
                        RadiusOriginType = er.RadiusOriginType,
                        BoomOriginType = er.BoomOriginType,
                        CandidateBoomsChecked = er.CandidateBoomsChecked,
                        RejectedCandidates = er.RejectedCandidates
                    }).ToList()
                }).ToList()
            };
            return dst;
        }

        private static void LegacyDeduplicateFinalPositions(
            PlacementResult result,
            GASettings settings)
        {
            if (result.Positions.Count < 2)
                return;

            double minimumDistanceMeters = Math.Max(
                settings.DuplicatePositionDistance,
                settings.MinimumRelocationDistance);

            if (minimumDistanceMeters <= 0)
                return;

            double minimumDistance = minimumDistanceMeters;

            var filtered = new List<CranePositionResult>();

            foreach (var candidate in result.Positions)
            {
                int nearIndex = filtered.FindIndex(existing =>
                    LegacyDistance2D(existing, candidate) < minimumDistance);

                if (nearIndex < 0)
                {
                    filtered.Add(candidate);
                    continue;
                }

                var existing = filtered[nearIndex];
                if (candidate.NewLiftableCount > existing.NewLiftableCount
                    || (candidate.NewLiftableCount == existing.NewLiftableCount
                        && candidate.LiftableCount > existing.LiftableCount))
                {
                    filtered[nearIndex] = candidate;
                }
            }

            result.Positions = filtered;
        }

        private static void LegacyRecalculateCoverage(PlacementResult result, int totalElementCount)
        {
            var covered = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var position in result.Positions)
            {
                var newlyCovered = position.ElementResults
                    .Where(item => item.Status == LiftStatus.Liftable && covered.Add(item.ElementId))
                    .Count();

                position.NewLiftableCount = newlyCovered;
            }

            result.LiftedElementCount = covered.Count;
            result.TotalElementCount = totalElementCount;
            result.CoverageRatio = totalElementCount == 0
                ? 0
                : (double)covered.Count / totalElementCount;
        }

        private static double LegacyDistance2D(CranePositionResult a, CranePositionResult b)
        {
            double dx = a.X - b.X;
            double dy = a.Y - b.Y;
            return Math.Sqrt(dx * dx + dy * dy);
        }
#endif
    }
}
