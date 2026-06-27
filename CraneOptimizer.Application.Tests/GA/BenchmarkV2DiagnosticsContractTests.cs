using CraneOptimizer.Application.Optimization.GeneticAlgorithms.DTOs;
using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Engines.CraneConfiguration;
using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Engines.LiftingCalculation;
using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Models;
using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Services;
using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Settings;
using CraneOptimizer.Application.Optimization.Utilities;
using CraneOptimizer.Domain.Common;
using CraneOptimizer.Domain.Diagnostics;
using CraneOptimizer.Domain.Entities;
using CraneOptimizer.Domain.Schemas;
using CraneOptimizer.Domain.Schemas.Enums;
using System.Globalization;
using System.Text;
using Xunit;

namespace CraneOptimizer.Application.Tests.GA
{
    public sealed class BenchmarkV2DiagnosticsContractTests
    {
        [Fact]
        public void GenerateDiagnostics_WritesJoinKeysAndEngineeringFormulas()
        {
            string tempDirectory = CreateTempDirectory();
            CultureInfo originalCulture = CultureInfo.CurrentCulture;

            try
            {
                GAProfiler.Reset();
                CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR");
                (GACsvDiagnosticsService service, PlacementInput input, PlacementResult result) =
                    CreateFixture(tempDirectory, "stage-1", 1, 0.5, 1);

                service.GenerateDiagnostics(input, result);

                // Files are written into a per-run subfolder: <tempDir>/<OptimizationRunId>
                string runDir = Path.Combine(tempDirectory, "optimization-1");

                string[] requiredFiles =
                {
                    "ga_audit_run_summary.csv",
                    "ga_audit_settings_snapshot.csv",
                    "ga_audit_stage_summary.csv",
                    "ga_audit_positions.csv",
                    "ga_audit_element_assignments.csv",
                    "ga_audit_project_features.csv",
                    "ga_audit_solution_quality_metrics.csv",
                    "ga_audit_zone_reachability_summary.csv"
                };

                foreach (string fileName in requiredFiles)
                {
                    string path = Path.Combine(runDir, fileName);
                    Assert.True(File.Exists(path), $"Missing {fileName}");
                    AssertCsvShapeAndUniqueHeaders(path);
                }

                foreach (string path in Directory.EnumerateFiles(runDir, "*.csv"))
                {
                    AssertCsvShapeAndUniqueHeaders(path);
                }

                Dictionary<string, string> assignment = ReadRows(
                        Path.Combine(runDir, "ga_audit_element_assignments.csv"))
                    .Single(row => row["ElementId"] == "EL-1");

                Assert.Equal("optimization-1", assignment["OptimizationRunId"]);
                Assert.Equal("N/A", assignment["StageRunId"]);
                Assert.Equal("N/A", assignment["StageIndex"]);
                Assert.Equal("2.0", assignment["DiagnosticsSchemaVersion"]);
                Assert.Equal("101", assignment["CraneSpecificationId"]);
                Assert.Equal("180Tons", assignment["CraneName"]);
                Assert.Equal("true", assignment["IsFinalLifted"]);
                Assert.Equal("true", ReadRows(
                    Path.Combine(runDir, "ga_audit_solution_quality_metrics.csv"))[0]["UseHungarianAssignment"]);
                Assert.Equal("Hungarian", ReadRows(
                    Path.Combine(runDir, "ga_audit_solution_quality_metrics.csv"))[0]["AssignmentEngineUsed"]);

                double elementWeight = Parse(assignment["ElementWeight"]);
                double additionalWeight = Parse(assignment["AdditionalWt"]);
                double requiredCapacity = Parse(assignment["RequiredCapacity"]);
                double availableCapacity = Parse(assignment["AvailableCapacity"]);
                double radius = Parse(assignment["Radius"]);
                double maxRadius = Parse(assignment["CraneMaxRadius"]);
                double boomLength = Parse(assignment["RequiredBoomLength"]);
                double maxBoomLength = Parse(assignment["CraneMaxBoomLength"]);

                Assert.Equal(elementWeight + additionalWeight, requiredCapacity, 6);
                Assert.Equal(requiredCapacity / availableCapacity, Parse(assignment["CapacityUtilization"]), 6);
                Assert.Equal(availableCapacity - requiredCapacity, Parse(assignment["CapacityMarginTons"]), 6);
                Assert.Equal(radius / maxRadius, Parse(assignment["RadiusUtilization"]), 6);
                Assert.Equal(boomLength / maxBoomLength, Parse(assignment["BoomUtilization"]), 6);
                Assert.Contains(".", assignment["CapacityUtilization"]);

                Dictionary<string, string> solution = ReadRows(
                    Path.Combine(runDir, "ga_audit_solution_quality_metrics.csv")).Single();
                Assert.InRange(Parse(solution["CoverageRatio"]), 0, 1);
                Assert.Equal(
                    Parse(solution["CoverageRatio"]) * 100,
                    Parse(solution["CoveragePercent"]),
                    4);

                Dictionary<string, string> features = ReadRows(
                    Path.Combine(runDir, "ga_audit_project_features.csv")).Single();
                Assert.Equal("project-guid-1", features["ProjectId"]);
                Assert.NotEqual("N/A", features["ScenarioId"]);
                Assert.NotEqual("N/A", features["ProjectFeatureSnapshotId"]);
                Assert.Equal("2", features["RawElementCount"]);
                Assert.Equal("2", features["OptimizationScopeElementCount"]);
                Assert.Equal("2", features["GaReadyElementCount"]);
                Assert.Equal("60.0000", features["CraneMaxRadius"]);

                string scenarioId = input.ScenarioId!;
                input.ScenarioId = null;
                CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("en-US");
                BenchmarkDiagnosticsIdentity.Ensure(input);
                Assert.Equal(scenarioId, input.ScenarioId);
            }
            finally
            {
                CultureInfo.CurrentCulture = originalCulture;
                GAProfiler.Reset();
                DeleteTempDirectory(tempDirectory);
            }
        }

        [Fact]
        public void GenerateDiagnostics_TwoStagesShareOptimizationIdAndHaveOneBestStage()
        {
            string tempDirectory = CreateTempDirectory();

            try
            {
                GAProfiler.Reset();
                (GACsvDiagnosticsService firstService, PlacementInput firstInput, PlacementResult firstResult) =
                    CreateFixture(tempDirectory, "stage-1", 1, 0.5, 1);
                firstService.GenerateDiagnostics(firstInput, firstResult);

                (GACsvDiagnosticsService secondService, PlacementInput secondInput, PlacementResult secondResult) =
                    CreateFixture(tempDirectory, "stage-2", 2, 1.0, 2);
                secondService.GenerateDiagnostics(secondInput, secondResult);

                // Per-run folder: stage-1 run and stage-2 run share the same OptimizationRunId
                // Files accumulate in <tempDir>/optimization-1/ since both stages use the same run id
                string runDir2 = Path.Combine(tempDirectory, "optimization-1");

                List<Dictionary<string, string>> rows = ReadRows(
                    Path.Combine(runDir2, "ga_audit_solution_quality_metrics.csv"));

                Assert.Equal(2, rows.Count);
                Assert.All(rows, row => Assert.Equal("optimization-1", row["OptimizationRunId"]));
                Assert.Equal(2, rows.Select(row => row["StageRunId"]).Distinct().Count());
                Assert.Equal(new[] { "1", "2" }, rows.Select(row => row["StageIndex"]).OrderBy(value => value));
                Assert.Single(rows, row => row["IsBestStage"] == "true");
                Assert.Equal("stage-2", rows.Single(row => row["IsBestStage"] == "true")["StageRunId"]);
                Assert.Equal("0.500000", rows.Single(row => row["StageRunId"] == "stage-2")["CoverageGainFromPreviousStage"]);
                Assert.Single(ReadRows(Path.Combine(runDir2, "ga_audit_project_features.csv")));
            }
            finally
            {
                GAProfiler.Reset();
                DeleteTempDirectory(tempDirectory);
            }
        }

        private static (GACsvDiagnosticsService Service, PlacementInput Input, PlacementResult Result)
            CreateFixture(
                string outputDirectory,
                string stageRunId,
                int stageIndex,
                double coverage,
                int positionCount)
        {
            GAProfiler.Enabled = true;
            GAProfiler.CurrentRunId = "optimization-1";

            var configurationEngine = new CraneConfigurationEngine();
            var liftingEngine = new LiftingCalculationEngine(configurationEngine);
            var capacityResolver = new LoadChartCapacityResolver();
            var constraintEngine = new CraneOptimizer.Application.Optimization.GeneticAlgorithms.Engines.Constraints.ConstraintEngine();
            var reachability = new ReachabilityMapService(constraintEngine, capacityResolver);
            var service = new GACsvDiagnosticsService(
                liftingEngine,
                configurationEngine,
                capacityResolver,
                null,
                reachability);

            var crane = new Crane
            {
                Id = 7,
                Name = "180Tons",
                VehicleLength = 15,
                VehicleWidth = 4,
                VehicleHeight = 3,
                OutriggerLength = 6,
                OutriggerWidth = 6
            };
            var craneSpecification = new CraneSpecification
            {
                Id = 101,
                CraneId = crane.Id,
                Crane = crane,
                MaxCapacity = 180,
                MinRadius = 2.5,
                MaxRadius = 60,
                MinBoomLength = 13.1,
                MaxBoomLength = 62,
                LoadChart = new List<TargetPoint>
                {
                    new() { Radius = 10, BoomLength = 20, Capacity = 50 },
                    new() { Radius = 20, BoomLength = 30, Capacity = 30 },
                    new() { Radius = 60, BoomLength = 62, Capacity = 10 }
                }
            };
            crane.Specifications.Add(craneSpecification);

            var zone = new GreenAreaZone
            {
                ZoneId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
                Name = "Allowed 1",
                Type = ZoneType.Allowed,
                Area = 400,
                Vertices = new List<DomainPoint3D>
                {
                    new(-10, -10, 0),
                    new(10, -10, 0),
                    new(10, 10, 0),
                    new(-10, 10, 0)
                }
            };

            var elements = new List<PrecastElement>
            {
                new()
                {
                    ElementID = "EL-1",
                    ElementType = "IfcWall",
                    ResolvedCategory = "Wall",
                    ElementCOG = new DomainPoint3D(10, 0, 2),
                    ElementHeight = 4,
                    ElementVolume = 3,
                    ElementWT = 10,
                    WeightTons = 10,
                    WeightTrustLevel = IfcWeightTrustLevel.ExplicitQuantity,
                    IncludeInGa = true
                },
                new()
                {
                    ElementID = "EL-2",
                    ElementType = "IfcColumn",
                    ResolvedCategory = "Column",
                    ElementCOG = new DomainPoint3D(20, 0, 2),
                    ElementHeight = 5,
                    ElementVolume = 4,
                    ElementWT = 20,
                    WeightTons = 20,
                    WeightTrustLevel = IfcWeightTrustLevel.ExplicitProperty,
                    IncludeInGa = true
                }
            };

            var settings = new GASettings
            {
                OptimizationRunId = "optimization-1",
                StageRunId = stageRunId,
                StageIndex = stageIndex,
                PopulationSize = 10,
                MaxGenerations = 2,
                MutationRate = 0.1,
                MutationSigma = 0.05,
                CrossoverRate = 0.8,
                MaxCranePositions = positionCount,
                GridStep = 5,
                CraneFootprintRadius = 1,
                AdditionalWt = 0.5,
                DeratingRatio = 0.9,
                SafetyFactor = 0.9,
                UseHungarianAssignment = true,
                EnableReachabilitySeeding = true,
                EnableMultiStageDiscovery = true,
                DiagnosticsLevel = CraneOptimizer.Application.Optimization.GeneticAlgorithms.Settings.DiagnosticsLevel.Research
            };

            var input = new PlacementInput
            {
                Zones = new List<GreenAreaZone> { zone },
                ForbiddenZones = new List<GreenAreaZone>(),
                Elements = elements,
                FeatureScopeElements = elements,
                Assignment = new Dictionary<int, List<string>>(),
                CraneSpec = craneSpecification,
                Settings = settings,
                ProjectId = "project-guid-1",
                ProjectName = "try4",
                IfcFileName = "try4.ifc",
                RawElementCount = 2,
                OptimizationScopeElementCount = 2,
                RequestedMaxCranePositions = 2,
                TotalElementsCount = 2,
                DiagnosticsReportsDirectory = outputDirectory
            };

            IReadOnlyList<ScoredCoordinate> hotSpots = reachability.ComputeHotSpots(
                elements,
                input.Zones,
                input.ForbiddenZones,
                craneSpecification,
                settings,
                10);
            if (hotSpots.Count > 0)
            {
                reachability.RecordSelectedSeed(hotSpots[0]);
            }

            var positions = new List<CranePositionResult>();
            for (int index = 0; index < positionCount; index++)
            {
                var position = new CranePositionResult
                {
                    X = index * 5,
                    Y = 0,
                    Z = 0
                };

                position.ElementResults.Add(new ElementLiftResult
                {
                    ElementId = "EL-1",
                    ElementType = "IfcWall",
                    Weight = 10,
                    Status = LiftStatus.Liftable,
                    Radius = 10,
                    BoomLength = 20,
                    RequiredCapacity = 10.5,
                    AvailableCapacity = 20,
                    SafetyMargin = 9.5,
                    ReachStatus = ReachStatus.Reachable,
                    ChartStatus = ChartStatus.ListedCapacityFound,
                    IsFinalAssignedToPoint = index == 0,
                    AssignmentScore = 1,
                    CandidatePointId = $"P-{index + 1}",
                    AssignedPointId = "P-1"
                });
                position.ElementResults.Add(new ElementLiftResult
                {
                    ElementId = "EL-2",
                    ElementType = "IfcColumn",
                    Weight = 20,
                    Status = positionCount > 1 && index == 1 ? LiftStatus.Liftable : LiftStatus.Overweight,
                    Radius = 20,
                    BoomLength = 30,
                    RequiredCapacity = 20.5,
                    AvailableCapacity = positionCount > 1 && index == 1 ? 25 : 15,
                    SafetyMargin = positionCount > 1 && index == 1 ? 4.5 : -5.5,
                    ReachStatus = ReachStatus.Reachable,
                    ChartStatus = ChartStatus.ListedCapacityFound,
                    IsFinalAssignedToPoint = positionCount > 1 && index == 1,
                    AssignmentScore = index,
                    CandidatePointId = $"P-{index + 1}",
                    AssignedPointId = positionCount > 1 ? "P-2" : string.Empty
                });
                positions.Add(position);
            }

            int liftedCount = coverage >= 1 ? 2 : 1;
            var result = new PlacementResult
            {
                Positions = positions,
                CoverageRatio = coverage,
                LiftedElementCount = liftedCount,
                TotalElementCount = 2,
                FitnessScore = coverage * 1000,
                Statistics = new LiftStatistics
                {
                    Liftable = liftedCount,
                    Overweight = 2 - liftedCount
                }
            };

            GAProfiler.StageLogs.Add(new GAProfiler.StageLogEntry
            {
                OptimizationRunId = settings.OptimizationRunId,
                StageRunId = settings.StageRunId,
                StageIndex = settings.StageIndex,
                MaxPositions = settings.MaxCranePositions,
                BestCoverageRatio = coverage,
                BestCoveragePercent = coverage * 100,
                BestLiftedCount = liftedCount,
                BestFitnessScore = result.FitnessScore,
                RawPositionCount = positionCount,
                FinalPositionCount = positionCount,
                UsedZoneIds = zone.ZoneId.ToString(),
                RuntimeMs = 12.5,
                Escalated = coverage < settings.MultiStageCoverageTarget,
                TerminationReason = "TestComplete"
            });

            return (service, input, result);
        }

        private static void AssertCsvShapeAndUniqueHeaders(string path)
        {
            string[] lines = File.ReadAllLines(path);
            Assert.NotEmpty(lines);
            string[] header = ParseCsvLine(lines[0]);
            Assert.Equal(header.Length, header.Distinct(StringComparer.OrdinalIgnoreCase).Count());

            foreach (string line in lines.Skip(1).Where(line => !string.IsNullOrWhiteSpace(line)))
            {
                Assert.Equal(header.Length, ParseCsvLine(line).Length);
            }
        }

        private static List<Dictionary<string, string>> ReadRows(string path)
        {
            string[] lines = File.ReadAllLines(path);
            string[] header = ParseCsvLine(lines[0]);
            return lines
                .Skip(1)
                .Where(line => !string.IsNullOrWhiteSpace(line))
                .Select(line => ParseCsvLine(line))
                .Select(values => header
                    .Select((name, index) => new { name, value = values[index] })
                    .ToDictionary(item => item.name, item => item.value))
                .ToList();
        }

        private static string[] ParseCsvLine(string line)
        {
            List<string> result = new();
            StringBuilder current = new();
            bool quoted = false;

            for (int index = 0; index < line.Length; index++)
            {
                char character = line[index];
                if (character == '"')
                {
                    if (quoted && index + 1 < line.Length && line[index + 1] == '"')
                    {
                        current.Append('"');
                        index++;
                    }
                    else
                    {
                        quoted = !quoted;
                    }
                }
                else if (character == ',' && !quoted)
                {
                    result.Add(current.ToString());
                    current.Clear();
                }
                else
                {
                    current.Append(character);
                }
            }

            result.Add(current.ToString());
            return result.ToArray();
        }

        private static double Parse(string value)
        {
            return double.Parse(value, NumberStyles.Float, CultureInfo.InvariantCulture);
        }

        private static string CreateTempDirectory()
        {
            string directory = Path.Combine(Path.GetTempPath(), $"benchmark-v2-{Guid.NewGuid():N}");
            Directory.CreateDirectory(directory);
            return directory;
        }

        private static void DeleteTempDirectory(string directory)
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, true);
            }
        }
    }
}
