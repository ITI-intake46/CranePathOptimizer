using Xunit;
using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Engines.CraneConfiguration;
using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Engines.LiftingCalculation;
using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Engines.Constraints;
using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Settings;
using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Models;
using CraneOptimizer.Application.Tests.Legacy;
using CraneOptimizer.Domain.Common;
using CraneOptimizer.Domain.Entities;
using CraneOptimizer.Domain.Schemas;
using System;
using System.Collections.Generic;
using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Engines.GeneticOptimization;
using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Engines.Coverage;
using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Engines.PlacementOrchestration;
using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Operators;
using CraneOptimizer.Application.Optimization.GeneticAlgorithms.DTOs;
using CraneOptimizer.Application.Optimization.Utilities;
using Xunit.Abstractions;

namespace CraneOptimizer.Application.Tests
{
    public class GoldenMasterTests
    {
        private readonly ITestOutputHelper _output;

        public GoldenMasterTests(ITestOutputHelper output)
        {
            _output = output;
        }

        [Fact]
        public void TestLiftingEnginesAgainstLegacy()
        {
            var configEngine = new CraneConfigurationEngine();
            var liftingEngine = new LiftingCalculationEngine(configEngine);

            var settings = new GASettings
            {
                WireLength = 3.0,
                DeratingRatio = 0.85,
                SafetyFactor = 0.90
            };

            var craneSpec = new CraneSpecification
            {
                MaxRadius = 40,
                MaxBoomLength = 50,
                LoadChart = new List<TargetPoint>
                {
                    new TargetPoint { Radius = 10, BoomLength = 20, Capacity = 50 },
                    new TargetPoint { Radius = 10, BoomLength = 30, Capacity = 40 },
                    new TargetPoint { Radius = 20, BoomLength = 20, Capacity = 30 },
                    new TargetPoint { Radius = 20, BoomLength = 30, Capacity = 20 }
                }
            };

            var cranePositions = new List<DomainPoint3D>
            {
                new DomainPoint3D(0, 0, 0),
                new DomainPoint3D(5, 5, 2),
                new DomainPoint3D(-10, 20, 5)
            };

            var elements = new List<PrecastElement>
            {
                new PrecastElement { ElementCOG = new DomainPoint3D(10, 10, 10), ElementHeight = 4.0, ElementWT = 12.0 },
                new PrecastElement { ElementCOG = new DomainPoint3D(20, -5, 12), ElementHeight = 3.5, ElementWT = 15.5 },
                new PrecastElement { ElementCOG = new DomainPoint3D(-15, 25, 8), ElementHeight = 5.0, ElementWT = 8.0 }
            };

            foreach (var pos in cranePositions)
            {
                foreach (var el in elements)
                {
                    // 1. Radius
                    double radiusNew = liftingEngine.CalculateRadius(pos, el);
                    double radiusOld = LiftCalculationLegacy.CalculateRadius(pos, el);
                    Assert.Equal(radiusOld, radiusNew, 7);

                    // 2. Height
                    double heightNew = liftingEngine.CalculateLiftHeight(el, pos, settings);
                    double heightOld = LiftCalculationLegacy.CalculateLiftHeight(el, pos, settings);
                    Assert.Equal(heightOld, heightNew, 7);

                    // 3. Boom Length
                    double boomNew = configEngine.CalculateRequiredBoomLength(radiusNew, heightNew);
                    double boomOld = LiftCalculationLegacy.CalculateRequiredBoomLength(radiusOld, heightOld);
                    Assert.Equal(boomOld, boomNew, 7);

                    // 4. Capacity
                    double capNew = configEngine.CraneCapacity(craneSpec, radiusNew, boomNew, settings);
                    double capOld = LiftCalculationLegacy.CraneCapacity(craneSpec, radiusOld, boomOld, settings);
                    Assert.Equal(capOld, capNew, 7);

                    // 5. Status
                    double reqWeight = liftingEngine.CalculateRequiredWeight(el, settings);
                    var statusNew = liftingEngine.DetermineLiftStatus(radiusNew, boomNew, reqWeight, craneSpec, settings);
                    var statusOld = LiftCalculationLegacy.DetermineLiftStatus(radiusOld, boomOld, reqWeight, craneSpec, settings);
                    
                    var statusNewMapped = statusNew == LiftStatus.NotCharted ? LiftStatus.SpecialCase : statusNew;
                    Assert.Equal(statusOld, statusNewMapped);
                }
            }
        }

        [Fact]
        public void TestConstraintEngineAgainstLegacy()
        {
            var engine = new ConstraintEngine();
            var settings = new GASettings
            {
                CraneFootprintRadius = 2.5,
                MinimumRelocationDistance = 8.0,
                MaxRadiusForNormalisation = 40.0,
                MaxCranePositions = 5
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

            var forbiddenZone = new GreenAreaZone
            {
                Vertices = new List<DomainPoint3D>
                {
                    new DomainPoint3D(-5, -5, 0),
                    new DomainPoint3D(5, -5, 0),
                    new DomainPoint3D(5, 5, 0),
                    new DomainPoint3D(-5, 5, 0)
                }
            };

            var allowedZones = new List<GreenAreaZone> { allowedZone };
            var forbiddenZones = new List<GreenAreaZone> { forbiddenZone };

            var testPoints = new List<DomainPoint3D>
            {
                new DomainPoint3D(0, 0, 0),
                new DomainPoint3D(10, 10, 0),
                new DomainPoint3D(3, 3, 0),
                new DomainPoint3D(12, 12, 0),
                new DomainPoint3D(45, -45, 0),
                new DomainPoint3D(55, 55, 0)
            };

            // 1. IsValidSetupPoint
            foreach (var p in testPoints)
            {
                bool validNew = engine.IsValidSetupPoint(p, allowedZones, forbiddenZones, settings);
                bool validOld = CranePlacementConstraintServiceLegacy.IsValidSetupPoint(p, allowedZones, forbiddenZones, settings);
                Assert.Equal(validOld, validNew);
            }

            // 2. IsMeaningfullySeparated
            var existing = new List<DomainPoint3D> { new DomainPoint3D(10, 10, 0) };
            foreach (var p in testPoints)
            {
                bool sepNew = engine.IsMeaningfullySeparated(p, existing, settings);
                bool sepOld = CranePlacementConstraintServiceLegacy.IsMeaningfullySeparated(p, existing, settings);
                Assert.Equal(sepOld, sepNew);
            }

            // 3. FilterDistinctValidPoints
            var filteredNew = engine.FilterDistinctValidPoints(testPoints, allowedZones, forbiddenZones, settings);
            var filteredOld = CranePlacementConstraintServiceLegacy.FilterDistinctValidPoints(testPoints, allowedZones, forbiddenZones, settings);
            Assert.Equal(filteredOld.Count, filteredNew.Count);
            for (int i = 0; i < filteredNew.Count; i++)
            {
                Assert.Equal(filteredOld[i].X, filteredNew[i].X, 7);
                Assert.Equal(filteredOld[i].Y, filteredNew[i].Y, 7);
            }

            // 4. CalculateSimilarityPenalty
            double penaltyNew = engine.CalculateSimilarityPenalty(testPoints, settings);
            double penaltyOld = CranePlacementConstraintServiceLegacy.CalculateSimilarityPenalty(testPoints, settings);
            Assert.Equal(penaltyOld, penaltyNew, 7);

            // 5. CalculateNormalizedRelocationDistance
            double distNew = engine.CalculateNormalizedRelocationDistance(testPoints, settings);
            double distOld = CranePlacementConstraintServiceLegacy.CalculateNormalizedRelocationDistance(testPoints, settings);
            Assert.Equal(distOld, distNew, 7);
        }

        [Fact]
        public void RunFullGAOptimizationProfiling()
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

            var diagnosticsService = new CraneOptimizer.Application.Optimization.GeneticAlgorithms.Services.GACsvDiagnosticsService(liftingEngine, configEngine);
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
                PopulationSize = 15,
                MaxGenerations = 10,
                MaxCranePositions = 4,
                RandomSeed = 42,
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
                EnableProfiling = true
            };

            var craneSpec = new CraneSpecification
            {
                MaxRadius = 40,
                MaxBoomLength = 50,
                LoadChart = new List<TargetPoint>
                {
                    new TargetPoint { Radius = 10, BoomLength = 20, Capacity = 50 },
                    new TargetPoint { Radius = 10, BoomLength = 30, Capacity = 40 },
                    new TargetPoint { Radius = 20, BoomLength = 20, Capacity = 30 },
                    new TargetPoint { Radius = 20, BoomLength = 30, Capacity = 20 }
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

            var forbiddenZone = new GreenAreaZone
            {
                Vertices = new List<DomainPoint3D>
                {
                    new DomainPoint3D(-5, -5, 0),
                    new DomainPoint3D(5, -5, 0),
                    new DomainPoint3D(5, 5, 0),
                    new DomainPoint3D(-5, 5, 0)
                }
            };

            var elements = new List<PrecastElement>
            {
                new PrecastElement { ElementID = "EL-1", ElementCOG = new DomainPoint3D(10, 10, 1), ElementHeight = 4.0, ElementWT = 12.0 },
                new PrecastElement { ElementID = "EL-2", ElementCOG = new DomainPoint3D(20, -5, 2), ElementHeight = 3.5, ElementWT = 15.5 },
                new PrecastElement { ElementID = "EL-3", ElementCOG = new DomainPoint3D(-15, 25, 3), ElementHeight = 5.0, ElementWT = 8.0 },
                new PrecastElement { ElementID = "EL-4", ElementCOG = new DomainPoint3D(-25, -20, 1), ElementHeight = 2.0, ElementWT = 10.0 },
                new PrecastElement { ElementID = "EL-5", ElementCOG = new DomainPoint3D(30, 30, 2), ElementHeight = 3.0, ElementWT = 14.0 },
                new PrecastElement { ElementID = "EL-6", ElementCOG = new DomainPoint3D(5, -20, 1), ElementHeight = 3.0, ElementWT = 11.0 }
            };

            var input = new PlacementInput
            {
                Zones = new List<GreenAreaZone> { allowedZone },
                ForbiddenZones = new List<GreenAreaZone> { forbiddenZone },
                Elements = elements,
                Assignment = new Dictionary<int, List<string>>(),
                CraneSpec = craneSpec,
                Settings = settings
            };

            var result = placementEngine.RunOptimization(input);

            Assert.NotNull(result);
            Assert.NotEmpty(result.Positions);

            string report = GAProfiler.GetSummaryReport();
            _output.WriteLine(report);
        }

        [Fact]
        public void RunBenchmarkScenarios()
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

            var diagnosticsService = new CraneOptimizer.Application.Optimization.GeneticAlgorithms.Services.GACsvDiagnosticsService(liftingEngine, configEngine);
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
                PopulationSize = 15,
                MaxGenerations = 10,
                MutationRate = 0.1,
                MaxCranePositions = 4,
                RandomSeed = 42,
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
                EnableProfiling = true,
                EnableAdaptiveSigma = false,
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
                    new TargetPoint { Radius = 10, BoomLength = 30, Capacity = 40 },
                    new TargetPoint { Radius = 20, BoomLength = 20, Capacity = 30 },
                    new TargetPoint { Radius = 20, BoomLength = 30, Capacity = 20 }
                }
            };

            var allowedZone = new GreenAreaZone
            {
                Vertices = new List<DomainPoint3D>
                {
                    new DomainPoint3D(-100, -100, 0),
                    new DomainPoint3D(100, -100, 0),
                    new DomainPoint3D(100, 100, 0),
                    new DomainPoint3D(-100, 100, 0)
                }
            };

            var forbiddenZone = new GreenAreaZone
            {
                Vertices = new List<DomainPoint3D>
                {
                    new DomainPoint3D(-5, -5, 0),
                    new DomainPoint3D(5, -5, 0),
                    new DomainPoint3D(5, 5, 0),
                    new DomainPoint3D(-5, 5, 0)
                }
            };

            // Run Scenario 1: Small selected element set (10 elements)
            _output.WriteLine("\n=== RUNNING BENCHMARK SCENARIO 1: SMALL SET (10 ELEMENTS) ===");
            var smallElements = GenerateMockElements(10);
            var inputSmall1 = CreateInput("Small Set (Scenario 1)", smallElements, allowedZone, forbiddenZone, craneSpec, settings);
            var resultSmall1 = placementEngine.RunOptimization(inputSmall1);
            _output.WriteLine($"Scenario 1 Fitness: {resultSmall1.FitnessScore:F4}, Coverage: {resultSmall1.CoverageRatio:P2}");
            _output.WriteLine(GAProfiler.GetSummaryReport());

            var inputSmall2 = CreateInput("Small Set (Scenario 1)", smallElements, allowedZone, forbiddenZone, craneSpec, settings);
            var resultSmall2 = placementEngine.RunOptimization(inputSmall2);
            Assert.Equal(resultSmall1.FitnessScore, resultSmall2.FitnessScore, 7);

            // Run Scenario 2: Medium selected element set (50 elements)
            _output.WriteLine("\n=== RUNNING BENCHMARK SCENARIO 2: MEDIUM SET (50 ELEMENTS) ===");
            var mediumElements = GenerateMockElements(50);
            var inputMed1 = CreateInput("Medium Set (Scenario 2)", mediumElements, allowedZone, forbiddenZone, craneSpec, settings);
            var resultMed1 = placementEngine.RunOptimization(inputMed1);
            _output.WriteLine($"Scenario 2 Fitness: {resultMed1.FitnessScore:F4}, Coverage: {resultMed1.CoverageRatio:P2}");
            _output.WriteLine(GAProfiler.GetSummaryReport());

            var inputMed2 = CreateInput("Medium Set (Scenario 2)", mediumElements, allowedZone, forbiddenZone, craneSpec, settings);
            var resultMed2 = placementEngine.RunOptimization(inputMed2);
            Assert.Equal(resultMed1.FitnessScore, resultMed2.FitnessScore, 7);

            // Run Scenario 3: Full/Large selected element set (200 elements)
            _output.WriteLine("\n=== RUNNING BENCHMARK SCENARIO 3: LARGE SET (200 ELEMENTS) ===");
            var largeElements = GenerateMockElements(200);
            var inputLarge1 = CreateInput("Large Set (Scenario 3)", largeElements, allowedZone, forbiddenZone, craneSpec, settings);
            var resultLarge1 = placementEngine.RunOptimization(inputLarge1);
            _output.WriteLine($"Scenario 3 Fitness: {resultLarge1.FitnessScore:F4}, Coverage: {resultLarge1.CoverageRatio:P2}");
            _output.WriteLine(GAProfiler.GetSummaryReport());

            var inputLarge2 = CreateInput("Large Set (Scenario 3)", largeElements, allowedZone, forbiddenZone, craneSpec, settings);
            var resultLarge2 = placementEngine.RunOptimization(inputLarge2);
            Assert.Equal(resultLarge1.FitnessScore, resultLarge2.FitnessScore, 7);
        }

        private List<PrecastElement> GenerateMockElements(int count)
        {
            var list = new List<PrecastElement>();
            var random = new Random(42);
            for (int i = 0; i < count; i++)
            {
                double angle = (double)i / count * 2.0 * Math.PI;
                double distance = 10.0 + random.NextDouble() * 30.0;
                double x = distance * Math.Cos(angle);
                double y = distance * Math.Sin(angle);
                list.Add(new PrecastElement
                {
                    ElementID = $"EL-{i + 1}",
                    ElementCOG = new DomainPoint3D(x, y, 1.0 + random.NextDouble() * 3.0),
                    ElementHeight = 3.0 + random.NextDouble() * 2.0,
                    ElementWT = 5.0 + random.NextDouble() * 15.0
                });
            }
            return list;
        }

        private PlacementInput CreateInput(
            string scenarioName,
            List<PrecastElement> elements,
            GreenAreaZone allowed,
            GreenAreaZone forbidden,
            CraneSpecification craneSpec,
            GASettings settings)
        {
            return new PlacementInput
            {
                Zones = new List<GreenAreaZone> { allowed },
                ForbiddenZones = new List<GreenAreaZone> { forbidden },
                Elements = elements,
                Assignment = new Dictionary<int, List<string>>(),
                CraneSpec = craneSpec,
                Settings = settings,
                ProjectName = scenarioName,
                ProjectFolderPath = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CraneOptimizer", "Benchmarks"),
                IfcFileName = $"{scenarioName.Replace(" ", "_")}.ifc",
                TotalElementsCount = elements.Count
            };
        }

        [Fact]
        public void TestGACsvDiagnosticsAndScenarios()
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

            var diagnosticsService = new CraneOptimizer.Application.Optimization.GeneticAlgorithms.Services.GACsvDiagnosticsService(liftingEngine, configEngine);

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
                MaxGenerations = 5,
                MaxCranePositions = 4,
                RandomSeed = 42,
                CraneFootprintRadius = 2.0,
                MinimumRelocationDistance = 5.0,
                MaxRadiusForNormalisation = 40.0,
                EnableProfiling = true
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
                    new DomainPoint3D(-100, -100, 0),
                    new DomainPoint3D(100, -100, 0),
                    new DomainPoint3D(100, 100, 0),
                    new DomainPoint3D(-100, 100, 0)
                }
            };

            var forbiddenZone = new GreenAreaZone
            {
                Vertices = new List<DomainPoint3D>()
            };

            // Setup temporary project directory for testing
            string tempProjectFolder = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "CraneOptimizer_TestProject_" + Guid.NewGuid().ToString("N"));
            System.IO.Directory.CreateDirectory(tempProjectFolder);

            // Record old global directory state
            string oldGlobalDir = System.IO.Path.Combine(AppContext.BaseDirectory, "Data", "GAReports");
            int oldGlobalFileCount = System.IO.Directory.Exists(oldGlobalDir) ? System.IO.Directory.GetFiles(oldGlobalDir).Length : 0;

            try
            {
                // Scenario 1: High Coverage Scenario (10 extremely light elements)
                var lightElements = new List<PrecastElement>();
                for (int i = 0; i < 10; i++)
                {
                    lightElements.Add(new PrecastElement
                    {
                        ElementID = $"Light-{i + 1}",
                        ElementCOG = new DomainPoint3D(5.0 + i, 5.0, 1.0),
                        ElementWT = 1.0 // extremely light
                    });
                }
                settings.DiagnosticsLevel = CraneOptimizer.Application.Optimization.GeneticAlgorithms.Settings.DiagnosticsLevel.Research;
                var inputHigh = CreateInput("High Coverage Project", lightElements, allowedZone, forbiddenZone, craneSpec, settings);
                inputHigh.ProjectFolderPath = tempProjectFolder;
                inputHigh.DiagnosticsReportsDirectory = System.IO.Path.Combine(tempProjectFolder, "Reports", "GA");

                var resultHigh = placementEngine.RunOptimization(inputHigh);

                // Verify files exist and are correct
                // Per-run folder: reportsFolder/<ProjectName>/<OptimizationRunId>/
                string reportsFolder = inputHigh.DiagnosticsReportsDirectory;
                Assert.True(System.IO.File.Exists(resultHigh.DiagnosticsReportPath), "Detailed diagnostics CSV should be created.");
                string historyPath = resultHigh.DiagnosticsReportPath.Replace("debug_ga_diagnostics_", "debug_ga_run_history_");
                Assert.True(System.IO.File.Exists(historyPath), "debug_ga_run_history.csv should be created.");

                string historyText = System.IO.File.ReadAllText(historyPath);
                Assert.Contains("High Coverage Project", historyText);
                Assert.Contains("Excellent", historyText); // health should be Excellent since coverage >= 90%

                // Scenario 2: Low Coverage Scenario (10 extremely heavy elements)
                var heavyElements = new List<PrecastElement>();
                for (int i = 0; i < 10; i++)
                {
                    heavyElements.Add(new PrecastElement
                    {
                        ElementID = $"Heavy-{i + 1}",
                        ElementCOG = new DomainPoint3D(5.0 + i, 5.0, 1.0),
                        ElementWT = 500.0 // extremely heavy, cannot lift
                    });
                }
                var inputLow = CreateInput("Low Coverage Project", heavyElements, allowedZone, forbiddenZone, craneSpec, settings);
                inputLow.ProjectFolderPath = tempProjectFolder;
                inputLow.DiagnosticsReportsDirectory = System.IO.Path.Combine(tempProjectFolder, "Reports", "GA");

                var resultLow = placementEngine.RunOptimization(inputLow);

                // Low Coverage run: also writes to a per-run subfolder
                Assert.True(System.IO.File.Exists(resultLow.DiagnosticsReportPath), "Detailed diagnostics CSV should be created.");
                string historyPathLow = resultLow.DiagnosticsReportPath.Replace("debug_ga_diagnostics_", "debug_ga_run_history_");
                string historyTextLow = System.IO.File.ReadAllText(historyPathLow);
                Assert.Contains("Low Coverage Project", historyTextLow);
                Assert.Contains("Critical", historyTextLow); // health should be Critical since coverage < 40%

                // Scenario 3: Large Selected Element Set (100 elements)
                var largeElements = GenerateMockElements(100);
                var inputLarge = CreateInput("Large Selected Project", largeElements, allowedZone, forbiddenZone, craneSpec, settings);
                inputLarge.ProjectFolderPath = tempProjectFolder;
                inputLarge.DiagnosticsReportsDirectory = System.IO.Path.Combine(tempProjectFolder, "Reports", "GA");

                var resultLarge = placementEngine.RunOptimization(inputLarge);

                // Verify failure breakdown CSV
                Assert.NotNull(resultLarge.DiagnosticsReportPath);
                string detailedCsvName = System.IO.Path.GetFileName(resultLarge.DiagnosticsReportPath)!;
                Assert.NotNull(detailedCsvName);
                string breakdownCsvName = detailedCsvName.Replace("debug_ga_diagnostics_", "debug_ga_failure_breakdown_");
                // The breakdown CSV sits in the same per-run folder as the detailed CSV
                string breakdownCsvPath = System.IO.Path.Combine(
                    System.IO.Path.GetDirectoryName(resultLarge.DiagnosticsReportPath)!, breakdownCsvName);

                Assert.True(System.IO.File.Exists(breakdownCsvPath), "Failure breakdown CSV should be created.");
                string breakdownText = System.IO.File.ReadAllText(breakdownCsvPath);
                Assert.Contains("OutOfRadius", breakdownText);
                Assert.Contains("Overweight", breakdownText);

                // Verify old global folder is NOT updated
                int newGlobalFileCount = System.IO.Directory.Exists(oldGlobalDir) ? System.IO.Directory.GetFiles(oldGlobalDir).Length : 0;
                Assert.Equal(oldGlobalFileCount, newGlobalFileCount);
            }
            finally
            {
                // Cleanup temporary folder
                try
                {
                    System.IO.Directory.Delete(tempProjectFolder, true);
                }
                catch {}
            }
        }
    }
}
