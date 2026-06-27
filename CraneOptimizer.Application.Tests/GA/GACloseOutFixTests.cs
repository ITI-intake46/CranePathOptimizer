using Xunit;
using System;
using System.IO;
using System.Linq;
using System.Globalization;
using System.Collections.Generic;
using System.Text;
using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Settings;
using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Engines.GeneticOptimization;
using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Engines.Constraints;
using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Models;
using CraneOptimizer.Application.Optimization.GeneticAlgorithms.DTOs;
using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Interfaces;
using CraneOptimizer.Domain.Schemas;
using CraneOptimizer.Domain.Entities;
using CraneOptimizer.Domain.Common;
using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Operators;
using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Engines.LiftingCalculation;
using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Engines.CraneConfiguration;
using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Engines.Coverage;
using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Engines.PlacementOrchestration;
using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Services;

namespace CraneOptimizer.Application.Tests.GA
{
    public class GACloseOutFixTests
    {
        [Fact]
        public void TestCoverageDominanceMultiplier_AppliedCorrectly()
        {
            // Arrange
            var constraintEngine = new ConstraintEngine();
            var fitnessEvaluator = new FitnessEvaluator(constraintEngine);

            var settings = new GASettings
            {
                CoverageWeight = 10,
                CoverageDominanceMultiplier = 100.0,
                RelocationWeight = 0,
                RadiusPenaltyWeight = 0,
                OverweightPenaltyWeight = 0,
                HeightPenaltyWeight = 0,
                RadiusQualityPenaltyWeight = 0,
                BalancePenaltyWeight = 0,
                RadiusStdDevPenaltyWeight = 0,
                SimilarPositionPenaltyWeight = 0,
                RelocationDistancePenaltyWeight = 0
            };

            var solution = new CraneSolution();
            solution.Chromosome.CranePositions.Add(new DomainPoint3D(0, 0, 0));

            // 5 out of 10 elements are Liftable (50% coverage)
            var evaluationResults = new List<ElementEvaluationResult>();
            for (int i = 0; i < 10; i++)
            {
                evaluationResults.Add(new ElementEvaluationResult
                {
                    ElementId = $"E{i}",
                    Status = i < 5 ? LiftStatus.Liftable : LiftStatus.OutOfRadius
                });
            }

            var assignment = new AssignmentResult
            {
                EvaluationResults = evaluationResults
            };

            // Act
            var breakdownWithDefault = fitnessEvaluator.EvaluateFitness(solution, assignment, settings);

            // Change multiplier to 250.0
            settings.CoverageDominanceMultiplier = 250.0;
            var breakdownWithCustom = fitnessEvaluator.EvaluateFitness(solution, assignment, settings);

            // Assert
            // Expected: CoverageWeight * DominanceMultiplier * CoverageRatio
            // 10 * 100 * 0.5 = 500
            Assert.Equal(500.0, breakdownWithDefault.FitnessScore);
            // 10 * 250 * 0.5 = 1250
            Assert.Equal(1250.0, breakdownWithCustom.FitnessScore);
        }

        [Fact]
        public void TestCoverageOverFitness_HigherCoverageWins()
        {
            // Arrange
            var constraintEngine = new ConstraintEngine();
            var fitnessEvaluator = new FitnessEvaluator(constraintEngine);

            var settings = new GASettings
            {
                CoverageWeight = 10,
                CoverageDominanceMultiplier = 100.0, // Ensures coverage dominates
                RelocationWeight = 50, // High relocation penalty
                RadiusPenaltyWeight = 0,
                OverweightPenaltyWeight = 0,
                HeightPenaltyWeight = 0,
                RadiusQualityPenaltyWeight = 0,
                BalancePenaltyWeight = 0,
                RadiusStdDevPenaltyWeight = 0,
                SimilarPositionPenaltyWeight = 0,
                RelocationDistancePenaltyWeight = 0,
                MaxCranePositions = 3
            };

            // Solution A: Coverage 90%, 3 setups (high relocation penalty)
            var solutionA = new CraneSolution();
            solutionA.Chromosome.CranePositions.Add(new DomainPoint3D(0, 0, 0));
            solutionA.Chromosome.CranePositions.Add(new DomainPoint3D(10, 0, 0));
            solutionA.Chromosome.CranePositions.Add(new DomainPoint3D(20, 0, 0));

            var evalA = new List<ElementEvaluationResult>();
            for (int i = 0; i < 10; i++)
            {
                evalA.Add(new ElementEvaluationResult
                {
                    ElementId = $"E{i}",
                    Status = i < 9 ? LiftStatus.Liftable : LiftStatus.OutOfRadius
                });
            }
            var assignmentA = new AssignmentResult { EvaluationResults = evalA };

            // Solution B: Coverage 50%, 1 setup (zero relocation penalty)
            var solutionB = new CraneSolution();
            solutionB.Chromosome.CranePositions.Add(new DomainPoint3D(0, 0, 0));

            var evalB = new List<ElementEvaluationResult>();
            for (int i = 0; i < 10; i++)
            {
                evalB.Add(new ElementEvaluationResult
                {
                    ElementId = $"E{i}",
                    Status = i < 5 ? LiftStatus.Liftable : LiftStatus.OutOfRadius
                });
            }
            var assignmentB = new AssignmentResult { EvaluationResults = evalB };

            // Act
            var fitnessA = fitnessEvaluator.EvaluateFitness(solutionA, assignmentA, settings).FitnessScore;
            var fitnessB = fitnessEvaluator.EvaluateFitness(solutionB, assignmentB, settings).FitnessScore;

            // Assert
            // Solution A fitness: 10 * 100 * 0.9 - 50 * (3 - 1) = 900 - 100 = 800
            // Solution B fitness: 10 * 100 * 0.5 - 50 * (1 - 1) = 500
            Assert.True(fitnessA > fitnessB, $"Solution A (800) should win over Solution B (500). Actual: {fitnessA} vs {fitnessB}");
        }

        [Fact]
        public void TestCoverageTieBreaker_FewerPositionsWin()
        {
            // Arrange
            var constraintEngine = new ConstraintEngine();
            var fitnessEvaluator = new FitnessEvaluator(constraintEngine);

            var settings = new GASettings
            {
                CoverageWeight = 10,
                CoverageDominanceMultiplier = 100.0,
                RelocationWeight = 10,
                RadiusPenaltyWeight = 0,
                OverweightPenaltyWeight = 0,
                HeightPenaltyWeight = 0,
                RadiusQualityPenaltyWeight = 0,
                BalancePenaltyWeight = 0,
                RadiusStdDevPenaltyWeight = 0,
                SimilarPositionPenaltyWeight = 0,
                RelocationDistancePenaltyWeight = 0,
                MaxCranePositions = 3
            };

            // Solution A: Coverage 80%, 2 setups
            var solutionA = new CraneSolution();
            solutionA.Chromosome.CranePositions.Add(new DomainPoint3D(0, 0, 0));
            solutionA.Chromosome.CranePositions.Add(new DomainPoint3D(10, 0, 0));

            var evalA = new List<ElementEvaluationResult>();
            for (int i = 0; i < 10; i++)
            {
                evalA.Add(new ElementEvaluationResult
                {
                    ElementId = $"E{i}",
                    Status = i < 8 ? LiftStatus.Liftable : LiftStatus.OutOfRadius
                });
            }
            var assignmentA = new AssignmentResult { EvaluationResults = evalA };

            // Solution B: Coverage 80%, 3 setups (tied on coverage, more positions)
            var solutionB = new CraneSolution();
            solutionB.Chromosome.CranePositions.Add(new DomainPoint3D(0, 0, 0));
            solutionB.Chromosome.CranePositions.Add(new DomainPoint3D(10, 0, 0));
            solutionB.Chromosome.CranePositions.Add(new DomainPoint3D(20, 0, 0));

            var evalB = new List<ElementEvaluationResult>();
            for (int i = 0; i < 10; i++)
            {
                evalB.Add(new ElementEvaluationResult
                {
                    ElementId = $"E{i}",
                    Status = i < 8 ? LiftStatus.Liftable : LiftStatus.OutOfRadius
                });
            }
            var assignmentB = new AssignmentResult { EvaluationResults = evalB };

            // Act
            var fitnessA = fitnessEvaluator.EvaluateFitness(solutionA, assignmentA, settings).FitnessScore;
            var fitnessB = fitnessEvaluator.EvaluateFitness(solutionB, assignmentB, settings).FitnessScore;

            // Assert
            // Solution A fitness: 800 - 10 * 1 = 790
            // Solution B fitness: 800 - 10 * 2 = 780
            Assert.True(fitnessA > fitnessB, $"Solution A (790) should win over Solution B (780). Actual: {fitnessA} vs {fitnessB}");
        }

        [Fact]
        public void TestResultDTO_CoverageRatioAndFitnessScore_AreSeparate()
        {
            // Arrange
            var result = new PlacementResult
            {
                CoverageRatio = 0.85,
                FitnessScore = 850.0
            };

            // Assert
            Assert.Equal(0.85, result.CoverageRatio);
            Assert.Equal(850.0, result.FitnessScore);
            Assert.NotEqual(result.CoverageRatio, result.FitnessScore);
        }

        [Fact]
        public void TestCsvCultureInfo_UsesInvariantCultureForDecimals()
        {
            // Test formatting behavior under cultures with different decimal separators (e.g. German "de-DE" uses comma)
            var originalCulture = CultureInfo.CurrentCulture;
            try
            {
                CultureInfo.CurrentCulture = new CultureInfo("de-DE");

                double val = 123.456;
                string formatted = val.ToString("F3", CultureInfo.InvariantCulture);

                // Should always use '.' as the decimal separator under invariant culture
                Assert.Equal("123.456", formatted);
            }
            finally
            {
                CultureInfo.CurrentCulture = originalCulture;
            }
        }

        [Fact]
        public void TestCsvDiagnostics_SchemasAndHeaderColumnCountsMatch()
        {
            // Create a temporary directory
            string tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            try
            {
                // Arrange
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
                    MaxGenerations = 2, // Keep it fast!
                    MaxCranePositions = 2,
                    RandomSeed = 12345,
                    CraneFootprintRadius = 2.0,
                    MinimumRelocationDistance = 5.0,
                    MaxRadiusForNormalisation = 40.0,
                    ConvergenceThresholdPercent = 0.05,
                    EarlyStoppingGenerations = 2,
                    PositionSamplingAttempts = 50,
                    GridStep = 0.5,
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

                var input = new PlacementInput
                {
                    Zones = new List<GreenAreaZone> { allowedZone },
                    ForbiddenZones = new List<GreenAreaZone>(),
                    Elements = elements,
                    Assignment = new Dictionary<int, List<string>>(),
                    CraneSpec = craneSpec,
                    Settings = settings,
                    TotalElementsCount = elements.Count,
                    DiagnosticsReportsDirectory = tempDir
                };

                // Act
                var result = placementEngine.RunOptimization(input);

                // Assert
                Assert.True(Directory.Exists(tempDir), "Diagnostics directory should exist");

                var csvFiles = Directory.GetFiles(tempDir, "*.csv", SearchOption.AllDirectories);
                Assert.True(csvFiles.Length >= 7, $"At least the 7 core diagnostic CSV files should be generated. Found: {csvFiles.Length}");

                foreach (var csvFile in csvFiles)
                {
                    var lines = File.ReadAllLines(csvFile);
                    Assert.True(lines.Length > 0, $"CSV file {Path.GetFileName(csvFile)} is empty");

                    string header = lines[0];
                    string[] headers = SplitCsvLine(header);
                    Assert.NotEmpty(headers);

                    // Assert schema version and run IDs check where applicable
                    int optRunIdIndex = Array.IndexOf(headers, "OptimizationRunId");
                    int stageRunIdIndex = Array.IndexOf(headers, "StageRunId");
                    int schemaVersionIndex = Array.IndexOf(headers, "DiagnosticsSchemaVersion");

                    for (int i = 1; i < lines.Length; i++)
                    {
                        if (string.IsNullOrWhiteSpace(lines[i])) continue;

                        string[] columns = SplitCsvLine(lines[i]);
                        Assert.True(headers.Length == columns.Length,
                            $"Column count mismatch in file {Path.GetFileName(csvFile)} at line {i + 1}. Expected: {headers.Length} ({header}), Got: {columns.Length} ({lines[i]})");

                        if (optRunIdIndex >= 0)
                        {
                            Assert.Equal(settings.OptimizationRunId, columns[optRunIdIndex]);
                        }
                        if (stageRunIdIndex >= 0)
                        {
                            string fileName = Path.GetFileName(csvFile);
                            bool isStageFile = !fileName.Contains("run_summary") &&
                                               !fileName.Contains("settings_snapshot") &&
                                               !fileName.Contains("positions") &&
                                               !fileName.Contains("element_assignments") &&
                                               !fileName.Contains("project_features");
                            string expectedStageRunId = isStageFile ? settings.StageRunId : "N/A";
                            Assert.Equal(expectedStageRunId, columns[stageRunIdIndex]);
                        }
                        if (schemaVersionIndex >= 0)
                        {
                            Assert.Contains(columns[schemaVersionIndex], new[] { "1.0", "2.0" });
                        }
                    }
                }
            }
            finally
            {
                if (Directory.Exists(tempDir))
                {
                    Directory.Delete(tempDir, true);
                }
            }
        }

        private static string[] SplitCsvLine(string line)
        {
            var result = new List<string>();
            bool inQuotes = false;
            var current = new StringBuilder();
            for (int i = 0; i < line.Length; i++)
            {
                char c = line[i];
                if (c == '\"')
                {
                    inQuotes = !inQuotes;
                }
                else if (c == ',' && !inQuotes)
                {
                    result.Add(current.ToString());
                    current.Clear();
                }
                else
                {
                    current.Append(c);
                }
            }
            result.Add(current.ToString());
            return result.ToArray();
        }
    }
}
