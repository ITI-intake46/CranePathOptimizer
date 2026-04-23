using CraneOptimizer.Application.Features.CranePlacement.DTOs;
using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Interfaces;
using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Models;
using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Settings;
using CraneOptimizer.UI.Features.F0_CoordinateSystem;

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

        public async Task<CranePlacementResult> RunOptimizationAsync(GASettings settings)
        {
            var input = await _inputBuilder.BuildAsync(settings);

            var solution = await Task.Run(() => _gaEngine.Run(input));
            if (solution == null ||
                solution.Chromosome == null ||
                solution.Chromosome.CranePositions == null ||
                solution.Chromosome.CranePositions.Count == 0)
            {
                throw new InvalidOperationException("Optimization did not return a valid crane position.");
            }

            var assignment = _assignmentService.EvaluateAssignment(
                solution,
                input.Elements,
                input.Assignment,
                input.CraneSpec,
                input.Settings);

            return MapResult(solution, assignment);
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
