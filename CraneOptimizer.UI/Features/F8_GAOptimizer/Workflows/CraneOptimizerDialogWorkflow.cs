using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Models;
using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Settings;
using CraneOptimizer.Domain.Entities;
using CraneOptimizer.Domain.Interfaces;
using CraneOptimizer.UI.Features.F8_GAOptimizer.DTOs;
using CraneOptimizer.UI.Features.F8_GAOptimizer.Services;
using System.Threading;

using CraneOptimizer.UI.Core.Configuration;
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
                r.Status == LiftStatus.SpecialCase ||
                r.Status == LiftStatus.NotCharted);
            int unreachable = point.ElementResults.Count(r => r.Status == LiftStatus.OutOfRadius);

            return new CraneOptimizerPointSummary(
                FormatPointCoordinates(point),
                liftable,
                nonLiftable,
                unreachable);
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
                // Issue 8 fix: RadiusQualityPenaltyWeight is separate from RadiusPenaltyWeight.
                // Default 1.0 keeps it lightweight relative to the out-of-radius count penalty.
                RadiusQualityPenaltyWeight = ParseDouble(options.RadiusQualityPenaltyWeightText, 1.0),
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
                MaxGenerations = ParseInt(options.MaxGenerationsText, 30),
                TargetElementTypes = options.TargetElementTypes,                EnableProfiling = AppFeatureFlags.EnableProjectPerformanceCapture,
                DiagnosticsLevel = AppFeatureFlags.EnableProjectPerformanceCapture
                    ? DiagnosticsLevel.Research
                    : DiagnosticsLevel.Off,


                // New dynamic and diagnostic options mapped to GASettings
                EnableAdaptiveSigma = ParseBool(options.EnableAdaptiveSigmaText, false),
                SigmaCoolingExponent = ParseDouble(options.SigmaCoolingExponentText, 1.5),
                MinimumMutationSigma = ParseDouble(options.MinimumMutationSigmaText, 0.5),
                EnableConcurrentOptimization = ParseBool(options.EnableConcurrentOptimizationText, true),
                SlewOffsetX = ParseDouble(options.SlewOffsetXText, 0.0),
                SlewOffsetY = ParseDouble(options.SlewOffsetYText, 0.0),
                SlewOffsetZ = ParseDouble(options.SlewOffsetZText, 0.0),
                BoomHingeOffsetX = ParseDouble(options.BoomHingeOffsetXText, 0.0),
                BoomHingeOffsetY = ParseDouble(options.BoomHingeOffsetYText, 0.0),
                BoomHingeOffsetZ = ParseDouble(options.BoomHingeOffsetZText, 0.0),
                CraneFootprintRadius = ParseDouble(options.CraneFootprintRadiusText, 0.0),
                ProfilingLogDirectory = options.ProfilingLogDirectoryText,
                // Phase 7: EnableMultiStageDiscovery is always true for normal UI optimization runs.
                // Developer/experiment paths can override GASettings directly after construction.
                EnableMultiStageDiscovery = true,
                MultiStageCoverageTarget = ParseDouble(options.MultiStageCoverageTargetText, 0.95),
                AdaptiveRelocationScaleFactor = ParseDouble(options.AdaptiveRelocationScaleFactorText, 0.40),
                UseHungarianAssignment = ParseBool(options.UseHungarianAssignmentText, false),
                LoadChartNumberOfLinesConstraint = options.LoadChartNumberOfLinesConstraint,
                LoadChartOperatingConditionConstraint = NullIfWhiteSpace(options.LoadChartOperatingConditionConstraint),
                EnableGroupedLifts = ParseBool(options.EnableGroupedLiftsText, true)
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
            return Math.Abs(point.Z) > 0.005
                ? $"({point.X:F0}, {point.Y:F0}, Z {point.Z:F2})"
                : $"({point.X:F0}, {point.Y:F0})";
        }

        private static int ParseInt(string? value, int fallback)
        {
            return int.TryParse(value, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var parsed) ? parsed : fallback;
        }

        private static double ParseDouble(string? value, double fallback)
        {
            return double.TryParse(value, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var parsed) ? parsed : fallback;
        }

        private static bool ParseBool(string? value, bool fallback)
        {
            return bool.TryParse(value, out var parsed) ? parsed : fallback;
        }

        private static int? ParseNullableInt(string? value, int? fallback)
        {
            if (string.IsNullOrWhiteSpace(value))
                return null;

            return int.TryParse(value, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var parsed) ? parsed : fallback;
        }

        private static string? NullIfWhiteSpace(string? value)
        {
            return string.IsNullOrWhiteSpace(value)
                ? null
                : value.Trim();
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
        string? RadiusQualityPenaltyWeightText,
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
        string? DevConvergenceText,
        IReadOnlyCollection<string> TargetElementTypes,
        // Added properties
        string? EnableAdaptiveSigmaText,
        string? SigmaCoolingExponentText,
        string? MinimumMutationSigmaText,
        string? EnableConcurrentOptimizationText,
        string? SlewOffsetXText,
        string? SlewOffsetYText,
        string? SlewOffsetZText,
        string? BoomHingeOffsetXText,
        string? BoomHingeOffsetYText,
        string? BoomHingeOffsetZText,
        string? CraneFootprintRadiusText,
        string? EnableProfilingText,
        string? ProfilingLogDirectoryText,
        string? EnableMultiStageDiscoveryText,
        string? MultiStageCoverageTargetText,
        string? AdaptiveRelocationScaleFactorText,
        string? UseHungarianAssignmentText,
        int? LoadChartNumberOfLinesConstraint,
        string? LoadChartOperatingConditionConstraint,
        /// <summary>
        /// GA Mode Preset to apply after building settings from UI text boxes.
        /// When <see cref="GAModePreset.None"/>, no preset is applied.
        /// </summary>
        GAModePreset SelectedPreset = GAModePreset.None,
        string? EnableGroupedLiftsText = null);

    public sealed class CraneOptimizerPointListItem
    {
        public CraneOptimizerPointListItem(CranePositionResult point)
        {
            Point = point;
        }

        public CranePositionResult Point { get; }

        public string DisplayText =>
            $"Point {CraneOptimizerDialogWorkflow.FormatPointCoordinates(Point)}" +
            $" — {Point.NewLiftableCount} lifted";
    }

    public sealed record CraneOptimizerPointSummary(
        string PositionText,
        int LiftableCount,
        int NonLiftableCount,
        int UnreachableCount);
}
