using CraneOptimizer.Application.Optimization.GeneticAlgorithms.DTOs;
using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Interfaces;
using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Models;
using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Settings;
using CraneOptimizer.Domain.Entities;
using CraneOptimizer.Domain.Schemas;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace CraneOptimizer.Application.Optimization.GeneticAlgorithms.Engines.PlacementOrchestration
{
    public class CranePlacementEngine : ICranePlacementEngine
    {
        private readonly IGeneticAlgorithmEngine _gaEngine;
        private readonly IElementAssignmentService _assignmentService;

        public CranePlacementEngine(
            IGeneticAlgorithmEngine gaEngine,
            IElementAssignmentService assignmentService)
        {
            _gaEngine = gaEngine;
            _assignmentService = assignmentService;
        }

        public PlacementResult RunOptimization(
            PlacementInput input,
            CancellationToken cancellationToken = default)
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

                remainingElements = allElements
                    .Where(element => !coveredElements.Contains(element.ElementID))
                    .ToList();
            }

            DeduplicateFinalPositions(finalResult, input.Settings);
            RecalculateCoverage(finalResult, allElements.Count);

            finalResult.FitnessScore = finalResult.CoverageRatio;
            finalResult.Positions = finalResult.Positions
                .OrderByDescending(position => position.NewLiftableCount)
                .ToList();

            RecalculateCoverage(finalResult, allElements.Count);

            return finalResult;
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

        private static void DeduplicateFinalPositions(
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
                    Distance2D(existing, candidate) < minimumDistance);

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

        private static void RecalculateCoverage(PlacementResult result, int totalElementCount)
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

        private static double Distance2D(CranePositionResult a, CranePositionResult b)
        {
            double dx = a.X - b.X;
            double dy = a.Y - b.Y;
            return Math.Sqrt(dx * dx + dy * dy);
        }
    }
}
