using Xunit;
using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Operators;
using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Engines.Constraints;
using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Interfaces;
using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Settings;
using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Models;
using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Engines.GeneticOptimization;
using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Engines.LiftingCalculation;
using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Engines.CraneConfiguration;
using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Engines.Coverage;
using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Engines.PlacementOrchestration;
using CraneOptimizer.Application.Optimization.GeneticAlgorithms.DTOs;
using CraneOptimizer.Domain.Common;
using CraneOptimizer.Domain.Schemas;
using CraneOptimizer.Domain.Entities;
using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Services;
using System;
using System.Collections.Generic;
using System.Reflection;

namespace CraneOptimizer.Application.Tests
{
    public class AdaptiveSigmaTests
    {
        private readonly IConstraintEngine _constraintEngine;
        private readonly MutationOperator _mutationOperator;

        public AdaptiveSigmaTests()
        {
            _constraintEngine = new ConstraintEngine();
            _mutationOperator = new MutationOperator(_constraintEngine);
        }

        private double InvokeResolveMutationSigma(GASettings settings, int currentGeneration)
        {
            var method = typeof(MutationOperator).GetMethod("ResolveMutationSigma", 
                BindingFlags.NonPublic | BindingFlags.Instance);
            
            if (method == null)
            {
                throw new InvalidOperationException("Could not find ResolveMutationSigma on MutationOperator");
            }

            return (double)method.Invoke(_mutationOperator, new object[] { settings, currentGeneration });
        }

        [Fact]
        public void TestSigmaDecay()
        {
            var settings = new GASettings
            {
                MutationSigma = 10.0, // Absolute mode: 10m base * 2.0 multiplier = 20.0m Base
                MutationExplorationMultiplier = 2.0,
                EnableAdaptiveSigma = true,
                MaxGenerations = 100,
                SigmaCoolingExponent = 1.5,
                MinimumMutationSigma = 0.5
            };

            // Generation 0: (1 - 0/100)^1.5 * 20 + 0.5 = 20.5
            double sigmaGen0 = InvokeResolveMutationSigma(settings, 0);
            Assert.Equal(20.5, sigmaGen0, 5);

            // Generation 50: (1 - 50/100)^1.5 * 20 + 0.5 = 0.5^1.5 * 20 + 0.5 = 0.35355 * 20 + 0.5 = 7.071 + 0.5 = 7.571
            double sigmaGen50 = InvokeResolveMutationSigma(settings, 50);
            Assert.Equal(7.5710678, sigmaGen50, 5);

            // Generation 100: (1 - 100/100)^1.5 * 20 + 0.5 = 0.5
            double sigmaGen100 = InvokeResolveMutationSigma(settings, 100);
            Assert.Equal(0.5, sigmaGen100, 5);

            // Verify decay is monotonic
            double prevSigma = sigmaGen0;
            for (int gen = 1; gen <= 100; gen++)
            {
                double currentSigma = InvokeResolveMutationSigma(settings, gen);
                Assert.True(currentSigma < prevSigma, $"Sigma did not decrease at generation {gen}: {currentSigma} vs {prevSigma}");
                prevSigma = currentSigma;
            }
        }

        [Fact]
        public void TestBackwardCompatibility()
        {
            var settings = new GASettings
            {
                MutationSigma = 10.0, // Absolute mode: 10m base * 2.0 multiplier = 20.0m Base
                MutationExplorationMultiplier = 2.0,
                EnableAdaptiveSigma = false, // DISABLED
                MaxGenerations = 100,
                SigmaCoolingExponent = 1.5,
                MinimumMutationSigma = 0.5
            };

            double sigmaGen0 = InvokeResolveMutationSigma(settings, 0);
            double sigmaGen50 = InvokeResolveMutationSigma(settings, 50);
            double sigmaGen100 = InvokeResolveMutationSigma(settings, 100);

            // All must be exactly the base (20.0) since adaptive is disabled
            Assert.Equal(20.0, sigmaGen0);
            Assert.Equal(20.0, sigmaGen50);
            Assert.Equal(20.0, sigmaGen100);
        }

        [Fact]
        public void TestDeterminismWithAdaptiveSigma()
        {
            var configEngine = new CraneConfigurationEngine();
            var liftingEngine = new LiftingCalculationEngine(configEngine);
            var constraintEngine = new ConstraintEngine();
            var coverageEngine = new CoverageEngine();

            var initializer = new GAInitializer(constraintEngine);
            var assignmentService = new ElementAssignmentService(liftingEngine, configEngine);
            var fitnessEvaluator = new FitnessEvaluator(constraintEngine);
            var selection = new TournamentSelection();
            var crossover = new CrossoverOperator();
            var mutation = new MutationOperator(constraintEngine);
            var replacement = new ReplacementStrategy();

            var gaEngine = new GAEngine(
                initializer,
                assignmentService,
                fitnessEvaluator,
                selection,
                crossover,
                mutation,
                replacement,
                constraintEngine
            );

            var diagnosticsService = new GACsvDiagnosticsService(liftingEngine, configEngine);
            var placementEngine = new CranePlacementEngine(
                gaEngine,
                assignmentService,
                coverageEngine,
                configEngine,
                diagnosticsService
            );

            var settings = new GASettings
            {
                WireLength = 3.0,
                DeratingRatio = 0.85,
                SafetyFactor = 0.90,
                PopulationSize = 10,
                MaxGenerations = 10,
                MaxCranePositions = 3,
                RandomSeed = 12345, // Fixed seed
                CraneFootprintRadius = 2.0,
                MinimumRelocationDistance = 5.0,
                MaxRadiusForNormalisation = 40.0,
                ConvergenceThresholdPercent = 0.05,
                EarlyStoppingGenerations = 5,
                PositionSamplingAttempts = 50,
                GridStep = 0.5,
                CoverageWeight = 100.0,
                RelocationWeight = 20.0,
                RadiusPenaltyWeight = 30.0,
                OverweightPenaltyWeight = 30.0,
                HeightPenaltyWeight = 30.0,
                RadiusQualityPenaltyWeight = 10.0,
                SimilarPositionPenaltyWeight = 10.0,
                RelocationDistancePenaltyWeight = 15.0,
                EnableProfiling = false,
                EnableAdaptiveSigma = true, // Enabled
                SigmaCoolingExponent = 1.5,
                MinimumMutationSigma = 0.5
            };

            var craneSpec = new CraneSpecification
            {
                Id = 1,
                MaxRadius = 40,
                MaxBoomLength = 50,
                LoadChart = new List<TargetPoint>
                {
                    new TargetPoint { Radius = 10, BoomLength = 20, Capacity = 50 },
                    new TargetPoint { Radius = 20, BoomLength = 30, Capacity = 30 }
                }
            };

            var allowedZone = new GreenAreaZone
            {
                Vertices = new List<DomainPoint3D>
                {
                    new DomainPoint3D(-50, -50, 0),
                    new DomainPoint3D(50, -50, 0),
                    new DomainPoint3D(50, 50, 0),
                    new DomainPoint3D(-50, 50, 0)
                }
            };

            var elements = new List<PrecastElement>
            {
                new PrecastElement { ElementID = "EL-1", ElementCOG = new DomainPoint3D(10, 10, 1), ElementHeight = 4.0, ElementWT = 12.0 },
                new PrecastElement { ElementID = "EL-2", ElementCOG = new DomainPoint3D(20, -5, 2), ElementHeight = 3.5, ElementWT = 15.5 },
                new PrecastElement { ElementID = "EL-3", ElementCOG = new DomainPoint3D(-15, 25, 3), ElementHeight = 5.0, ElementWT = 8.0 }
            };

            var input1 = new PlacementInput
            {
                Zones = new List<GreenAreaZone> { allowedZone },
                ForbiddenZones = new List<GreenAreaZone>(),
                Elements = elements,
                Assignment = new Dictionary<int, List<string>>(),
                CraneSpec = craneSpec,
                Settings = settings,
                TotalElementsCount = elements.Count
            };

            var input2 = new PlacementInput
            {
                Zones = new List<GreenAreaZone> { allowedZone },
                ForbiddenZones = new List<GreenAreaZone>(),
                Elements = elements,
                Assignment = new Dictionary<int, List<string>>(),
                CraneSpec = craneSpec,
                Settings = settings,
                TotalElementsCount = elements.Count
            };

            var result1 = placementEngine.RunOptimization(input1);
            var result2 = placementEngine.RunOptimization(input2);

            Assert.Equal(result1.FitnessScore, result2.FitnessScore, 7);
            Assert.Equal(result1.Positions.Count, result2.Positions.Count);
            for (int i = 0; i < result1.Positions.Count; i++)
            {
                Assert.Equal(result1.Positions[i].X, result2.Positions[i].X, 7);
                Assert.Equal(result1.Positions[i].Y, result2.Positions[i].Y, 7);
            }
        }
    }
}
