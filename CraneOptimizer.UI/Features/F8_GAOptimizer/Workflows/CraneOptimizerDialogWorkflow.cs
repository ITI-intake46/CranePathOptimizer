using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Models;
using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Settings;
using CraneOptimizer.Domain.Entities;
using CraneOptimizer.Domain.Interfaces;
using CraneOptimizer.UI.Features.F8_GAOptimizer.DTOs;
using CraneOptimizer.UI.Features.F8_GAOptimizer.Services;
using System.Threading;

namespace CraneOptimizer.UI.Features.F8_GAOptimizer.Workflows
{
    public sealed class CraneOptimizerDialogWorkflow
    {
        private readonly ICranePlacementService _placementService;
        private readonly ICraneRepository _craneRepository;

        public CraneOptimizerDialogWorkflow(
            ICranePlacementService placementService,
            ICraneRepository craneRepository)
        {
            _placementService = placementService;
            _craneRepository = craneRepository;
        }

        public async Task<IReadOnlyList<Crane>> LoadCranesAsync()
        {
            return await _craneRepository.GetAllWithSpecsAsync();
        }

        public Task<CranePlacementResult> RunOptimizationAsync(
            Crane selectedCrane,
            CraneOptimizerDialogOptions options,
            CancellationToken cancellationToken)
        {
            var settings = BuildSettings(options);
            return _placementService.RunOptimizationAsync(settings, selectedCrane, cancellationToken);
        }

        public IReadOnlyList<CraneOptimizerPointListItem> CreatePointItems(CranePlacementResult result)
        {
            return result.Positions
                .Select(point => new CraneOptimizerPointListItem(point))
                .ToList();
        }

        public CraneOptimizerPointSummary CreatePointSummary(CranePositionResult point)
        {
            int liftable = point.ElementResults.Count(result => result.Status == LiftStatus.Liftable);
            int nonLiftable = point.ElementResults.Count(result =>
                result.Status == LiftStatus.Overweight || result.Status == LiftStatus.HeightFail);
            int unreachable = point.ElementResults.Count(result => result.Status == LiftStatus.OutOfRadius);

            int total = point.ElementResults.Count;
            double coverage = total == 0 ? 0 : (double)liftable / total;

            return new CraneOptimizerPointSummary(
                FormatPointCoordinates(point),
                liftable,
                nonLiftable,
                unreachable,
                $"Point Coverage: {coverage:P0}");
        }

        public GASettings BuildSettings(CraneOptimizerDialogOptions options)
        {
            double nonLiftablePenalty = ParseDouble(options.NonLiftablePenaltyText, 20.0);

            return new GASettings
            {
                PopulationSize = ParseInt(options.PopulationText, 30),
                MaxGenerations = ParseInt(options.GenerationsText, 30),
                MutationRate = ParseDouble(options.MutationRateText, 0.1),
                MutationSigma = ParseDouble(options.MutationSigmaText, 0.05),
                GridStep = ParseDouble(options.GridStepText, 2.0),
                ConvergenceThresholdPercent = ParseDouble(options.ConvergenceText, 0.0),
                RadiusPenaltyWeight = ParseDouble(options.UnreachablePenaltyText, 100.0),
                OverweightPenaltyWeight = nonLiftablePenalty,
                HeightPenaltyWeight = nonLiftablePenalty,
                RandomSeed = 42
            };
        }

        internal static string FormatPointCoordinates(CranePositionResult point)
        {
            return $"({point.X:F0}, {point.Y:F0})";
        }

        private static int ParseInt(string? value, int fallback)
        {
            return int.TryParse(value, out var parsed) ? parsed : fallback;
        }

        private static double ParseDouble(string? value, double fallback)
        {
            return double.TryParse(value, out var parsed) ? parsed : fallback;
        }
    }

    public sealed record CraneOptimizerDialogOptions(
        string? PopulationText,
        string? GenerationsText,
        string? MutationRateText,
        string? MutationSigmaText,
        string? GridStepText,
        string? ConvergenceText,
        string? NonLiftablePenaltyText,
        string? UnreachablePenaltyText);

    public sealed class CraneOptimizerPointListItem
    {
        public CraneOptimizerPointListItem(CranePositionResult point)
        {
            Point = point;
        }

        public CranePositionResult Point { get; }

        public string DisplayText =>
            $"Point {CraneOptimizerDialogWorkflow.FormatPointCoordinates(Point)} - {Point.ElementResults.Count} elements";
    }

    public sealed record CraneOptimizerPointSummary(
        string PositionText,
        int LiftableCount,
        int NonLiftableCount,
        int UnreachableCount,
        string CoverageText);
}
