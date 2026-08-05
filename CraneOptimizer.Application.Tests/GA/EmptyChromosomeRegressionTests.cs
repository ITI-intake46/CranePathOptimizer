using Xunit;
using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Engines.GeneticOptimization;
using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Engines.LiftingCalculation;
using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Engines.CraneConfiguration;
using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Engines.Constraints;
using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Models;
using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Settings;
using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Services;
using CraneOptimizer.Application.Optimization.Utilities;
using CraneOptimizer.Domain.Common;
using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Interfaces;
using CraneOptimizer.Application.Optimization.GeneticAlgorithms.DTOs;
using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Engines.PlacementOrchestration;
using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Engines.Coverage;
using CraneOptimizer.Domain.Entities;
using CraneOptimizer.Domain.Schemas;
using System.Collections.Generic;

namespace CraneOptimizer.Application.Tests
{
    public class EmptyChromosomeRegressionTests
    {
        [Fact]
        public void TestEmptyChromosomeEvaluation_NoExceptionAndCorrectDefaults()
        {
            // Reset GAProfiler & enable it
            GAProfiler.Reset();
            GAProfiler.Enabled = true;

            var configEngine = new CraneConfigurationEngine();
            var liftingEngine = new LiftingCalculationEngine(configEngine);
            var assignmentService = new ElementAssignmentService(liftingEngine, configEngine);

            var settings = new GASettings();
            var craneSpec = new CraneSpecification();
            var elements = new List<PrecastElement>
            {
                new PrecastElement
                {
                    ElementID = "EL-REG-1",
                    ElementCOG = new DomainPoint3D(10, 10, 2),
                    ElementWT = 5.0
                }
            };

            // Crane solution has 0 positions
            var solution = new CraneSolution
            {
                Chromosome = new CraneChromosome
                {
                    CranePositions = new List<DomainPoint3D>()
                }
            };

            // 1. Evaluate assignment (slow path)
            var slowResult = assignmentService.EvaluateAssignment(
                solution,
                elements,
                new Dictionary<int, List<string>>(),
                craneSpec,
                settings);

            Assert.NotNull(slowResult);
            Assert.Single(slowResult.EvaluationResults);
            
            var slowEval = slowResult.EvaluationResults[0];
            Assert.NotNull(slowEval);
            Assert.Equal("EL-REG-1", slowEval.ElementId);
            Assert.Equal(-1, slowEval.AssignedCraneIndex);
            Assert.Equal(LiftStatus.OutOfRadius, slowEval.Status);
            Assert.Equal(ReachStatus.OutOfRadius, slowEval.ReachStatus);
            Assert.Equal(ChartStatus.OutsideChartEnvelope, slowEval.ChartStatus);
            Assert.Equal(0, slowEval.AvailableCapacity);
            Assert.Equal(double.MinValue, slowEval.SafetyMargin);
            Assert.Empty(slowResult.AssignedElements);
            Assert.Empty(slowResult.AssignedLiftableElements);

            // 2. Evaluate assignment (fast path)
            var index = new LoadChartIndex(craneSpec);
            var elementCache = new ElementStaticCache(elements, settings);
            var fastResult = assignmentService.EvaluateAssignment(
                solution,
                elements,
                new Dictionary<int, List<string>>(),
                craneSpec,
                settings,
                index,
                elementCache);

            Assert.NotNull(fastResult);
            Assert.Single(fastResult.EvaluationResults);

            var fastEval = fastResult.EvaluationResults[0];
            Assert.NotNull(fastEval);
            Assert.Equal("EL-REG-1", fastEval.ElementId);
            Assert.Equal(-1, fastEval.AssignedCraneIndex);
            Assert.Equal(LiftStatus.OutOfRadius, fastEval.Status);
            Assert.Equal(ReachStatus.OutOfRadius, fastEval.ReachStatus);
            Assert.Equal(ChartStatus.OutsideChartEnvelope, fastEval.ChartStatus);
            Assert.Equal(0, fastEval.AvailableCapacity);
            Assert.Equal(double.MinValue, fastEval.SafetyMargin);
            Assert.Empty(fastResult.AssignedElements);
            Assert.Empty(fastResult.AssignedLiftableElements);

            // 3. Compute Fitness (should be 0)
            var constraintEngine = new ConstraintEngine();
            var fitnessEvaluator = new FitnessEvaluator(constraintEngine);
            var fitnessBreakdown = fitnessEvaluator.EvaluateFitness(solution, fastResult, settings);

            Assert.Equal(0, fitnessBreakdown.FitnessScore);
            Assert.Equal(0, fitnessBreakdown.CoverageRatio);

            // 4. Verify Diagnostics counter incremented (called once for slow, once for fast path)
            Assert.Equal(2, GAProfiler.EmptyChromosomeCount);
        }

        private class StubGAEngine : IGeneticAlgorithmEngine
        {
            private readonly CraneSolution _solution;
            public StubGAEngine(CraneSolution solution) => _solution = solution;
            public CraneSolution Run(PlacementInput input, System.Threading.CancellationToken cancellationToken = default) => _solution;
        }

        private class StubDiagnosticsService : IGACsvDiagnosticsService
        {
            public string GenerateDiagnostics(PlacementInput input, PlacementResult result) => string.Empty;
        }

        [Fact]
        public void TestZeroLiftingSetupPositionsFilteredOut()
        {
            var solution = new CraneSolution
            {
                Chromosome = new CraneChromosome
                {
                    CranePositions = new List<DomainPoint3D>
                    {
                        new DomainPoint3D(1.1, 1.1, 0),
                        new DomainPoint3D(9.0, 9.0, 0)
                    }
                },
                Fitness = 0.8,
                CoverageRatio = 1.0
            };

            var gaEngine = new StubGAEngine(solution);
            var configEngine = new CraneConfigurationEngine();
            var liftingEngine = new LiftingCalculationEngine(configEngine);
            var assignmentService = new ElementAssignmentService(liftingEngine, configEngine);
            var coverageEngine = new CoverageEngine();
            var diagnosticsService = new StubDiagnosticsService();

            var placementEngine = new CranePlacementEngine(
                gaEngine,
                assignmentService,
                coverageEngine,
                configEngine,
                diagnosticsService);

            var settings = new GASettings
            {
                EnableConcurrentOptimization = true,
                CraneFootprintRadius = 1.0,
                RenderedOperatingEnvelopeRadius = 15.0,
                MinimumRelocationDistance = 3.0,
                WireLength = 2.0,
                DeratingRatio = 1.0,
                SafetyFactor = 1.0,
                MaxCranePositions = 3,
                DuplicatePositionDistance = 2.0,
                RelocationWeight = 5.0,
                CoverageWeight = 10.0,
                MaxRadiusForNormalisation = 50.0
            };

            var craneSpec = new CraneSpecification
            {
                Id = 1,
                MaxRadius = 15,
                MaxBoomLength = 20,
                LoadChart = new List<TargetPoint>
                {
                    new TargetPoint { Radius = 1.0, BoomLength = 10.0, Capacity = 50.0 },
                    new TargetPoint { Radius = 12.0, BoomLength = 15.0, Capacity = 50.0 }
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
                new PrecastElement { ElementID = "EL-T1", ElementCOG = new DomainPoint3D(1.1, 1.1, 0), ElementHeight = 1.0, ElementWT = 10.0 },
                new PrecastElement { ElementID = "EL-T2", ElementCOG = new DomainPoint3D(1.1, 1.1, 0), ElementHeight = 1.0, ElementWT = 10.0 }
            };

            var input = new PlacementInput
            {
                Zones = new List<GreenAreaZone> { allowedZone },
                ForbiddenZones = new List<GreenAreaZone>(),
                Elements = elements,
                Assignment = new Dictionary<int, List<string>>(),
                CraneSpec = craneSpec,
                Settings = settings,
                TotalElementsCount = elements.Count
            };

            var result = placementEngine.RunOptimization(input);

            // The position (9.0, 9.0, 0) should cover 0 elements uniquely and be filtered out.
            // Only (1.1, 1.1, 0) should remain.
            Assert.Single(result.Positions);
            Assert.Equal(1.1, result.Positions[0].X);
            Assert.Equal(1.1, result.Positions[0].Y);
            Assert.Equal(2, result.Positions[0].NewLiftableCount);
        }

        [Fact]
        public void RunOptimization_RejectsInvalidFinalFootprintPositionBeforeReturningResult()
        {
            var solution = new CraneSolution
            {
                Chromosome = new CraneChromosome
                {
                    CranePositions = new List<DomainPoint3D>
                    {
                        new DomainPoint3D(0.25, 5.0, 0),
                        new DomainPoint3D(5.0, 5.0, 0)
                    }
                },
                Fitness = 0.8,
                CoverageRatio = 1.0
            };

            var configEngine = new CraneConfigurationEngine();
            var liftingEngine = new LiftingCalculationEngine(configEngine);
            var assignmentService = new ElementAssignmentService(liftingEngine, configEngine);
            var placementEngine = new CranePlacementEngine(
                new StubGAEngine(solution),
                assignmentService,
                new CoverageEngine(),
                configEngine,
                new StubDiagnosticsService());

            var settings = new GASettings
            {
                EnableConcurrentOptimization = true,
                CraneFootprintRadius = 1.0,
                MinimumRelocationDistance = 1.0,
                DuplicatePositionDistance = 1.0,
                WireLength = 0.0,
                DeratingRatio = 1.0,
                SafetyFactor = 1.0,
                MaxCranePositions = 2,
                MaxRadiusForNormalisation = 20
            };

            var elements = new List<PrecastElement>
            {
                new PrecastElement
                {
                    ElementID = "EL-FINAL-1",
                    ElementCOG = new DomainPoint3D(5, 5, 0),
                    ElementHeight = 1.0,
                    ElementWT = 10.0
                }
            };

            var input = new PlacementInput
            {
                Zones = new List<GreenAreaZone> { CreateSquare(0, 0, 10, 10) },
                ForbiddenZones = new List<GreenAreaZone>(),
                Elements = elements,
                Assignment = new Dictionary<int, List<string>>(),
                CraneSpec = new CraneSpecification
                {
                    MaxRadius = 20,
                    MaxBoomLength = 30,
                    LoadChart = new List<TargetPoint>
                    {
                        new TargetPoint { Radius = 1, BoomLength = 1, Capacity = 100 }
                    }
                },
                Settings = settings,
                TotalElementsCount = elements.Count
            };

            var exception = Assert.Throws<InvalidOperationException>(() => placementEngine.RunOptimization(input));

            Assert.Contains("physical ground footprint", exception.Message);
        }

        private static GreenAreaZone CreateSquare(double minX, double minY, double maxX, double maxY)
        {
            return new GreenAreaZone
            {
                Vertices = new List<DomainPoint3D>
                {
                    new DomainPoint3D(minX, minY, 0),
                    new DomainPoint3D(maxX, minY, 0),
                    new DomainPoint3D(maxX, maxY, 0),
                    new DomainPoint3D(minX, maxY, 0)
                }
            };
        }
    }
}
