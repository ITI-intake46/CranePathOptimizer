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
using Xunit;
using System.Text;

namespace CraneOptimizer.Application.Tests.GA
{
    public sealed class DiagnosticsSeparationTests
    {
        [Fact]
        public void DiagnosticsLevel_Off_WritesNoCsvFiles()
        {
            string tempDirectory = CreateTempDirectory();
            try
            {
                GAProfiler.Reset();
                var fixture = CreateFixture(tempDirectory, DiagnosticsLevel.Off);
                fixture.Service.GenerateDiagnostics(fixture.Input, fixture.Result);

                // Off level: absolutely no CSV files anywhere under tempDirectory
                string[] csvFiles = Directory.GetFiles(tempDirectory, "*.csv", SearchOption.AllDirectories);
                Assert.Empty(csvFiles);
            }
            finally
            {
                GAProfiler.Reset();
                DeleteTempDirectory(tempDirectory);
            }
        }

        [Fact]
        public void DiagnosticsLevel_Summary_WritesOnlySummaryFiles()
        {
            string tempDirectory = CreateTempDirectory();
            try
            {
                GAProfiler.Reset();
                var fixture = CreateFixture(tempDirectory, DiagnosticsLevel.Summary);
                fixture.Service.GenerateDiagnostics(fixture.Input, fixture.Result);

                // Per-run folder: <tempDir>/DiagSepTest/diag-sep-test/
                string runDir = Path.Combine(tempDirectory, "DiagSepTest", "diag-sep-test");

                string[] summaryPatterns =
                {
                    "ga_audit_run_summary.csv",
                    "ga_audit_settings_snapshot.csv",
                    "ga_audit_stage_summary.csv",
                    "ga_audit_positions.csv",
                    "ga_audit_element_assignments.csv",
                    "ga_audit_run_history.csv"
                };

                bool hasBreakdown = false;
                foreach (string file in Directory.GetFiles(runDir, "*.csv"))
                {
                    string name = Path.GetFileName(file);
                    if (name.StartsWith("ga_audit_failure_breakdown_"))
                    {
                        hasBreakdown = true;
                        continue;
                    }
                    bool matchesSummary = false;
                    foreach (string pattern in summaryPatterns)
                    {
                        if (name == pattern) { matchesSummary = true; break; }
                    }
                    Assert.True(matchesSummary, $"Unexpected file at Summary level: {name}");
                }
                Assert.True(hasBreakdown, "Missing ga_audit_failure_breakdown file at Summary level");
            }
            finally
            {
                GAProfiler.Reset();
                DeleteTempDirectory(tempDirectory);
            }
        }

        [Fact]
        public void DiagnosticsLevel_Summary_DoesNotWriteResearchFiles()
        {
            string tempDirectory = CreateTempDirectory();
            try
            {
                GAProfiler.Reset();
                var fixture = CreateFixture(tempDirectory, DiagnosticsLevel.Summary);
                fixture.Service.GenerateDiagnostics(fixture.Input, fixture.Result);

                string[] researchPatterns =
                {
                    "ga_audit_diagnostics_*",
                    "ga_audit_assignment_matrix_*",
                    "ga_audit_final_assignments_*",
                    "ga_audit_generation_summary.csv",
                    "ga_audit_reachability_hotspots.csv",
                    "ga_audit_project_features.csv",
                    "ga_audit_solution_quality_metrics.csv",
                    "ga_audit_zone_reachability_summary.csv"
                };

                // Per-run folder: <tempDir>/DiagSepTest/diag-sep-test/
                string runDirR = Path.Combine(tempDirectory, "DiagSepTest", "diag-sep-test");

                foreach (string pattern in researchPatterns)
                {
                    string searchPattern = pattern.Contains('*') ? pattern : pattern;
                    // Summary level should have no research files even in subdirectories
                    string[] matches = Directory.Exists(runDirR)
                        ? Directory.GetFiles(runDirR, searchPattern)
                        : Array.Empty<string>();
                    Assert.Empty(matches);
                }
            }
            finally
            {
                GAProfiler.Reset();
                DeleteTempDirectory(tempDirectory);
            }
        }

        [Fact]
        public void DiagnosticsLevel_Summary_ElementAssignmentsExposeSelectedLoadChartMetadata()
        {
            string tempDirectory = CreateTempDirectory();
            try
            {
                GAProfiler.Reset();
                var fixture = CreateFixture(tempDirectory, DiagnosticsLevel.Summary);
                fixture.Service.GenerateDiagnostics(fixture.Input, fixture.Result);

                // Per-run folder: <tempDir>/DiagSepTest/diag-sep-test/
                string runDirS = Path.Combine(tempDirectory, "DiagSepTest", "diag-sep-test");

                List<Dictionary<string, string>> assignments = ReadRows(
                    Path.Combine(runDirS, "ga_audit_element_assignments.csv"));
                Dictionary<string, string> row = assignments.Single(item => item["ElementId"] == "E1");

                Assert.Equal("3", row["SelectedVisualColumnIndex"]);
                Assert.Equal("39.1 rear", row["SelectedVisualColumnHeader"]);
                Assert.Equal("8", row["SelectedNumberOfLines"]);
                Assert.Equal("39.1 rear / 8 lines", row["SelectedOperatingConditionLabel"]);
                Assert.Equal("true", row["IsConservativeDuplicateFallback"]);
                Assert.Equal("2", row["DuplicateAlternativeCount"]);
                Assert.Equal("col 2 lines 8 capacity 31.5; col 3 lines 6 capacity 30.0", row["DuplicateAlternativeSummary"]);

                Dictionary<string, string> settings = ReadRows(
                    Path.Combine(runDirS, "ga_audit_settings_snapshot.csv")).Single();
                Assert.Equal("Summary", settings["DiagnosticsLevel"]);

                Dictionary<string, string> history = ReadRows(
                    Path.Combine(runDirS, "ga_audit_run_history.csv")).Single();
                Assert.Equal("N/A", history["DetailedDiagnosticsCsvPath"]);
                Assert.Empty(Directory.GetFiles(runDirS, "ga_audit_diagnostics_*.csv"));
            }
            finally
            {
                GAProfiler.Reset();
                DeleteTempDirectory(tempDirectory);
            }
        }

        [Fact]
        public void DiagnosticsLevel_Research_WritesAllFiles()
        {
            string tempDirectory = CreateTempDirectory();
            try
            {
                GAProfiler.Reset();
                var fixture = CreateFixture(tempDirectory, DiagnosticsLevel.Research);
                fixture.Service.GenerateDiagnostics(fixture.Input, fixture.Result);

                // Per-run folder: <tempDir>/DiagSepTest/diag-sep-test/
                string runDirRes = Path.Combine(tempDirectory, "DiagSepTest", "diag-sep-test");

                string[] requiredFiles =
                {
                    "ga_audit_run_summary.csv",
                    "ga_audit_settings_snapshot.csv",
                    "ga_audit_stage_summary.csv",
                    "ga_audit_positions.csv",
                    "ga_audit_element_assignments.csv",
                    "ga_audit_run_history.csv",
                    "ga_audit_generation_summary.csv",
                    "ga_audit_reachability_hotspots.csv",
                    "ga_audit_project_features.csv",
                    "ga_audit_solution_quality_metrics.csv",
                    "ga_audit_zone_reachability_summary.csv"
                };

                foreach (string fileName in requiredFiles)
                {
                    string path = Path.Combine(runDirRes, fileName);
                    Assert.True(File.Exists(path), $"Missing Research file: {fileName}");
                }

                bool hasDiagnostics = false;
                bool hasMatrix = false;
                bool hasFinalAssignments = false;
                bool hasBreakdown = false;
                foreach (string file in Directory.GetFiles(runDirRes, "*.csv"))
                {
                    string name = Path.GetFileName(file);
                    if (name.StartsWith("ga_audit_diagnostics_")) hasDiagnostics = true;
                    if (name.StartsWith("ga_audit_assignment_matrix_")) hasMatrix = true;
                    if (name.StartsWith("ga_audit_final_assignments_")) hasFinalAssignments = true;
                    if (name.StartsWith("ga_audit_failure_breakdown_")) hasBreakdown = true;
                }
                Assert.True(hasDiagnostics, "Missing ga_audit_diagnostics file at Research level");
                Assert.True(hasMatrix, "Missing ga_audit_assignment_matrix file at Research level");
                Assert.True(hasFinalAssignments, "Missing ga_audit_final_assignments file at Research level");
                Assert.True(hasBreakdown, "Missing ga_audit_failure_breakdown file at Research level");
            }
            finally
            {
                GAProfiler.Reset();
                DeleteTempDirectory(tempDirectory);
            }
        }

        private static (GACsvDiagnosticsService Service, PlacementInput Input, PlacementResult Result)
            CreateFixture(string outputDirectory, DiagnosticsLevel diagLevel)
        {
            GAProfiler.Enabled = true;
            GAProfiler.CurrentRunId = "diag-sep-test";

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
                Id = 1,
                Name = "TestCrane",
                VehicleLength = 10,
                VehicleWidth = 3,
                VehicleHeight = 3,
                OutriggerLength = 5,
                OutriggerWidth = 5
            };
            var craneSpecification = new CraneSpecification
            {
                Id = 101,
                CraneId = crane.Id,
                Crane = crane,
                MaxCapacity = 100,
                MinRadius = 2.5,
                MaxRadius = 50,
                MinBoomLength = 10,
                MaxBoomLength = 50,
                LoadChart = new List<TargetPoint>
                {
                    new() { Radius = 5, BoomLength = 10, Capacity = 50 },
                    new() { Radius = 25, BoomLength = 30, Capacity = 25 },
                    new() { Radius = 50, BoomLength = 50, Capacity = 10 }
                }
            };
            crane.Specifications.Add(craneSpecification);

            var zone = new GreenAreaZone
            {
                ZoneId = Guid.NewGuid(),
                Name = "TestZone",
                Type = ZoneType.Allowed,
                Area = 100,
                Vertices = new List<DomainPoint3D>
                {
                    new(-5, -5, 0),
                    new(5, -5, 0),
                    new(5, 5, 0),
                    new(-5, 5, 0)
                }
            };

            var elements = new List<PrecastElement>
            {
                new()
                {
                    ElementID = "E1",
                    ElementType = "IfcColumn",
                    ResolvedCategory = "Column",
                    ElementCOG = new DomainPoint3D(5, 0, 1),
                    ElementHeight = 3,
                    ElementVolume = 2,
                    ElementWT = 5,
                    WeightTons = 5,
                    WeightTrustLevel = IfcWeightTrustLevel.ExplicitQuantity,
                    IncludeInGa = true
                },
                new()
                {
                    ElementID = "E2",
                    ElementType = "IfcWall",
                    ResolvedCategory = "Wall",
                    ElementCOG = new DomainPoint3D(10, 0, 2),
                    ElementHeight = 4,
                    ElementVolume = 3,
                    ElementWT = 10,
                    WeightTons = 10,
                    WeightTrustLevel = IfcWeightTrustLevel.ExplicitProperty,
                    IncludeInGa = true
                }
            };

            var settings = new GASettings
            {
                OptimizationRunId = "diag-sep-test",
                StageRunId = "stage-1",
                StageIndex = 1,
                PopulationSize = 5,
                MaxGenerations = 1,
                MutationRate = 0.1,
                MutationSigma = 0.05,
                CrossoverRate = 0.8,
                MaxCranePositions = 1,
                GridStep = 5,
                CraneFootprintRadius = 1,
                AdditionalWt = 0.5,
                DeratingRatio = 0.9,
                SafetyFactor = 0.9,
                UseHungarianAssignment = true,
                EnableMultiStageDiscovery = false,
                DiagnosticsLevel = diagLevel
            };

            var input = new PlacementInput
            {
                Zones = new List<GreenAreaZone> { zone },
                ForbiddenZones = new List<GreenAreaZone>(),
                Elements = elements,
                Assignment = new Dictionary<int, List<string>>(),
                CraneSpec = craneSpecification,
                Settings = settings,
                ProjectName = "DiagSepTest",
                IfcFileName = "test.ifc",
                TotalElementsCount = 2,
                DiagnosticsReportsDirectory = outputDirectory
            };

            var positions = new List<CranePositionResult>
            {
                new()
                {
                    X = 0,
                    Y = 0,
                    Z = 0
                }
            };

            positions[0].ElementResults.Add(new ElementLiftResult
            {
                ElementId = "E1",
                ElementType = "IfcColumn",
                Weight = 5,
                Status = LiftStatus.Liftable,
                Radius = 5,
                BoomLength = 10,
                RequiredCapacity = 5.5,
                AvailableCapacity = 50,
                SafetyMargin = 44.5,
                ReachStatus = ReachStatus.Reachable,
                ChartStatus = ChartStatus.ListedCapacityFound,
                IsExactMatch = true,
                IsSteppedRadius = false,
                IsBlankCell = false,
                IsZeroCell = false,
                LookupMode = "Exact",
                RequiredRadius = 5,
                SelectedChartRadius = 5,
                SelectedChartBoomLength = 10,
                SelectedVisualColumnIndex = 3,
                SelectedVisualColumnHeader = "39.1 rear",
                SelectedNumberOfLines = 8,
                SelectedOperatingConditionLabel = "39.1 rear / 8 lines",
                IsConservativeDuplicateFallback = true,
                DuplicateAlternativeCount = 2,
                DuplicateAlternativeSummary = "col 2 lines 8 capacity 31.5; col 3 lines 6 capacity 30.0",
                CraneBaseX = 0,
                CraneBaseY = 0,
                CraneBaseZ = 0,
                SlewCenterX = 0,
                SlewCenterY = 0,
                SlewCenterZ = 0,
                BoomHingeX = 0,
                BoomHingeY = 0,
                BoomHingeZ = 1,
                LoadPointX = 0,
                LoadPointY = 0,
                LoadPointZ = 1,
                RadiusOriginType = "CenterOfSlew",
                BoomOriginType = "BoomHinge",
                CandidateBoomsChecked = "10",
                RejectedCandidates = "",
                CandidatePointId = "CP1",
                CandidatePointIndex = 0,
                IsFeasibleFromPoint = true,
                IsFinalAssignedToPoint = true,
                AssignedPointId = "CP1",
                AssignedPointIndex = 0,
                AssignmentReason = "BestScore",
                AssignmentScore = 1.0,
                FinalOwnerPointId = "CP1",
                IsFinalAssigned = true,
                BestFailedPointId = "",
                BestFailureReason = "",
                DuplicateAssignmentResolved = false,
                FeasiblePointIds = "CP1",
                HasBestFailedCandidate = false
            });

            positions[0].ElementResults.Add(new ElementLiftResult
            {
                ElementId = "E2",
                ElementType = "IfcWall",
                Weight = 10,
                Status = LiftStatus.Liftable,
                Radius = 10,
                BoomLength = 15,
                RequiredCapacity = 10.5,
                AvailableCapacity = 40,
                SafetyMargin = 29.5,
                ReachStatus = ReachStatus.Reachable,
                ChartStatus = ChartStatus.ListedCapacityFound,
                IsExactMatch = true,
                IsSteppedRadius = false,
                IsBlankCell = false,
                IsZeroCell = false,
                LookupMode = "Exact",
                RequiredRadius = 10,
                SelectedChartRadius = 10,
                SelectedChartBoomLength = 15,
                CraneBaseX = 0,
                CraneBaseY = 0,
                CraneBaseZ = 0,
                SlewCenterX = 0,
                SlewCenterY = 0,
                SlewCenterZ = 0,
                BoomHingeX = 0,
                BoomHingeY = 0,
                BoomHingeZ = 1,
                LoadPointX = 5,
                LoadPointY = 0,
                LoadPointZ = 2,
                RadiusOriginType = "CenterOfSlew",
                BoomOriginType = "BoomHinge",
                CandidateBoomsChecked = "15",
                RejectedCandidates = "",
                CandidatePointId = "CP1",
                CandidatePointIndex = 0,
                IsFeasibleFromPoint = true,
                IsFinalAssignedToPoint = true,
                AssignedPointId = "CP1",
                AssignedPointIndex = 0,
                AssignmentReason = "BestScore",
                AssignmentScore = 0.9,
                FinalOwnerPointId = "CP1",
                IsFinalAssigned = true,
                BestFailedPointId = "",
                BestFailureReason = "",
                DuplicateAssignmentResolved = false,
                FeasiblePointIds = "CP1",
                HasBestFailedCandidate = false
            });

            var result = new PlacementResult
            {
                Positions = positions,
                LiftedElementCount = 2,
                CoverageRatio = 1.0,
                FitnessScore = 0.95,
                Statistics = new LiftStatistics()
            };

            return (service, input, result);
        }

        private static List<Dictionary<string, string>> ReadRows(string path)
        {
            string[] lines = File.ReadAllLines(path);
            Assert.True(lines.Length >= 2, $"Expected at least one data row in {path}.");

            string[] header = ParseCsvLine(lines[0]);
            var rows = new List<Dictionary<string, string>>();

            foreach (string line in lines.Skip(1).Where(value => !string.IsNullOrWhiteSpace(value)))
            {
                string[] values = ParseCsvLine(line);
                Assert.Equal(header.Length, values.Length);

                var row = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                for (int index = 0; index < header.Length; index++)
                {
                    row[header[index]] = values[index];
                }

                rows.Add(row);
            }

            return rows;
        }

        private static string[] ParseCsvLine(string line)
        {
            var values = new List<string>();
            var current = new StringBuilder();
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
                    values.Add(current.ToString());
                    current.Clear();
                }
                else
                {
                    current.Append(character);
                }
            }

            values.Add(current.ToString());
            return values.ToArray();
        }

        private static string CreateTempDirectory()
        {
            string directory = Path.Combine(Path.GetTempPath(), $"diag-sep-{Guid.NewGuid():N}");
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
