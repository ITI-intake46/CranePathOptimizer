using Xunit;
using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Settings;
using System.Collections.Generic;

namespace CraneOptimizer.Application.Tests.GA
{
    public class GAParameterHelpRegistryTests
    {
        [Fact]
        public void TestRegistryContainsAllExpectedKeys()
        {
            var expectedKeys = new List<string>
            {
                // 1. Core Calibration
                "PopulationSize",
                "MaxGenerations",
                "MutationRate",
                "MutationSigma",
                "MaxCranePositions",
                "EarlyStoppingGenerations",
                "ConvergenceThresholdPercent",

                // 2. Fitness Strategy
                "CoverageWeight",
                "RelocationWeight",
                "RadiusPenaltyWeight",
                "RadiusQualityPenaltyWeight",
                "BalancePenaltyWeight",
                "RadiusStdDevPenaltyWeight",
                "SimilarPositionPenaltyWeight",
                "RelocationDistancePenaltyWeight",
                "OverweightPenaltyWeight",
                "HeightPenaltyWeight",

                // 3. Layout Constraints
                "MinimumRelocationDistance",
                "GridStep",
                "TargetElementTypes",

                // 4. Engineering Model
                "WireLength",
                "AdditionalWt",
                "DeratingRatio",
                "SafetyFactor",

                // 5. Advanced GA
                "RandomSeed",
                "CrossoverRate",
                "MutationExplorationMultiplier",
                "EnableAdaptiveSigma",
                "SigmaCoolingExponent",
                "MinimumMutationSigma",
                "PositionSamplingAttempts",
                "EnableConcurrentOptimization",

                // 6. Diagnostics
                "SlewOffsetX",
                "SlewOffsetY",
                "SlewOffsetZ",
                "BoomHingeOffsetX",
                "BoomHingeOffsetY",
                "BoomHingeOffsetZ",
                "MaxRadiusForNormalisation",
                "DuplicatePositionDistance",
                "CraneFootprintRadius",
                "EnableProfiling",
                "ProfilingLogDirectory",

                // 7. Multi-Stage Discovery Parameters
                "EnableMultiStageDiscovery",
                "MultiStageCoverageTarget",
                "AdaptiveRelocationScaleFactor",
                "UseHungarianAssignment"
            };

            foreach (var key in expectedKeys)
            {
                bool found = GAParameterHelpRegistry.TryGet(key, out var metadata);
                Assert.True(found, $"Key '{key}' was not found in GAParameterHelpRegistry.");
                Assert.NotNull(metadata);
                Assert.Equal(key, metadata.ParameterKey);
                Assert.False(string.IsNullOrWhiteSpace(metadata.DisplayName), $"Key '{key}' has empty DisplayName.");
                Assert.False(string.IsNullOrWhiteSpace(metadata.Description), $"Key '{key}' has empty Description.");
            }
        }

        [Fact]
        public void TestTryGetHandlesInvalidKeysGracefully()
        {
            Assert.False(GAParameterHelpRegistry.TryGet(null!, out _));
            Assert.False(GAParameterHelpRegistry.TryGet("", out _));
            Assert.False(GAParameterHelpRegistry.TryGet("   ", out _));
            Assert.False(GAParameterHelpRegistry.TryGet("NonExistentKey123", out _));
        }
    }
}
