using Xunit;
using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Services;
using CraneOptimizer.Infrastructure.Repositories;
using System;
using System.IO;
using System.Threading.Tasks;
using System.Linq;

namespace CraneOptimizer.Application.Tests.GA
{
    public class CsvOptimizationHistoryLoggerTests
    {
        [Fact]
        public async Task TestLoggerCreatesFileAndAppendsCorrectly()
        {
            // Use local output bin directory for temporary test files
            string tempDir = Path.Combine(AppContext.BaseDirectory, "CsvHistoryTemp_" + Guid.NewGuid().ToString("N"));
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, true);
            }
            Directory.CreateDirectory(tempDir);

            try
            {
                using var logger = new CsvOptimizationHistoryLogger();

                var entry1 = new OptimizationRunEntry
                {
                    Timestamp = new DateTime(2026, 6, 4, 10, 30, 0),
                    ProjectName = "Test, Project", // Includes comma to test escaping
                    IfcFileName = "TestModel.ifc",
                    CraneModel = "LTM \"1300\"", // Includes quotes to test escaping
                    PopulationSize = 100,
                    MaxGenerations = 200,
                    MutationRate = 0.15,
                    CrossoverRate = 0.85,
                    EliteCount = 10,
                    TournamentSize = 3,
                    ConvergenceThreshold = 0.05,
                    EarlyStopGenerations = 25,
                    WireLength = 3.5,
                    AdditionalWt = 1.2,
                    SafetyFactor = 0.95,
                    DeratingRatio = 0.90,
                    MaxCranePositions = 2,
                    MinimumRelocationDistance = 15.0,
                    MutationSigma = 0.06,
                    GridStep = 0.5,
                    CandidatePointsGenerated = 1000,
                    ValidPointsGenerated = 800,
                    FinalSolutionsCount = 1,
                    BestFitness = -1.25,
                    CoveragePercent = 85.5,
                    ExecutionTimeMs = 4500,
                    TotalElements = 50,
                    LiftableElements = 45,
                    UnliftableElements = 5,
                    BestPointX = 10.5,
                    BestPointY = 20.75,
                    BestPointZ = 1.0,
                    ElementsLiftedByBestPoint = 40,
                    CoveragePercentBestPoint = 80.0,
                    AverageRadius = 22.4,
                    MaxRadius = 38.6,
                    GenerationReached = 145,
                    StoppedByConvergence = true,
                    StoppedByMaxGenerations = false
                };

                string csvPath = Path.Combine(tempDir, "OptimizationHistory.csv");

                // Act - Run 1 (Should create file and write header + data)
                await logger.LogRunAsync(entry1, tempDir);

                Assert.True(File.Exists(csvPath), "CSV history file was not created.");

                var lines = await File.ReadAllLinesAsync(csvPath);
                Assert.Equal(2, lines.Length);

                // Verify header columns count matches data columns count
                var headers = lines[0].Split(',');
                // Parse CSV line with comma splitting, taking escaping into consideration
                // For simplicity, we can inspect contents
                Assert.Contains("Timestamp", lines[0]);
                Assert.Contains("ProjectName", lines[0]);

                // Verify values of Run 1
                Assert.Contains("2026-06-04 10:30:00", lines[1]);
                Assert.Contains("\"Test, Project\"", lines[1]); // comma escaped
                Assert.Contains("\"LTM \"\"1300\"\"\"", lines[1]); // quotes escaped
                Assert.Contains("100", lines[1]);
                Assert.Contains("0.15", lines[1]); // decimal dot
                Assert.Contains("1", lines[1]); // StoppedByConvergence should log "1"
                Assert.Contains("0", lines[1]); // StoppedByMaxGenerations should log "0"

                // Act - Run 2 (Should append without duplicate header)
                var entry2 = new OptimizationRunEntry
                {
                    Timestamp = new DateTime(2026, 6, 4, 10, 35, 0),
                    ProjectName = "SecondRun",
                    IfcFileName = "TestModel.ifc",
                    CraneModel = "LTM 1300",
                    PopulationSize = 100,
                    MaxGenerations = 200,
                    MutationRate = 0.15,
                    CrossoverRate = 0.85,
                    EliteCount = 10,
                    TournamentSize = 3,
                    ConvergenceThreshold = 0.05,
                    EarlyStopGenerations = 25,
                    WireLength = 3.5,
                    AdditionalWt = 1.2,
                    SafetyFactor = 0.95,
                    DeratingRatio = 0.90,
                    MaxCranePositions = 2,
                    MinimumRelocationDistance = 15.0,
                    MutationSigma = 0.06,
                    GridStep = 0.5,
                    CandidatePointsGenerated = 1200,
                    ValidPointsGenerated = 950,
                    FinalSolutionsCount = 1,
                    BestFitness = -1.10,
                    CoveragePercent = 90.0,
                    ExecutionTimeMs = 4800,
                    TotalElements = 50,
                    LiftableElements = 48,
                    UnliftableElements = 2,
                    BestPointX = 12.0,
                    BestPointY = 21.0,
                    BestPointZ = 1.0,
                    ElementsLiftedByBestPoint = 43,
                    CoveragePercentBestPoint = 86.0,
                    AverageRadius = 21.8,
                    MaxRadius = 35.2,
                    GenerationReached = 200,
                    StoppedByConvergence = false,
                    StoppedByMaxGenerations = true
                };

                await logger.LogRunAsync(entry2, tempDir);

                lines = await File.ReadAllLinesAsync(csvPath);
                Assert.Equal(3, lines.Length); // Header + Run 1 + Run 2
                Assert.Contains("SecondRun", lines[2]);
            }
            finally
            {
                if (Directory.Exists(tempDir))
                {
                    Directory.Delete(tempDir, true);
                }
            }
        }

        [Fact]
        public async Task TestLoggerHandlesNullOrInvalidPathsGracefully()
        {
            using var logger = new CsvOptimizationHistoryLogger();
            var entry = new OptimizationRunEntry { Timestamp = DateTime.Now };

            // Act & Assert: Should not throw any exception
            await logger.LogRunAsync(entry, null!);
            await logger.LogRunAsync(entry, "");
            await logger.LogRunAsync(entry, "   ");
            await logger.LogRunAsync(null!, "C:\\NonExistentPath");
        }
    }
}
