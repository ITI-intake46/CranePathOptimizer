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

            finalResult.CoverageRatio = allElements.Count == 0
                ? 0
                : (double)coveredElements.Count / allElements.Count;
            finalResult.FitnessScore = finalResult.CoverageRatio;
            finalResult.Positions = finalResult.Positions
                .OrderByDescending(position => position.NewLiftableCount)
                .ToList();

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
                        .Select(evaluation => new ElementLiftResult
                        {
                            ElementId = evaluation.ElementId,
                            Status = evaluation.Status,
                            Radius = evaluation.Radius,
                            RequiredCapacity = evaluation.RequiredCapacity
                        })
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
    }
}
