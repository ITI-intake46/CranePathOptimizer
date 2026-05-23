using CraneOptimizer.Application.Optimization.GeneticAlgorithms.DTOs;
using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Interfaces;
using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Models;
using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Settings;
using CraneOptimizer.Domain.Entities;
using CraneOptimizer.Domain.Schemas;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using CraneOptimizer.Application.Optimization.Utilities;

namespace CraneOptimizer.Application.Optimization.GeneticAlgorithms.Engines.PlacementOrchestration
{
    public class CranePlacementEngine : ICranePlacementEngine
    {
        private readonly IGeneticAlgorithmEngine _gaEngine;
        private readonly IElementAssignmentService _assignmentService;
        private readonly ICoverageEngine _coverageEngine;

        public CranePlacementEngine(
            IGeneticAlgorithmEngine gaEngine,
            IElementAssignmentService assignmentService,
            ICoverageEngine coverageEngine)
        {
            _gaEngine = gaEngine;
            _assignmentService = assignmentService;
            _coverageEngine = coverageEngine;
        }

        public PlacementResult RunOptimization(
            PlacementInput input,
            CancellationToken cancellationToken = default)
        {
            GAProfiler.Reset();

            using (new GAProfiler.ProfileScope("CranePlacementEngine.TotalRun"))
            {
                cancellationToken.ThrowIfCancellationRequested();

                var allElements = input.Elements;
                var remainingElements = new List<PrecastElement>(allElements);
                var coveredElements = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                var finalResult = new PlacementResult();

                double previousCoverage = 0;
                int maxPoints = 4;
                double targetCoverage = 0.95;
                double minImprovement = 0.05;
                int iteration = 0;

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

                finalResult.FitnessScore = finalResult.CoverageRatio;
                finalResult.Positions = finalResult.Positions
                    .OrderByDescending(position => position.NewLiftableCount)
                    .ToList();

                _coverageEngine.RecalculateCoverage(finalResult, allElements.Count);

                if (GAProfiler.Enabled)
                {
                    string summary = GAProfiler.GetSummaryReport();

                    // Main persisted profiling output: export final summary to a text file
                    try
                    {
                        string logDir = input.Settings.ProfilingLogDirectory;
                        if (string.IsNullOrWhiteSpace(logDir))
                        {
                            logDir = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CraneOptimizer", "Logs", "GA");
                        }
                        System.IO.Directory.CreateDirectory(logDir);
                        string fileName = $"ga_profile_{DateTime.Now:yyyyMMdd_HHmmss}.txt";
                        string fullPath = System.IO.Path.Combine(logDir, fileName);
                        System.IO.File.WriteAllText(fullPath, summary, System.Text.Encoding.UTF8);
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Failed to write profiling log: {ex.Message}");
                    }

                    // Keep Console and Debug output optional
                    Console.WriteLine(summary);
                    Debug.WriteLine(summary);
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
                RequiredCapacity = evaluation.RequiredCapacity
            };
        }

        private static void UpdateStatistics(LiftStatistics statistics, LiftStatus status)
        {
            switch (status)
            {
                case LiftStatus.Liftable:
                    statistics.Liftable++;
                    break;
                case LiftStatus.OutOfRadius:
                    statistics.OutOfRadius++;
                    break;
                case LiftStatus.Overweight:
                    statistics.Overweight++;
                    break;
                case LiftStatus.HeightFail:
                    statistics.HeightFail++;
                    break;
                case LiftStatus.SpecialCase:
                    statistics.SpecialCase++;
                    break;
            }
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
                    SpecialCase = src.Statistics.SpecialCase
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
                        RequiredCapacity = er.RequiredCapacity
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
