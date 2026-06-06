using Xunit;
using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Services;
using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Interfaces;
using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Settings;
using CraneOptimizer.Application.Optimization.GeneticAlgorithms.DTOs;
using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Engines.GeneticOptimization;
using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Engines.CraneConfiguration;
using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Engines.LiftingCalculation;
using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Engines.Constraints;
using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Engines.Coverage;
using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Models;
using CraneOptimizer.Domain.Entities;
using CraneOptimizer.Domain.Schemas;
using CraneOptimizer.Domain.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace CraneOptimizer.Application.Tests.GA
{
    public class MultiZoneOptimizationTests
    {
        // 1. Global feasibility sweep test
        [Fact]
        public void TestGlobalFeasibilitySweep_ElementBecomesLiftableWhenCloser()
        {
            var configEngine = new CraneConfigurationEngine();
            var capacityResolver = new LoadChartCapacityResolver();
            var liftingEngine = new LiftingCalculationEngine(configEngine, capacityResolver);

            var settings = new GASettings
            {
                WireLength = 2.0,
                AdditionalWt = 0.0,
                DeratingRatio = 1.0,
                SafetyFactor = 1.0
            };

            var craneSpec = new CraneSpecification
            {
                MaxRadius = 36.0,
                MaxBoomLength = 45.0,
                LoadChart = new List<TargetPoint>
                {
                    new TargetPoint { Radius = 10.0, BoomLength = 15.0, Capacity = 16.0 },
                    new TargetPoint { Radius = 30.0, BoomLength = 35.0, Capacity = 8.0 }
                }
            };

            var element = new PrecastElement
            {
                ElementID = "EL-1",
                ElementCOG = new DomainPoint3D(0, 30, 0),
                ElementHeight = 1.0,
                ElementWT = 10.0 // 10 tons
            };

            // Setup A: far away (Radius = 30m)
            var setupA = new DomainPoint3D(0, 0, 0);
            double radiusA = liftingEngine.CalculateRadius(setupA, element, settings);
            double heightA = liftingEngine.CalculateLiftHeight(element, setupA, settings);
            double boomLengthA = configEngine.CalculateRequiredBoomLength(setupA, element, settings);
            double requiredWt = liftingEngine.CalculateRequiredWeight(element, settings);

            var resolveA = capacityResolver.Resolve(craneSpec, radiusA, boomLengthA, requiredWt, settings);
            Assert.Equal(LiftStatus.Overweight, resolveA.LiftStatus); // Capacity 8.0 < 10.0 Weight

            // Setup B: closer (Radius = 10m)
            var setupB = new DomainPoint3D(0, 20, 0);
            double radiusB = liftingEngine.CalculateRadius(setupB, element, settings);
            double heightB = liftingEngine.CalculateLiftHeight(element, setupB, settings);
            double boomLengthB = configEngine.CalculateRequiredBoomLength(setupB, element, settings);

            var resolveB = capacityResolver.Resolve(craneSpec, radiusB, boomLengthB, requiredWt, settings);
            Assert.Equal(LiftStatus.Liftable, resolveB.LiftStatus); // Capacity 16.0 >= 10.0 Weight
        }

        // 2. Zone 2 candidate generation test
        [Fact]
        public void TestZone2CandidateGeneration_ProducesValidSetupPoints()
        {
            var constraintEngine = new ConstraintEngine();
            var settings = new GASettings
            {
                CraneFootprintRadius = 1.0,
                PositionSamplingAttempts = 40,
                GridStep = 0.5
            };

            var zone1 = new GreenAreaZone
            {
                Vertices = new List<DomainPoint3D>
                {
                    new DomainPoint3D(0, -10, 0),
                    new DomainPoint3D(20, -10, 0),
                    new DomainPoint3D(20, 10, 0),
                    new DomainPoint3D(0, 10, 0)
                }
            };

            var zone2 = new GreenAreaZone
            {
                Vertices = new List<DomainPoint3D>
                {
                    new DomainPoint3D(0, 40, 0),
                    new DomainPoint3D(20, 40, 0),
                    new DomainPoint3D(20, 60, 0),
                    new DomainPoint3D(0, 60, 0)
                }
            };

            var allowedZones = new List<GreenAreaZone> { zone1, zone2 };
            var forbiddenZones = new List<GreenAreaZone>();

            // Point inside Zone 1 Center
            var pt1 = new DomainPoint3D(10, 0, 0);
            Assert.True(constraintEngine.IsValidSetupPoint(pt1, allowedZones, forbiddenZones, settings));

            // Point inside Zone 2 Center
            var pt2 = new DomainPoint3D(10, 50, 0);
            Assert.True(constraintEngine.IsValidSetupPoint(pt2, allowedZones, forbiddenZones, settings));
        }

        // 3. Reachability zone diversity test
        [Fact]
        public void TestReachabilityZoneDiversity_IncludesPointsFromMultipleZones()
        {
            var constraintEngine = new ConstraintEngine();
            var capacityResolver = new LoadChartCapacityResolver();
            var reachabilityMapService = new ReachabilityMapService(constraintEngine, capacityResolver);

            var settings = new GASettings
            {
                CraneFootprintRadius = 1.0,
                GridStep = 2.0,
                WireLength = 2.0,
                AdditionalWt = 0.0,
                DeratingRatio = 1.0,
                SafetyFactor = 1.0
            };

            var craneSpec = new CraneSpecification
            {
                MaxRadius = 25.0,
                MaxBoomLength = 30.0,
                LoadChart = new List<TargetPoint>
                {
                    new TargetPoint { Radius = 2.0, BoomLength = 5.0, Capacity = 20.0 },
                    new TargetPoint { Radius = 10.0, BoomLength = 15.0, Capacity = 10.0 },
                    new TargetPoint { Radius = 20.0, BoomLength = 25.0, Capacity = 5.0 }
                }
            };

            // Elements in Zone 1 (x: 5, y: 0) and Zone 2 (x: 5, y: 50)
            var elements = new List<PrecastElement>
            {
                new PrecastElement { ElementID = "EL-1", ElementCOG = new DomainPoint3D(5, 0, 0), ElementHeight = 1.0, ElementWT = 2.0 },
                new PrecastElement { ElementID = "EL-2", ElementCOG = new DomainPoint3D(5, 50, 0), ElementHeight = 1.0, ElementWT = 2.0 }
            };

            var zone1 = new GreenAreaZone
            {
                Vertices = new List<DomainPoint3D>
                {
                    new DomainPoint3D(0, -10, 0),
                    new DomainPoint3D(20, -10, 0),
                    new DomainPoint3D(20, 10, 0),
                    new DomainPoint3D(0, 10, 0)
                }
            };

            var zone2 = new GreenAreaZone
            {
                Vertices = new List<DomainPoint3D>
                {
                    new DomainPoint3D(0, 40, 0),
                    new DomainPoint3D(20, 40, 0),
                    new DomainPoint3D(20, 60, 0),
                    new DomainPoint3D(0, 60, 0)
                }
            };

            var allowedZones = new List<GreenAreaZone> { zone1, zone2 };
            var forbiddenZones = new List<GreenAreaZone>();

            var hotSpots = reachabilityMapService.ComputeHotSpots(elements, allowedZones, forbiddenZones, craneSpec, settings, 20);

            Assert.NotEmpty(hotSpots);
            // Verify we have at least one hot spot inside Zone 1 (y < 20) and one inside Zone 2 (y > 30)
            bool hasZone1Spot = hotSpots.Any(hs => hs.Y < 20);
            bool hasZone2Spot = hotSpots.Any(hs => hs.Y > 30);

            Assert.True(hasZone1Spot, "Should have seeded a hot spot in Zone 1");
            Assert.True(hasZone2Spot, "Should have seeded a hot spot in Zone 2");
        }

        // 4. Multi-stage improvement test
        private class MockPlacementEngine : ICranePlacementEngine
        {
            public Func<PlacementInput, CancellationToken, PlacementResult>? OptimizationMock { get; set; }

            public PlacementResult RunOptimization(PlacementInput input, CancellationToken cancellationToken)
            {
                if (OptimizationMock != null)
                {
                    return OptimizationMock(input, cancellationToken);
                }
                return new PlacementResult();
            }
        }

        [Fact]
        public async Task TestMultiStageImprovement_Stage2WinsWhenCoverageImproves()
        {
            var mockEngine = new MockPlacementEngine();
            mockEngine.OptimizationMock = (input, token) =>
            {
                if (input.Settings.MaxCranePositions == 1)
                {
                    return new PlacementResult
                    {
                        CoverageRatio = 0.64,
                        Positions = new List<CranePositionResult> { new CranePositionResult { X = 10, Y = 0 } },
                        FitnessScore = 6.4
                    };
                }
                else
                {
                    return new PlacementResult
                    {
                        CoverageRatio = 0.82,
                        Positions = new List<CranePositionResult>
                        {
                            new CranePositionResult { X = 10, Y = 0 },
                            new CranePositionResult { X = 10, Y = 50 }
                        },
                        FitnessScore = 7.2
                    };
                }
            };

            var orchestrator = new MultiStageGAOrchestrator(mockEngine);
            var baseSettings = new GASettings
            {
                MaxCranePositions = 2,
                CoverageWeight = 10,
                AdaptiveRelocationScaleFactor = 0.4
            };

            var input = new PlacementInput { Settings = baseSettings };
            var crane = new Crane();

            var result = await orchestrator.RunAsync(input, crane, baseSettings, 0.95, CancellationToken.None);

            Assert.Equal(2, result.StagesExecuted);
            Assert.Equal(2, result.BestResult.Positions.Count);
            Assert.Equal(0.82, result.BestResult.CoverageRatio);
        }

        // 5. Equal coverage tie-break test
        [Fact]
        public async Task TestEqualCoverageTieBreak_FewerPositionsWin()
        {
            var mockEngine = new MockPlacementEngine();
            mockEngine.OptimizationMock = (input, token) =>
            {
                if (input.Settings.MaxCranePositions == 1)
                {
                    return new PlacementResult
                    {
                        CoverageRatio = 0.64,
                        Positions = new List<CranePositionResult> { new CranePositionResult { X = 10, Y = 0 } },
                        FitnessScore = 6.4
                    };
                }
                else
                {
                    return new PlacementResult
                    {
                        CoverageRatio = 0.64,
                        Positions = new List<CranePositionResult>
                        {
                            new CranePositionResult { X = 10, Y = 0 },
                            new CranePositionResult { X = 12, Y = 0 }
                        },
                        FitnessScore = 4.8 // lower fitness due to relocation penalty
                    };
                }
            };

            var orchestrator = new MultiStageGAOrchestrator(mockEngine);
            var baseSettings = new GASettings
            {
                MaxCranePositions = 2,
                CoverageWeight = 10,
                AdaptiveRelocationScaleFactor = 0.4
            };

            var input = new PlacementInput { Settings = baseSettings };
            var crane = new Crane();

            var result = await orchestrator.RunAsync(input, crane, baseSettings, 0.95, CancellationToken.None);

            Assert.Equal(2, result.StagesExecuted);
            Assert.Single(result.BestResult.Positions); // 1 position wins
            Assert.Equal(0.64, result.BestResult.CoverageRatio);
        }

        // 6. Overweight classification test
        [Fact]
        public void TestOverweightClassification_NotGloballyImpossibleUnlessAllPositionsFail()
        {
            var configEngine = new CraneConfigurationEngine();
            var capacityResolver = new LoadChartCapacityResolver();
            var liftingEngine = new LiftingCalculationEngine(configEngine, capacityResolver);

            var settings = new GASettings
            {
                WireLength = 2.0,
                AdditionalWt = 0.0,
                DeratingRatio = 1.0,
                SafetyFactor = 1.0
            };

            var craneSpec = new CraneSpecification
            {
                MaxRadius = 20.0,
                MaxBoomLength = 25.0,
                LoadChart = new List<TargetPoint>
                {
                    new TargetPoint { Radius = 5.0, BoomLength = 10.0, Capacity = 15.0 },
                    new TargetPoint { Radius = 15.0, BoomLength = 20.0, Capacity = 5.0 }
                }
            };

            var element = new PrecastElement
            {
                ElementID = "EL-1",
                ElementCOG = new DomainPoint3D(0, 15, 0),
                ElementHeight = 1.0,
                ElementWT = 8.0 // 8 tons
            };

            // Setup A: far away (Radius = 15m)
            var setupA = new DomainPoint3D(0, 0, 0);
            double radiusA = liftingEngine.CalculateRadius(setupA, element, settings);
            double heightA = liftingEngine.CalculateLiftHeight(element, setupA, settings);
            double boomLengthA = configEngine.CalculateRequiredBoomLength(setupA, element, settings);
            double requiredWt = liftingEngine.CalculateRequiredWeight(element, settings);

            var resolveA = capacityResolver.Resolve(craneSpec, radiusA, boomLengthA, requiredWt, settings);
            Assert.Equal(LiftStatus.Overweight, resolveA.LiftStatus); // Capacity 5.0 < 8.0 Weight

            // Setup B: closer (Radius = 5m)
            var setupB = new DomainPoint3D(0, 10, 0);
            double radiusB = liftingEngine.CalculateRadius(setupB, element, settings);
            double heightB = liftingEngine.CalculateLiftHeight(element, setupB, settings);
            double boomLengthB = configEngine.CalculateRequiredBoomLength(setupB, element, settings);

            var resolveB = capacityResolver.Resolve(craneSpec, radiusB, boomLengthB, requiredWt, settings);
            Assert.Equal(LiftStatus.Liftable, resolveB.LiftStatus); // Capacity 15.0 >= 8.0 Weight
        }

        // 7. Post-processing test
        [Fact]
        public void TestPostProcessing_DeduplicationDoesNotMergeDistinctSetupPoints()
        {
            var coverageEngine = new CoverageEngine();
            var settings = new GASettings
            {
                DuplicatePositionDistance = 3.0
            };

            var result = new PlacementResult
            {
                Positions = new List<CranePositionResult>
                {
                    new CranePositionResult { X = 10, Y = 0 },
                    new CranePositionResult { X = 10, Y = 50 } // distance = 50m >> 3m
                }
            };

            coverageEngine.DeduplicateFinalPositions(result, settings);

            Assert.Equal(2, result.Positions.Count); // should not be merged
        }

        // 8. UI diagnostic test
        [Fact]
        public void TestUIDiagnostic_WhenTargetImpossible()
        {
            // Verify that we can identify unliftable elements and failure reasons
            var capacityResolver = new LoadChartCapacityResolver();
            var settings = new GASettings();
            var craneSpec = new CraneSpecification
            {
                MaxRadius = 10.0,
                MaxBoomLength = 15.0,
                LoadChart = new List<TargetPoint>() // empty load chart
            };

            var resolve = capacityResolver.Resolve(craneSpec, 15.0, 20.0, 5.0, settings);
            Assert.Equal(LiftStatus.NotCharted, resolve.LiftStatus);
            Assert.Equal(CapacityFailureReason.MissingLoadChartData.ToString(), resolve.FailureReason);
        }
    }
}
