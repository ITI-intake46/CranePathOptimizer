using System.Collections.Generic;
using Xunit;
using CraneOptimizer.Domain.Entities;
using CraneOptimizer.UI.Features.F8_GAOptimizer.Workflows;
using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Settings;

namespace CraneOptimizer.Application.Tests.GA
{
    public class CraneOptimizerDialogWorkflowTransparencyTests
    {
        private readonly CraneOptimizerDialogWorkflow _workflow = new(null!, null!);

        private static CraneOptimizerDialogOptions CreateOptions(
            string? enableMultiStageDiscoveryText = null,
            string? deratingRatioText = null,
            string? safetyFactorText = null,
            string? maxCranePositionsText = null)
        {
            return new CraneOptimizerDialogOptions(
                CoverageWeightText: "10.0",
                RelocationWeightText: "5.0",
                WireLengthText: "2.0",
                AdditionalWtText: "0.0",
                DeratingRatioText: deratingRatioText ?? "0.9",
                SafetyFactorText: safetyFactorText ?? "0.95",
                MaxCranePositionsText: maxCranePositionsText ?? "1",
                EarlyStoppingGenerationsText: "20",
                GridStepText: "0.0",
                RadiusPenaltyWeightText: "3.0",
                RadiusQualityPenaltyWeightText: "1.0",
                BalancePenaltyWeightText: "2.0",
                OverweightPenaltyWeightText: "10.0",
                HeightPenaltyWeightText: "5.0",
                RadiusStdDevPenaltyWeightText: "2.0",
                SimilarPositionPenaltyWeightText: "8.0",
                RelocationDistancePenaltyWeightText: "1.5",
                MinimumRelocationDistanceText: "10.0",
                DuplicatePositionDistanceText: "3.0",
                MutationExplorationMultiplierText: "2.0",
                PositionSamplingAttemptsText: "80",
                MaxRadiusForNormalisationText: "50.0",
                PopulationSizeText: "30",
                MaxGenerationsText: "30",
                DevMutationRateText: "0.1",
                DevMutationSigmaText: "0.05",
                DevCrossoverRateText: "0.8",
                DevRandomSeedText: "42",
                DevConvergenceText: "0.0",
                TargetElementTypes: new List<string>(),
                EnableAdaptiveSigmaText: "false",
                SigmaCoolingExponentText: "1.5",
                MinimumMutationSigmaText: "0.5",
                EnableConcurrentOptimizationText: "true",
                SlewOffsetXText: "0.0",
                SlewOffsetYText: "0.0",
                SlewOffsetZText: "0.0",
                BoomHingeOffsetXText: "0.0",
                BoomHingeOffsetYText: "0.0",
                BoomHingeOffsetZText: "0.0",
                CraneFootprintRadiusText: "0.0",
                EnableProfilingText: "true",
                ProfilingLogDirectoryText: "",
                EnableMultiStageDiscoveryText: enableMultiStageDiscoveryText ?? "true",
                MultiStageCoverageTargetText: "0.95",
                AdaptiveRelocationScaleFactorText: "0.40",
                UseHungarianAssignmentText: "false",
                LoadChartNumberOfLinesConstraint: null,
                LoadChartOperatingConditionConstraint: null,
                SelectedPreset: GAModePreset.None
            );
        }

        [Fact]
        public void BuildSettings_AlwaysSetsEnableMultiStageDiscovery_True_WhenFalseInOptions()
        {
            // Arrange
            var options = CreateOptions(enableMultiStageDiscoveryText: "false");

            // Act
            var settings = _workflow.BuildSettings(options);

            // Assert
            Assert.True(settings.EnableMultiStageDiscovery);
        }

        [Fact]
        public void BuildSettings_AlwaysSetsEnableMultiStageDiscovery_True_WhenTrueInOptions()
        {
            // Arrange
            var options = CreateOptions(enableMultiStageDiscoveryText: "true");

            // Act
            var settings = _workflow.BuildSettings(options);

            // Assert
            Assert.True(settings.EnableMultiStageDiscovery);
        }

        [Fact]
        public void BuildSettings_DeratingRatio_Clamped_WhenAbove1()
        {
            // Arrange
            var options = CreateOptions(deratingRatioText: "1.2");

            // Act
            var settings = _workflow.BuildSettings(options);

            // Assert
            Assert.Equal(1.0, settings.DeratingRatio);
        }

        [Fact]
        public void BuildSettings_DeratingRatio_Clamped_WhenBelow0()
        {
            // Arrange
            var options = CreateOptions(deratingRatioText: "-0.5");

            // Act
            var settings = _workflow.BuildSettings(options);

            // Assert
            Assert.Equal(0.0, settings.DeratingRatio);
        }

        [Fact]
        public void BuildSettings_SafetyFactor_Clamped_WhenAbove1()
        {
            // Arrange
            var options = CreateOptions(safetyFactorText: "1.05");

            // Act
            var settings = _workflow.BuildSettings(options);

            // Assert
            Assert.Equal(1.0, settings.SafetyFactor);
        }

        [Fact]
        public void BuildSettings_SafetyFactor_Clamped_WhenBelow0()
        {
            // Arrange
            var options = CreateOptions(safetyFactorText: "-0.1");

            // Act
            var settings = _workflow.BuildSettings(options);

            // Assert
            Assert.Equal(0.0, settings.SafetyFactor);
        }

        [Fact]
        public void BuildSettings_MaxCranePositions_Parsed_AsUserValue()
        {
            // Arrange
            var options = CreateOptions(maxCranePositionsText: "6");

            // Act
            var settings = _workflow.BuildSettings(options);

            // Assert
            Assert.Equal(6, settings.MaxCranePositions);
        }

        [Fact]
        public void BuildSettings_RetiredFields_FallBackToDefaults_WhenNullInOptions()
        {
            // Arrange
            var options = new CraneOptimizerDialogOptions(
                CoverageWeightText: "10.0",
                RelocationWeightText: "5.0",
                WireLengthText: "2.0",
                AdditionalWtText: "0.0",
                DeratingRatioText: "0.9",
                SafetyFactorText: "0.95",
                MaxCranePositionsText: "3",
                EarlyStoppingGenerationsText: "20",
                GridStepText: "0.0",
                RadiusPenaltyWeightText: "3.0",
                RadiusQualityPenaltyWeightText: "1.0",
                BalancePenaltyWeightText: "2.0",
                OverweightPenaltyWeightText: "10.0",
                HeightPenaltyWeightText: "5.0",
                RadiusStdDevPenaltyWeightText: "2.0",
                SimilarPositionPenaltyWeightText: "8.0",
                RelocationDistancePenaltyWeightText: "1.5",
                MinimumRelocationDistanceText: "10.0",
                DuplicatePositionDistanceText: null, // Retired
                MutationExplorationMultiplierText: "2.0",
                PositionSamplingAttemptsText: "80",
                MaxRadiusForNormalisationText: null, // Retired
                PopulationSizeText: "30",
                MaxGenerationsText: "30",
                DevMutationRateText: "0.1",
                DevMutationSigmaText: "0.05",
                DevCrossoverRateText: "0.8",
                DevRandomSeedText: "42",
                DevConvergenceText: "0.0",
                TargetElementTypes: new List<string>(),
                EnableAdaptiveSigmaText: "false",
                SigmaCoolingExponentText: "1.5",
                MinimumMutationSigmaText: "0.5",
                EnableConcurrentOptimizationText: "true",
                SlewOffsetXText: null, // Retired
                SlewOffsetYText: null, // Retired
                SlewOffsetZText: null, // Retired
                BoomHingeOffsetXText: null, // Retired
                BoomHingeOffsetYText: null, // Retired
                BoomHingeOffsetZText: null, // Retired
                CraneFootprintRadiusText: null, // Retired
                EnableProfilingText: "true",
                ProfilingLogDirectoryText: null, // Retired
                EnableMultiStageDiscoveryText: "true",
                MultiStageCoverageTargetText: "0.95",
                AdaptiveRelocationScaleFactorText: "0.40",
                UseHungarianAssignmentText: "false",
                LoadChartNumberOfLinesConstraint: null,
                LoadChartOperatingConditionConstraint: null,
                SelectedPreset: GAModePreset.None
            );

            // Act
            var settings = _workflow.BuildSettings(options);

            // Assert
            Assert.Equal(0.0, settings.SlewOffsetX);
            Assert.Equal(0.0, settings.SlewOffsetY);
            Assert.Equal(0.0, settings.SlewOffsetZ);
            Assert.Equal(0.0, settings.BoomHingeOffsetX);
            Assert.Equal(0.0, settings.BoomHingeOffsetY);
            Assert.Equal(0.0, settings.BoomHingeOffsetZ);
            Assert.Equal(0.0, settings.CraneFootprintRadius);
            Assert.Equal(3.0, settings.DuplicatePositionDistance);
            Assert.Equal(50.0, settings.MaxRadiusForNormalisation);
            Assert.Null(settings.ProfilingLogDirectory);
        }
    }
}
