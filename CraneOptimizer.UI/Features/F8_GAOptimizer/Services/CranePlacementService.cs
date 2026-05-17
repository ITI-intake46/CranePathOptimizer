using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Interfaces;
using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Models;
using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Settings;
using CraneOptimizer.Domain.Entities;
using CraneOptimizer.Domain.Schemas;
using CraneOptimizer.UI.Features.F0_CoordinateSystem;
using CraneOptimizer.UI.Features.F8_GAOptimizer.DTOs;
using System.Threading;

namespace CraneOptimizer.UI.Features.F8_GAOptimizer.Services
{
    public class CranePlacementService : ICranePlacementService
    {
        private readonly GAInputBuilder _inputBuilder;
        private readonly IGeneticAlgorithmEngine _gaEngine;
        private readonly IElementAssignmentService _assignmentService;
        private readonly CoordinateService _coordinateService;

        public CranePlacementService(
            GAInputBuilder inputBuilder,
            IGeneticAlgorithmEngine gaEngine,
            IElementAssignmentService assignmentService,
            CoordinateService coordinateService)
        {
            _inputBuilder = inputBuilder;
            _gaEngine = gaEngine;
            _assignmentService = assignmentService;
            _coordinateService = coordinateService;
        }

        public async Task<CranePlacementResult> RunOptimizationAsync(
            GASettings settings,
            Crane crane,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var input = await _inputBuilder.BuildAsync(settings, crane);

            var allElements = input.Elements;
            var remainingElements = new List<PrecastElement>(allElements);
            var coveredElements = new HashSet<string>();
            var finalResult = new CranePlacementResult();

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

                var solution = await Task.Run(
                    () => _gaEngine.Run(input, cancellationToken),
                    cancellationToken);

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
            WriteDebugReport(finalResult);

            return finalResult;
        }

        private CranePlacementResult MapResult(
            CraneSolution solution,
            List<PrecastElement> elements,
            CraneSpecification craneSpec,
            GASettings settings)
        {
            var result = new CranePlacementResult
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
                var scenePoint = _coordinateService.DenormalizePoint(solution.Chromosome.CranePositions[i]);
                var evaluations = _assignmentService.EvaluatePosition(
                    solution,
                    i,
                    elements,
                    craneSpec,
                    settings);

                var point = new CranePositionResult
                {
                    X = scenePoint.X,
                    Y = scenePoint.Y,
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

            var sceneCog = element == null
                ? new Domain.Common.DomainPoint3D()
                : _coordinateService.DenormalizePoint(element.ElementCOG);

            return new ElementLiftResult
            {
                ElementId = evaluation.ElementId,
                ElementMark = element?.ElementMark,
                ElementType = element?.ElementType,
                Weight = element?.ElementWT,
                CogX = sceneCog.X,
                CogY = sceneCog.Y,
                CogZ = sceneCog.Z,
                Status = evaluation.Status,
                Radius = evaluation.Radius,
                BoomLength = evaluation.BoomLength,
                AvailableCapacity = evaluation.AvailableCapacity,
                RequiredCapacity = evaluation.RequiredCapacity
            };
        }

        private static void WriteDebugReport(CranePlacementResult result)
        {
            try
            {
                result.DebugReportPath = GADebugReportWriter.Write(result);
                result.DebugReportError = null;
            }
            catch (Exception ex)
            {
                result.DebugReportPath = null;
                result.DebugReportError = ex.Message;
            }
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

        /// <summary>
        /// Filters final crane setup points so small adjustments are not presented
        /// as separate crane relocations. Inputs are the optimization result and GA settings;
        /// the output is an in-place list where near-identical points keep the stronger
        /// coverage candidate and discard the weaker duplicate.
        /// </summary>
        private static void DeduplicateFinalPositions(
            CranePlacementResult result,
            GASettings settings)
        {
            if (result.Positions.Count < 2)
                return;

            double minimumDistanceMeters = Math.Max(
                settings.DuplicatePositionDistance,
                settings.MinimumRelocationDistance);

            if (minimumDistanceMeters <= 0)
                return;

            double minimumDistance = minimumDistanceMeters * CoordinateService.MetersToMillimetersScale;

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

        /// <summary>
        /// Recomputes total coverage and per-point new coverage after final de-duplication.
        /// Inputs are the filtered result and original element count; outputs are updated
        /// NewLiftableCount and CoverageRatio values consistent with displayed points.
        /// </summary>
        private static void RecalculateCoverage(CranePlacementResult result, int totalElementCount)
        {
            var covered = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var position in result.Positions)
            {
                var newlyCovered = position.ElementResults
                    .Where(item => item.Status == LiftStatus.Liftable && covered.Add(item.ElementId))
                    .Count();

                position.NewLiftableCount = newlyCovered;
            }

            result.CoverageRatio = totalElementCount == 0
                ? 0
                : (double)covered.Count / totalElementCount;
        }

        /// <summary>
        /// Calculates horizontal distance between two displayed crane positions.
        /// F8 result points are denormalized back to millimeters before display, so the
        /// caller converts engineering thresholds from meters to millimeters first.
        /// </summary>
        private static double Distance2D(CranePositionResult a, CranePositionResult b)
        {
            double dx = a.X - b.X;
            double dy = a.Y - b.Y;
            return Math.Sqrt(dx * dx + dy * dy);
        }
    }
}
