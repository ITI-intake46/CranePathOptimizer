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

                // Run GA on remaining elements only
                input.Elements = remainingElements;

                var solution = await Task.Run(
                    () => _gaEngine.Run(input, cancellationToken),
                    cancellationToken);

                var assignment = _assignmentService.EvaluateAssignment(
                    solution,
                    remainingElements,
                    input.Assignment,
                    input.CraneSpec,
                    input.Settings);

                var iterationResult = MapResult(solution, assignment);

                if (iterationResult.Positions.Count == 0)
                    break;

                var bestPoint = iterationResult.Positions[0];

                // Get NEW elements only (no duplication)
                var newElements = bestPoint.ElementResults
                    .Where(e => e.Status == LiftStatus.Liftable && !coveredElements.Contains(e.ElementId))
                    .ToList();

                // Stop if no new contribution
                if (newElements.Count == 0)
                    break;

                // Add to covered set
                foreach (var e in newElements)
                    coveredElements.Add(e.ElementId);

                // Add filtered result (only new elements)
                finalResult.Positions.Add(new CranePositionResult
                {
                    X = bestPoint.X,
                    Y = bestPoint.Y,
                    ElementResults = newElements
                });

                // Calculate coverage
                double currentCoverage = (double)coveredElements.Count / allElements.Count;

                double improvement = currentCoverage - previousCoverage;

                // Stop conditions
                if (currentCoverage >= targetCoverage)
                    break;

                if (improvement < minImprovement)
                    break;

                previousCoverage = currentCoverage;

                // Update remaining elements
                remainingElements = allElements
                    .Where(e => !coveredElements.Contains(e.ElementID))
                    .ToList();
            }

            // Final stats
            finalResult.CoverageRatio = (double)coveredElements.Count / allElements.Count;
            finalResult.FitnessScore = finalResult.CoverageRatio;
            finalResult.Positions = finalResult.Positions
                .OrderByDescending(p => p.ElementResults.Count)
                .ToList();
            return finalResult;
        }

        private CranePlacementResult MapResult(
            CraneSolution solution,
            AssignmentResult assignment)
        {
            var result = new CranePlacementResult
            {
                FitnessScore = solution.Fitness
            };

            var positions = solution.Chromosome.CranePositions;
            result.Positions = new List<CranePositionResult>(positions.Count);

            for (int i = 0; i < positions.Count; i++)
            {
                var scenePoint = _coordinateService.DenormalizePoint(positions[i]);
                result.Positions.Add(new CranePositionResult
                {
                    X = scenePoint.X,
                    Y = scenePoint.Y
                });
            }

            var statistics = new LiftStatistics();
            var evaluationResults = assignment.EvaluationResults;

            for (int i = 0; i < evaluationResults.Count; i++)
            {
                var evaluation = evaluationResults[i];
                UpdateStatistics(statistics, evaluation.Status);

                if (evaluation.AssignedCraneIndex < 0 || evaluation.AssignedCraneIndex >= result.Positions.Count)
                    continue;

                result.Positions[evaluation.AssignedCraneIndex].ElementResults.Add(new ElementLiftResult
                {
                    ElementId = evaluation.ElementId,
                    Status = evaluation.Status,
                    Radius = evaluation.Radius,
                    RequiredCapacity = evaluation.RequiredCapacity
                });
            }

            result.Statistics = statistics;

            int totalElements = evaluationResults.Count;
            result.CoverageRatio = totalElements == 0
                ? 0
                : (double)statistics.Liftable / totalElements;

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
