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
            int liftable = point.LiftableCount;
            int nonLiftable = point.ElementResults.Count(r =>
                r.Status == LiftStatus.Overweight ||
                r.Status == LiftStatus.HeightFail ||
                r.Status == LiftStatus.SpecialCase);
            int unreachable = point.ElementResults.Count(r => r.Status == LiftStatus.OutOfRadius);

            int total = point.ElementResults.Count;
            double coverage = total == 0 ? 0 : (double)liftable / total;

            string coverageText = point.NewLiftableCount > 0
                ? $"Point Coverage: {coverage:P0}  (+{point.NewLiftableCount} newly covered)"
                : $"Point Coverage: {coverage:P0}";

            return new CraneOptimizerPointSummary(
                FormatPointCoordinates(point),
                liftable,
                nonLiftable,
                unreachable,
                coverageText);
        }

        public GASettings BuildSettings(CraneOptimizerDialogOptions options)
        {
            return new GASettings
            {
                CoverageWeight = ParseDouble(options.CoverageWeightText, 10.0),
                RelocationWeight = ParseDouble(options.RelocationWeightText, 5.0),
                WireLength = ParseDouble(options.WireLengthText, 2.0),
                AdditionalWt = ParseDouble(options.AdditionalWtText, 0.0),
                DeratingRatio = Clamp(ParseDouble(options.DeratingRatioText, 0.9), 0.0, 1.0),
                SafetyFactor = Clamp(ParseDouble(options.SafetyFactorText, 0.95), 0.0, 1.0),
                MaxCranePositions = ParseInt(options.MaxCranePositionsText, 1),
                EarlyStoppingGenerations = ParseInt(options.EarlyStoppingGenerationsText, 20),
                GridStep = ParseDouble(options.GridStepText, 0.0),
                RadiusPenaltyWeight = ParseDouble(options.RadiusPenaltyWeightText, 3.0),
                BalancePenaltyWeight = ParseDouble(options.BalancePenaltyWeightText, 2.0),
                OverweightPenaltyWeight = ParseDouble(options.OverweightPenaltyWeightText, 10.0),
                HeightPenaltyWeight = ParseDouble(options.HeightPenaltyWeightText, 5.0),
                RadiusStdDevPenaltyWeight = ParseDouble(options.RadiusStdDevPenaltyWeightText, 2.0),
                SimilarPositionPenaltyWeight = ParseDouble(options.SimilarPositionPenaltyWeightText, 8.0),
                RelocationDistancePenaltyWeight = ParseDouble(options.RelocationDistancePenaltyWeightText, 1.5),
                MinimumRelocationDistance = ParseDouble(options.MinimumRelocationDistanceText, 10.0),
                DuplicatePositionDistance = ParseDouble(options.DuplicatePositionDistanceText, 3.0),
                MutationExplorationMultiplier = ParseDouble(options.MutationExplorationMultiplierText, 2.0),
                PositionSamplingAttempts = ParseInt(options.PositionSamplingAttemptsText, 80),
                MaxRadiusForNormalisation = ParseDouble(options.MaxRadiusForNormalisationText, 50.0),
                MutationRate = ParseDouble(options.DevMutationRateText, 0.1),
                MutationSigma = ParseDouble(options.DevMutationSigmaText, 0.05),
                CrossoverRate = ParseDouble(options.DevCrossoverRateText, 0.8),
                RandomSeed = ParseNullableInt(options.DevRandomSeedText, 42),
                ConvergenceThresholdPercent = ParseDouble(options.DevConvergenceText, 0.0),
                PopulationSize = ParseInt(options.PopulationSizeText, 30),
                MaxGenerations = ParseInt(options.MaxGenerationsText, 30)
            };
        }

        private static double Clamp(double value, double min, double max)
        {
            if (value < min) return min;
            if (value > max) return max;
            return value;
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

        private static int? ParseNullableInt(string? value, int? fallback)
        {
            if (string.IsNullOrWhiteSpace(value))
                return null;

            return int.TryParse(value, out var parsed) ? parsed : fallback;
        }
    }

    public sealed record CraneOptimizerDialogOptions(
        string? CoverageWeightText,
        string? RelocationWeightText,
        string? WireLengthText,
        string? AdditionalWtText,
        string? DeratingRatioText,
        string? SafetyFactorText,
        string? MaxCranePositionsText,
        string? EarlyStoppingGenerationsText,
        string? GridStepText,
        string? RadiusPenaltyWeightText,
        string? BalancePenaltyWeightText,
        string? OverweightPenaltyWeightText,
        string? HeightPenaltyWeightText,
        string? RadiusStdDevPenaltyWeightText,
        string? SimilarPositionPenaltyWeightText,
        string? RelocationDistancePenaltyWeightText,
        string? MinimumRelocationDistanceText,
        string? DuplicatePositionDistanceText,
        string? MutationExplorationMultiplierText,
        string? PositionSamplingAttemptsText,
        string? MaxRadiusForNormalisationText,
        string? PopulationSizeText,
        string? MaxGenerationsText,
        string? DevMutationRateText,
        string? DevMutationSigmaText,
        string? DevCrossoverRateText,
        string? DevRandomSeedText,
        string? DevConvergenceText);

    public sealed class CraneOptimizerPointListItem
    {
        public CraneOptimizerPointListItem(CranePositionResult point)
        {
            Point = point;
        }

        public CranePositionResult Point { get; }

        public string DisplayText =>
            $"Point {CraneOptimizerDialogWorkflow.FormatPointCoordinates(Point)} - liftable {Point.LiftableCount} elements (+{Point.NewLiftableCount} new)";
    }

    public sealed record CraneOptimizerPointSummary(
        string PositionText,
        int LiftableCount,
        int NonLiftableCount,
        int UnreachableCount,
        string CoverageText);
}
