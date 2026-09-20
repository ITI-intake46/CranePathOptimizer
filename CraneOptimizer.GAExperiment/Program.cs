using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using CraneOptimizer.Domain.Entities;
using CraneOptimizer.Domain.Schemas;
using CraneOptimizer.Domain.Schemas.Enums;
using CraneOptimizer.Domain.Interfaces;
using CraneOptimizer.Domain.Common;
using CraneOptimizer.Domain.Diagnostics;
using CraneOptimizer.Domain.CreatedProject;
using CraneOptimizer.Domain.CreatedProject.IfcFiles;
using CraneOptimizer.Domain.CreatedProject.ProjectState;
using CraneOptimizer.Domain.Units;
using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Interfaces;
using CraneOptimizer.Application.Optimization.GeneticAlgorithms.DTOs;
using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Settings;
using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Models;
using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Engines.GeneticOptimization;
using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Engines.Coverage;
using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Engines.PlacementOrchestration;
using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Operators;
using CraneOptimizer.Infrastructure.Contexts;
using CraneOptimizer.Infrastructure.Contexts.CreatedProject;
using CraneOptimizer.Infrastructure.Repositories;
using CraneOptimizer.Application.Optimization.Utilities;
using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Services;
using CraneOptimizer.Application.Services;
using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Snapshots;
using CraneOptimizer.Application.Reports.Builders;
using CraneOptimizer.Application.Reports.Inputs;
using CraneOptimizer.Application.Reports.Services;
using CraneOptimizer.Infrastructure.Optimization.GeneticAlgorithms.Snapshots;
using CraneOptimizer.Infrastructure.Reports.Csv;
using CraneOptimizer.Infrastructure.Reports.Pdf;

namespace CraneOptimizer.GAExperiment
{
    public class Program
    {
        private static async Task<int> Main(string[] args)
        {
            Console.OutputEncoding = Encoding.UTF8;
            
            // Set static holders
            ZoneGeometryServiceHolder.Instance = new CraneOptimizer.Domain.Services.ZoneGeometryService();

            // Setup cancellation token source
            var cts = new CancellationTokenSource();
            Console.CancelKeyPress += (sender, eventArgs) =>
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("\n[Ctrl+C] Cancellation requested. Stopping after the current run finishes...");
                Console.ResetColor();
                eventArgs.Cancel = true;
                cts.Cancel();
            };

            string configPath = args.Length > 0
                ? Path.GetFullPath(args[0])
                : Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "GAExperimentConfig.json");

            // Generate default config if missing
            if (!File.Exists(configPath))
            {
                Console.WriteLine($"Config file not found. Generating default: {configPath}");
                GenerateDefaultConfig(configPath);
                Console.WriteLine("Please edit the generated configuration file and run again.");
                return 0;
            }

            // Load config
            GAExperimentConfig config;
            try
            {
                var json = await File.ReadAllTextAsync(configPath, Encoding.UTF8, cts.Token);
                config = JsonSerializer.Deserialize<GAExperimentConfig>(json, new JsonSerializerOptions(JsonSerializerDefaults.Web))
                    ?? throw new InvalidOperationException("Failed to deserialize configuration.");
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"Error reading config file: {ex.Message}");
                Console.ResetColor();
                return 1;
            }

            // Resolve paths relative to configuration directory if needed
            string configDir = Path.GetDirectoryName(Path.GetFullPath(configPath)) ?? AppDomain.CurrentDomain.BaseDirectory;
            
            string rawCatalogDbPath = ResolveCatalogDbPath(config.CatalogDatabasePath, configDir);
            if (!File.Exists(rawCatalogDbPath))
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"Error: Catalog database file '{rawCatalogDbPath}' does not exist.");
                Console.ResetColor();
                return 1;
            }

            // Resolve and check project files
            var resolvedProjectFiles = new List<string>();
            foreach (var projFile in config.ProjectFiles)
            {
                string fullPath = Path.IsPathRooted(projFile) ? projFile : Path.Combine(configDir, projFile);
                if (!File.Exists(fullPath))
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"Error: Project file '{fullPath}' does not exist.");
                    Console.ResetColor();
                    return 1;
                }
                resolvedProjectFiles.Add(Path.GetFullPath(fullPath));
            }

            if (resolvedProjectFiles.Count == 0)
            {
                Console.WriteLine("No project files configured. Exiting.");
                return 0;
            }

            string resultsCsvPath = Path.IsPathRooted(config.ExperimentResultsCsvPath) 
                ? config.ExperimentResultsCsvPath 
                : Path.Combine(configDir, config.ExperimentResultsCsvPath);

            // Setup DI container
            var services = new ServiceCollection();
            services.AddDbContext<CraneDbContext>(options => options.UseSqlite($"Data Source={rawCatalogDbPath}"));
            services.AddDbContextFactory<CraneDbContext>(options => options.UseSqlite($"Data Source={rawCatalogDbPath}"));

            // Core registries
            services.AddScoped<ICraneRepository, CraneRepository>();
            services.AddSingleton<IUnitSettingsService, UnitSettingsService>();
            services.AddSingleton<IProjectCoordinateTransformService, ProjectCoordinateTransformService>();

            // GA Registrations
            services.AddScoped<IGeneticAlgorithmEngine, GAEngine>();
            services.AddScoped<ICraneConfigurationEngine, CraneOptimizer.Application.Optimization.GeneticAlgorithms.Engines.CraneConfiguration.CraneConfigurationEngine>();
            services.AddScoped<ILiftingCalculationEngine, CraneOptimizer.Application.Optimization.GeneticAlgorithms.Engines.LiftingCalculation.LiftingCalculationEngine>();
            services.AddScoped<ILoadChartCapacityResolver, LoadChartCapacityResolver>();
            services.AddScoped<IConstraintEngine, CraneOptimizer.Application.Optimization.GeneticAlgorithms.Engines.Constraints.ConstraintEngine>();
            services.AddScoped<ICoverageEngine, CoverageEngine>();

            services.AddScoped<IGAInitializer, GAInitializer>();
            services.AddScoped<IReachabilityMapService, ReachabilityMapService>();
            services.AddScoped<ElementAssignmentService>();
            services.AddScoped<HungarianAssignmentService>();
            services.AddScoped<IElementAssignmentService, RoutingAssignmentService>();
            services.AddScoped<IFitnessEvaluator, FitnessEvaluator>();
            services.AddScoped<IGACsvDiagnosticsService, GACsvDiagnosticsService>();
            services.AddScoped<ISelectionStrategy, TournamentSelection>();
            services.AddScoped<ICrossoverOperator, CrossoverOperator>();
            services.AddScoped<IMutationOperator, MutationOperator>();
            services.AddScoped<IReplacementStrategy, ReplacementStrategy>();

            services.AddSingleton<IOptimizationHistoryLogger, CsvOptimizationHistoryLogger>();
            services.AddSingleton<IExperimentResultWriter, ExperimentResultWriter>();

            services.AddScoped<IOptimizationInputBuilder, CraneOptimizer.Application.Optimization.GeneticAlgorithms.Engines.OptimizationScope.OptimizationInputBuilder>();
            services.AddScoped<ICranePlacementEngine, CranePlacementEngine>();
            services.AddScoped<IOptimizationScopeEngine, CraneOptimizer.Application.Optimization.GeneticAlgorithms.Engines.OptimizationScope.OptimizationScopeEngine>();

            var serviceProvider = services.BuildServiceProvider();

            // Load cranes from database
            Dictionary<string, Crane> craneMap;
            using (var scope = serviceProvider.CreateScope())
            {
                var craneRepo = scope.ServiceProvider.GetRequiredService<ICraneRepository>();
                var availableCranes = await craneRepo.GetAllWithSpecsAsync();
                craneMap = availableCranes.ToDictionary(c => c.Name, c => c, StringComparer.OrdinalIgnoreCase);
            }

            // Verify cranes from config
            var validCranes = new List<string>();
            foreach (var craneName in config.Cranes)
            {
                if (!craneMap.TryGetValue(craneName, out _))
                {
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.WriteLine($"[Warning] Crane model '{craneName}' not found in database. Skipping.");
                    Console.ResetColor();
                    continue;
                }
                validCranes.Add(craneName);
            }

            if (validCranes.Count == 0)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("Error: No valid crane models found in the database. Exiting.");
                Console.ResetColor();
                return 1;
            }

            // Generate parameter matrix combinations
            List<GASettings> settingsCombinations;
            List<int> seedsToRun = new List<int>();

            if (config.BatchCalibrationMode)
            {
                seedsToRun = new List<int> { 1, 42, 99 };
                settingsCombinations = new List<GASettings>();

                // Helper to build settings and override defaults
                GASettings BuildBaseSettings(string setId, string setName, int pop, int gen, double sigma, int maxPositions, double relocWeight)
                {
                    return new GASettings
                    {
                        BatchModeEnabled = true,
                        ExperimentName = config.ExperimentName ?? "auditmodel_STC800_mode_calibration",
                        ParameterSetId = setId,
                        ParameterSetName = setName,
                        PopulationSize = pop,
                        MaxGenerations = gen,
                        MutationSigma = sigma,
                        MaxCranePositions = maxPositions,
                        RelocationWeight = relocWeight,
                        // Defaults
                        MutationRate = 0.1,
                        CrossoverRate = 0.8,
                        WireLength = 2.0,
                        AdditionalWt = 0.5,
                        SafetyFactor = 0.9,
                        DeratingRatio = 0.9,
                        EnableProfiling = true,
                        EnableConcurrentOptimization = true,
                        UseHungarianAssignment = true,
                        EnableReachabilitySeeding = true,
                        DiagnosticsLevel = DiagnosticsLevel.Research
                    };
                }

                // Balanced Candidates
                settingsCombinations.Add(BuildBaseSettings("B01", "Balanced_LowPop", 30, 40, 0.005, 2, 5.0));
                settingsCombinations.Add(BuildBaseSettings("B02", "Balanced_MedPop", 60, 80, 0.005, 2, 5.0));
                settingsCombinations.Add(BuildBaseSettings("B03", "Balanced_HighPos", 60, 80, 0.02, 3, 5.0));

                // Minimum Relocation Candidates
                settingsCombinations.Add(BuildBaseSettings("R01", "MinReloc_SinglePos", 30, 40, 0.005, 1, 25.0));
                settingsCombinations.Add(BuildBaseSettings("R02", "MinReloc_HighWeight", 60, 80, 0.005, 2, 25.0));
                settingsCombinations.Add(BuildBaseSettings("R03", "MinReloc_VeryHighWeight", 60, 80, 0.02, 2, 100.0));

                // Crane-Friendly Candidates
                var c01 = BuildBaseSettings("C01", "Friendly_LowPenalties", 60, 80, 0.005, 2, 5.0);
                c01.RadiusPenaltyWeight = 1.0;
                c01.HeightPenaltyWeight = 2.0;
                c01.OverweightPenaltyWeight = 5.0;
                settingsCombinations.Add(c01);

                var c02 = BuildBaseSettings("C02", "Friendly_MedPenalties", 60, 80, 0.005, 3, 5.0);
                c02.RadiusPenaltyWeight = 3.0;
                c02.HeightPenaltyWeight = 5.0;
                c02.OverweightPenaltyWeight = 10.0;
                settingsCombinations.Add(c02);

                var c03 = BuildBaseSettings("C03", "Friendly_HighPenalties", 80, 120, 0.02, 3, 5.0);
                c03.RadiusPenaltyWeight = 10.0;
                c03.HeightPenaltyWeight = 15.0;
                c03.OverweightPenaltyWeight = 30.0;
                settingsCombinations.Add(c03);
            }
            else
            {
                settingsCombinations = GenerateMatrixCombinations(
                    config.Matrix,
                    config.UseHungarianAssignment,
                    config.EnableReachabilitySeeding).ToList();

                if (config.SeedMode.Equals("Fixed", StringComparison.OrdinalIgnoreCase))
                {
                    seedsToRun.Add(config.Seeds.Count > 0 ? config.Seeds[0] : 42);
                }
                else if (config.SeedMode.Equals("Random", StringComparison.OrdinalIgnoreCase))
                {
                    int count = config.RandomSeedCount > 0 ? config.RandomSeedCount : 5;
                    if (config.Seeds.Count > 0)
                    {
                        seedsToRun.AddRange(config.Seeds);
                    }
                    else
                    {
                        var rand = new Random();
                        for (int i = 0; i < count; i++)
                        {
                            seedsToRun.Add(rand.Next(1, 1000000));
                        }
                    }
                }
                else
                {
                    if (config.Seeds.Count > 0)
                    {
                        seedsToRun.AddRange(config.Seeds);
                    }
                    else
                    {
                        seedsToRun.Add(42);
                    }
                }
            }

            if (settingsCombinations.Count == 0)
            {
                Console.WriteLine("No parameter matrix combinations configured. Exiting.");
                return 0;
            }

            // Estimations & safety warnings
            int totalRuns = resolvedProjectFiles.Count * validCranes.Count * settingsCombinations.Count * seedsToRun.Count;
            long totalEvaluations = 0;
            foreach (var settings in settingsCombinations)
            {
                totalEvaluations += settings.PopulationSize * settings.MaxGenerations;
            }
            totalEvaluations *= resolvedProjectFiles.Count * validCranes.Count * seedsToRun.Count;

            Console.WriteLine("==================================================");
            Console.WriteLine("      GA Headless Parameter Sweep Validation      ");
            Console.WriteLine("==================================================");
            Console.WriteLine($"Database Path:   {rawCatalogDbPath}");
            Console.WriteLine($"Projects Count:  {resolvedProjectFiles.Count}");
            Console.WriteLine($"Cranes Count:    {validCranes.Count} ({string.Join(", ", validCranes)})");
            Console.WriteLine($"Combinations:    {settingsCombinations.Count}");
            Console.WriteLine($"Seeds Count:     {seedsToRun.Count} ({config.SeedMode} Mode: {string.Join(", ", seedsToRun)})");
            Console.WriteLine($"Estimated Runs:  {totalRuns}");
            Console.WriteLine($"Max GA Evaluations: {totalEvaluations:N0}");
            Console.WriteLine($"Output CSV:      {resultsCsvPath}");
            Console.WriteLine("==================================================");

            if (totalRuns > config.MaxRuns)
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine($"WARNING: Estimated run count ({totalRuns}) exceeds configured MaxRuns ({config.MaxRuns}).");
                Console.ResetColor();
                Console.Write("Do you want to proceed? (y/N): ");
                var choice = Console.ReadLine();
                if (choice?.Trim().ToLower() != "y")
                {
                    Console.WriteLine("Experiment loop aborted by user.");
                    return 0;
                }
            }

            var resultWriter = serviceProvider.GetRequiredService<IExperimentResultWriter>();

            // Load existing run IDs if resuming is enabled
            var completedRunIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (config.ResumeEnabled)
            {
                completedRunIds = await resultWriter.ReadExistingRunIdsAsync(resultsCsvPath);
                if (completedRunIds.Count > 0)
                {
                    Console.WriteLine($"Loaded {completedRunIds.Count} completed runs. Resume is active.");
                }
            }

            var allRunResults = new List<ExperimentRunResult>();
            int runIndex = 0;
            int skippedCount = 0;

            // Temp directory for GA CSV diagnostics output
            string tempDiagnosticsDir = string.IsNullOrWhiteSpace(config.DiagnosticsDirectoryPath)
                ? Path.Combine(configDir, "Diagnostics")
                : Path.GetFullPath(Path.IsPathRooted(config.DiagnosticsDirectoryPath)
                    ? config.DiagnosticsDirectoryPath
                    : Path.Combine(configDir, config.DiagnosticsDirectoryPath));
            Directory.CreateDirectory(tempDiagnosticsDir);

            string batchId = Guid.NewGuid().ToString();
            string timestampStr = DateTime.Now.ToString("yyyyMMdd_HHmmss");

            foreach (var projFile in resolvedProjectFiles)
            {
                if (cts.IsCancellationRequested) break;

                List<PrecastElement> elements;
                List<GreenAreaZone> allowedZones;
                List<GreenAreaZone> forbiddenZones;
                List<PrecastElement> scopeElements;
                string projectName;
                string projectId;
                string ifcFileName;
                int rawElementCount;

                try
                {
                    // Lifetime safety: Open and dispose DbContext immediately
                    (elements, scopeElements, allowedZones, forbiddenZones, projectName, projectId, ifcFileName, rawElementCount) = LoadProjectData(projFile);
                }
                catch (Exception ex)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"Failed to load project database '{projFile}': {ex.Message}");
                    Console.ResetColor();
                    continue;
                }

                string reportsFolder = tempDiagnosticsDir;
                if (config.BatchCalibrationMode)
                {
                    reportsFolder = Path.Combine(tempDiagnosticsDir, projectName, $"mode_calibration_{timestampStr}");
                    Directory.CreateDirectory(reportsFolder);
                }

                foreach (var craneName in validCranes)
                {
                    if (cts.IsCancellationRequested) break;

                    var crane = craneMap[craneName];

                    foreach (var baseSettings in settingsCombinations)
                    {
                        if (cts.IsCancellationRequested) break;

                        foreach (var seed in seedsToRun)
                        {
                            runIndex++;

                            if (cts.IsCancellationRequested) break;

                            // Setup settings copy
                            var settings = CloneSettings(baseSettings);
                            settings.RandomSeed = seed;
                            settings.ProfilingLogDirectory = Path.Combine(configDir, "Logs");

                            if (config.BatchCalibrationMode)
                            {
                                settings.BatchModeEnabled = true;
                                settings.ExperimentBatchId = batchId;
                                settings.ExperimentName = config.ExperimentName ?? "auditmodel_STC800_mode_calibration";
                            }

                            // Calculate deterministic RunId
                            string runId = GenerateRunId(projFile, craneName, seed, settings);
                            settings.OptimizationRunId = runId;
                            settings.RunIndex = runIndex;

                            if (completedRunIds.Contains(runId))
                            {
                                skippedCount++;
                                continue;
                            }

                            // Run optimization
                            using (var scope = serviceProvider.CreateScope())
                            {
                                var placementEngine = scope.ServiceProvider.GetRequiredService<ICranePlacementEngine>();
                                var inputBuilder = scope.ServiceProvider.GetRequiredService<IOptimizationInputBuilder>();

                                // Build input
                                PlacementInput input;
                                try
                                {
                                    input = inputBuilder.Build(settings, crane, elements, allowedZones.Concat(forbiddenZones).ToList());
                                    input.ProjectName = projectName;
                                    input.ProjectId = projectId;
                                    input.ProjectFolderPath = Path.GetDirectoryName(projFile);
                                    input.IfcFileName = ifcFileName;
                                    input.RawElementCount = rawElementCount;
                                    input.OptimizationScopeElementCount = scopeElements.Count;
                                    input.FeatureScopeElements = scopeElements;
                                    input.TotalElementsCount = rawElementCount;
                                    input.DiagnosticsReportsDirectory = reportsFolder;
                                }
                                catch (Exception ex)
                                {
                                    Console.ForegroundColor = ConsoleColor.Red;
                                    Console.WriteLine($"[Run {runIndex}/{totalRuns}] Error building input: {ex.Message}");
                                    Console.ResetColor();
                                    continue;
                                }

                                var runStartedAtUtc = DateTimeOffset.UtcNow;
                                var sw = Stopwatch.StartNew();
                                PlacementResult result;
                                try
                                {
                                    result = placementEngine.RunOptimization(input, CancellationToken.None); // CancellationToken.None ensures run completes fully
                                }
                                catch (Exception ex)
                                {
                                    Console.ForegroundColor = ConsoleColor.Red;
                                    Console.WriteLine($"[Run {runIndex}/{totalRuns}] GA Optimization failed: {ex.Message}");
                                    Console.ResetColor();
                                    continue;
                                }
                                sw.Stop();
                                var runCompletedAtUtc = DateTimeOffset.UtcNow;

                                // Calculate KPIs
                                int uniqueCraneSetups = result.Positions.Count;
                                var allLifted = result.Positions
                                    .SelectMany(p => p.ElementResults)
                                    .Where(e => e.Status == LiftStatus.Liftable)
                                    .ToList();

                                double maxRadius = allLifted.Any() ? allLifted.Max(e => e.Radius) : 0.0;
                                double avgRadius = allLifted.Any() ? allLifted.Average(e => e.Radius) : 0.0;
                                double relocationDistance = 0.0;
                                for (int i = 0; i < result.Positions.Count - 1; i++)
                                {
                                    var p1 = result.Positions[i];
                                    var p2 = result.Positions[i + 1];
                                    double dx = p1.X - p2.X;
                                    double dy = p1.Y - p2.Y;
                                    double dz = p1.Z - p2.Z;
                                    relocationDistance += Math.Sqrt(dx * dx + dy * dy + dz * dz);
                                }

                                int totalTargets = input.Elements.Count;
                                int liftable = result.LiftedElementCount;
                                int failures = totalTargets - liftable;

                                double coveragePct = result.CoverageRatio * 100.0;

                                // Build CSV entry
                                var runResult = new ExperimentRunResult
                                {
                                    RunId = runId,
                                    BenchmarkVersion = config.BenchmarkVersion,
                                    Timestamp = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
                                    ProjectFile = (Path.GetFileName(Path.GetDirectoryName(projFile)) + "/" + Path.GetFileName(projFile)).Replace('\\', '/'),
                                    ProjectScale = ExperimentRunResult.ClassifyProjectScale(totalTargets),
                                    ElementCount = totalTargets,
                                    CraneModel = craneName,
                                    
                                    PopulationSize = settings.PopulationSize,
                                    MaxGenerations = settings.MaxGenerations,
                                    MutationRate = settings.MutationRate,
                                    MutationSigma = settings.MutationSigma,
                                    CrossoverRate = settings.CrossoverRate,
                                    MaxCranePositions = settings.MaxCranePositions,
                                    MinimumRelocationDistance = settings.MinimumRelocationDistance,
                                    GridStep = settings.GridStep,
                                    RandomSeed = seed,

                                    Coverage = result.CoverageRatio,
                                    CoveredElements = liftable,
                                    TotalElements = totalTargets,
                                    Fitness = result.FitnessScore,
                                    ExecutionTimeMs = sw.ElapsedMilliseconds,
                                    GenerationReached = GAProfiler.ActualGenerationLoopCount,

                                    UniqueCranePositions = uniqueCraneSetups,
                                    AverageRadius = avgRadius,
                                    MaximumRadius = maxRadius,
                                    RelocationDistance = relocationDistance,
                                    LiftFailures = failures,

                                    OutOfRadius = result.Statistics.OutOfRadius,
                                    Overweight = result.Statistics.Overweight,
                                    HeightFail = result.Statistics.HeightFail,
                                    SpecialCase = result.Statistics.SpecialCase,
                                    NotCharted = result.Statistics.NotCharted
                                };
                                runResult.RefreshDerivedFields();

                                allRunResults.Add(runResult);

                                // Append immediately to CSV
                                await resultWriter.WriteResultAsync(runResult, resultsCsvPath);

                                if (config.WriteAcceptanceArtifacts)
                                {
                                    await PersistAcceptanceArtifactsAsync(
                                        config,
                                        configDir,
                                        projFile,
                                        projectName,
                                        projectId,
                                        ifcFileName,
                                        input,
                                        result,
                                        settings,
                                        runId,
                                        runStartedAtUtc,
                                        runCompletedAtUtc,
                                        sw.ElapsedMilliseconds,
                                        cts.Token);
                                }

                                // Console print per run
                                Console.WriteLine($"[{runIndex}/{totalRuns}] {Path.GetFileName(projFile)} | {craneName} | Pop={settings.PopulationSize}, Gen={settings.MaxGenerations}, Mut={settings.MutationRate:F2}, Seed={seed} ... Coverage={coveragePct:F1}% Time={sw.ElapsedMilliseconds}ms ✓");
                            }
                        }
                    }
                }
            }

            if (skippedCount > 0)
            {
                Console.WriteLine($"Skipped {skippedCount} already completed runs.");
            }

            // Post-processing: summary report generation
            if (allRunResults.Count > 0 || completedRunIds.Count > 0)
            {
                Console.WriteLine("Generating post-processing summary report...");
                
                // Read all runs from CSV to make summary comprehensive
                var readerWriter = new ExperimentResultWriter();
                var allRunsFromCsv = await ReadAllRunsFromCsvAsync(resultsCsvPath);

                string summaryCsvPath = Path.Combine(Path.GetDirectoryName(resultsCsvPath) ?? configDir, "ExperimentSummary.csv");
                await resultWriter.WriteSummaryAsync(allRunsFromCsv, summaryCsvPath);
                
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine($"Experiment complete! Summary generated at: {summaryCsvPath}");
                Console.ResetColor();
            }

            return 0;
        }

        private static (
            List<PrecastElement> Elements,
            List<PrecastElement> ScopeElements,
            List<GreenAreaZone> AllowedZones,
            List<GreenAreaZone> ForbiddenZones,
            string ProjectName,
            string ProjectId,
            string IfcFileName,
            int RawElementCount) LoadProjectData(string projectFilePath)
        {
            var options = new DbContextOptionsBuilder<CreatedProjectDbContext>()
                .UseSqlite($"Data Source={projectFilePath}")
                .Options;

            using (var context = new CreatedProjectDbContext(options))
            {
                var projectInfo = context.ProjectInfo.OrderBy(p => p.Id).FirstOrDefault();
                if (projectInfo == null)
                {
                    throw new InvalidOperationException($"No ProjectInfo found in database '{projectFilePath}'.");
                }

                var elementsInDb = context.IfcElements
                    .AsNoTracking()
                    .Where(e => e.ProjectInfoId == projectInfo.Id)
                    .ToList();

                var zonesInDb = context.SiteZones
                    .AsNoTracking()
                    .Where(z => z.ProjectInfoId == projectInfo.Id)
                    .ToList();

                var activeIfcFile = context.IfcFiles
                    .AsNoTracking()
                    .FirstOrDefault(f => f.ProjectInfoId == projectInfo.Id && f.IsActive);

                string ifcFileName = activeIfcFile?.FileName ?? "unknown.ifc";

                var elements = new List<PrecastElement>();
                var scopeElements = new List<PrecastElement>();
                foreach (var entity in elementsInDb)
                {
                    var domainElement = MapElementToDomain(entity);
                    scopeElements.Add(domainElement);
                    
                    if (!domainElement.IncludeInGa) continue;
                    if (domainElement.Role == IfcElementRole.NeedsReview) continue;
                    if (domainElement.Role == IfcElementRole.GroupingOnly) continue;
                    if (domainElement.Role == IfcElementRole.InternalAuxiliary) continue;
                    if (domainElement.Role == IfcElementRole.Excluded) continue;
                    if (!domainElement.WeightTons.HasValue || domainElement.WeightTons.Value <= 0) continue;
                    if (domainElement.WeightTrustLevel != IfcWeightTrustLevel.ExplicitQuantity &&
                        domainElement.WeightTrustLevel != IfcWeightTrustLevel.ExplicitProperty) continue;
                    if (domainElement.ElementCOG == null || 
                        double.IsNaN(domainElement.ElementCOG.X) || double.IsInfinity(domainElement.ElementCOG.X) ||
                        double.IsNaN(domainElement.ElementCOG.Y) || double.IsInfinity(domainElement.ElementCOG.Y) ||
                        double.IsNaN(domainElement.ElementCOG.Z) || double.IsInfinity(domainElement.ElementCOG.Z)) continue;

                    elements.Add(domainElement);
                }

                var allowedZones = new List<GreenAreaZone>();
                var forbiddenZones = new List<GreenAreaZone>();
                foreach (var entity in zonesInDb)
                {
                    var zone = MapZoneToDomain(entity);
                    if (zone.Type == ZoneType.Allowed)
                    {
                        allowedZones.Add(zone);
                    }
                    else if (zone.Type == ZoneType.Forbidden)
                    {
                        forbiddenZones.Add(zone);
                    }
                }

                return (
                    elements,
                    scopeElements,
                    allowedZones,
                    forbiddenZones,
                    projectInfo.Name,
                    projectInfo.ProjectGuid,
                    ifcFileName,
                    elementsInDb.Count);
            }
        }

        private static PrecastElement MapElementToDomain(ProjectIfcElementEntity entity)
        {
            double minX = entity.BBoxMinX ?? 0;
            double minY = entity.BBoxMinY ?? 0;
            double minZ = entity.BBoxMinZ ?? 0;
            double maxX = entity.BBoxMaxX ?? 0;
            double maxY = entity.BBoxMaxY ?? 0;
            double maxZ = entity.BBoxMaxZ ?? 0;

            double bboxLength = maxX - minX;
            double bboxWidth = maxY - minY;
            double bboxHeight = maxZ - minZ;

            var cog = new DomainPoint3D(entity.COG_X ?? 0, entity.COG_Y ?? 0, entity.COG_Z ?? 0);
            var bbox = new DomainPoint3D(bboxLength, bboxWidth, bboxHeight);

            _ = Enum.TryParse<IfcElementRole>(entity.Role, out var role);
            _ = Enum.TryParse<IfcWeightTrustLevel>(entity.WeightTrustLevel, out var trustLevel);

            var domain = new PrecastElement
            {
                ElementID = entity.GlobalId,
                ElementRevitId = entity.Name,
                ElementMark = entity.Mark,
                ElementType = entity.ElementType ?? string.Empty,
                ElementLength = entity.LengthM,
                ElementWidth = entity.WidthM,
                ElementHeight = entity.HeightM,
                ElementMaterial = entity.Material ?? string.Empty,
                ElementWT = entity.WeightTons,
                ElementVolume = entity.VolumeM3,
                ElementCOG = cog,
                BBoxLength = bboxLength,
                BBoxWidth = bboxWidth,
                BBoxHeight = bboxHeight,

                RawIfcType = entity.RawIfcType ?? string.Empty,
                ResolvedCategory = entity.ResolvedCategory ?? string.Empty,
                Role = role,
                ParentGlobalId = entity.ParentGlobalId,
                IncludeInRender = entity.IncludeInRender,
                IncludeInElementTree = entity.IncludeInElementTree,
                IncludeInGa = entity.IncludeInGa,
                WeightTons = entity.WeightTons,
                WeightSource = entity.WeightSource ?? "Missing",
                WeightTrustLevel = trustLevel,
                VolumeM3 = entity.VolumeM3,
                COG_M = cog,
                BoundingBox_M = bbox,
                NormalizationDecisionReason = entity.NormalizationDecisionReason ?? string.Empty
            };

            if (!string.IsNullOrWhiteSpace(entity.ChildGlobalIdsJson))
            {
                try { domain.ChildGlobalIds = JsonSerializer.Deserialize<List<string>>(entity.ChildGlobalIdsJson) ?? new(); } catch {}
            }
            if (!string.IsNullOrWhiteSpace(entity.VisibleGeometryOwnerGlobalIdsJson))
            {
                try { domain.VisibleGeometryOwnerGlobalIds = JsonSerializer.Deserialize<List<string>>(entity.VisibleGeometryOwnerGlobalIdsJson) ?? new(); } catch {}
            }
            if (!string.IsNullOrWhiteSpace(entity.HiddenInternalGeometryOwnerGlobalIdsJson))
            {
                try { domain.HiddenInternalGeometryOwnerGlobalIds = JsonSerializer.Deserialize<List<string>>(entity.HiddenInternalGeometryOwnerGlobalIdsJson) ?? new(); } catch {}
            }
            if (!string.IsNullOrWhiteSpace(entity.ExtractionWarningsJson))
            {
                try { domain.ExtractionWarnings = JsonSerializer.Deserialize<List<string>>(entity.ExtractionWarningsJson) ?? new(); } catch {}
            }

            return domain;
        }

        private static GreenAreaZone MapZoneToDomain(SiteZoneEntity entity)
        {
            _ = Guid.TryParse(entity.ZoneGuid, out var zoneId);
            _ = Enum.TryParse<ZoneType>(entity.ZoneType, out var zoneType);

            var vertices = new List<DomainPoint3D>();
            if (!string.IsNullOrWhiteSpace(entity.VerticesJson))
            {
                try 
                { 
                    vertices = JsonSerializer.Deserialize<List<DomainPoint3D>>(
                        entity.VerticesJson, 
                        new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new(); 
                } 
                catch {}
            }

            return new GreenAreaZone
            {
                ZoneId = zoneId == Guid.Empty ? Guid.NewGuid() : zoneId,
                Name = entity.Name,
                Type = zoneType,
                Area = entity.Area,
                Vertices = vertices
            };
        }

        private static async Task PersistAcceptanceArtifactsAsync(
            GAExperimentConfig config,
            string configDir,
            string projectFile,
            string projectName,
            string projectId,
            string ifcFileName,
            PlacementInput input,
            PlacementResult result,
            GASettings settings,
            string runId,
            DateTimeOffset startedAtUtc,
            DateTimeOffset completedAtUtc,
            long durationMilliseconds,
            CancellationToken cancellationToken)
        {
            var configuredRoot = string.IsNullOrWhiteSpace(config.AcceptanceProjectRoot)
                ? Path.GetDirectoryName(projectFile) ?? throw new InvalidOperationException("Acceptance project root could not be resolved.")
                : config.AcceptanceProjectRoot;
            var projectRoot = Path.GetFullPath(Path.IsPathRooted(configuredRoot)
                ? configuredRoot
                : Path.Combine(configDir, configuredRoot));

            if (!Directory.Exists(projectRoot))
                throw new DirectoryNotFoundException($"Acceptance project root does not exist: {projectRoot}");

            var projectFileRoot = Path.GetFullPath(Path.GetDirectoryName(projectFile) ?? string.Empty);
            if (!string.Equals(projectFileRoot.TrimEnd(Path.DirectorySeparatorChar), projectRoot.TrimEnd(Path.DirectorySeparatorChar), StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    $"Acceptance project root '{projectRoot}' does not match the project database root '{projectFileRoot}'. " +
                    "REV05 acceptance artifacts must be generated from and written back to the same project folder.");
            }

            if (input.SubmittedCanonicalLiftUnits.Count == 0
                || string.IsNullOrWhiteSpace(input.CanonicalSnapshotId)
                || string.IsNullOrWhiteSpace(input.CanonicalPayloadHash))
            {
                throw new InvalidOperationException("Acceptance run did not contain the canonical pre-run lift-unit snapshot required for a production GA result snapshot.");
            }

            var finalStageRunId = runId + "-S1";
            var requested = CloneSettings(settings);
            requested.OptimizationRunId = runId;
            requested.StageRunId = finalStageRunId;
            requested.StageIndex = 1;
            var effective = CloneSettings(settings);
            effective.OptimizationRunId = runId;
            effective.StageRunId = finalStageRunId;
            effective.StageIndex = 1;

            var execution = new GaExecutionResult
            {
                OriginalInput = input,
                BestResult = result,
                OptimizationRunId = runId,
                FinalStageRunId = finalStageRunId,
                FinalStageIndex = 1,
                StartedAtUtc = startedAtUtc,
                CompletedAtUtc = completedAtUtc,
                DurationMilliseconds = durationMilliseconds,
                TerminationReason = "REV05 deterministic acceptance run completed.",
                RequestedSettings = requested,
                EffectiveFinalSettings = effective,
                Stages = new[]
                {
                    new GaStageExecutionResult
                    {
                        OptimizationRunId = runId,
                        StageRunId = finalStageRunId,
                        StageIndex = 1,
                        EffectiveSettings = effective,
                        Result = result,
                        StartedAtUtc = startedAtUtc,
                        CompletedAtUtc = completedAtUtc,
                        DurationMilliseconds = durationMilliseconds,
                        TerminationReason = "Acceptance stage completed.",
                        WasSelectedAsFinal = true
                    }
                }
            };

            var ifcPath = Path.Combine(projectRoot, "Models", "IFC", ifcFileName);
            if (!File.Exists(ifcPath))
                throw new FileNotFoundException("Acceptance project's active IFC was not found.", ifcPath);

            var configuredGitHash = config.GitCommitHash;
            if (string.Equals(configuredGitHash, "FROM_RELEASE_PIPELINE", StringComparison.OrdinalIgnoreCase))
                configuredGitHash = Environment.GetEnvironmentVariable("CRANEOPT_GIT_COMMIT") ?? string.Empty;

            var mapper = new GaResultSnapshotMapper();
            var snapshot = await mapper.MapAsync(
                execution,
                new GaSnapshotMappingContext
                {
                    ProjectGuid = projectId,
                    ProjectName = projectName,
                    ProjectDatabaseId = Path.GetFileName(projectFile),
                    ProjectRoot = projectRoot,
                    IfcFilePath = ifcPath,
                    ApplicationVersion = config.ApplicationVersion,
                    GitCommitHash = configuredGitHash
                },
                cancellationToken).ConfigureAwait(false);

            var validator = new GaResultSnapshotValidator();
            var validation = validator.Validate(snapshot);
            if (!validation.IsValid)
                throw new InvalidOperationException("Acceptance GA snapshot validation failed: " + string.Join("; ", validation.Errors));

            var outputPaths = new ProjectOutputPathService(new FixedActiveProjectPathProvider(projectRoot));
            var pathResolver = new GaResultSnapshotPathResolver(outputPaths);
            var snapshotStore = new JsonGaResultSnapshotStore(pathResolver, validator);
            await snapshotStore.SaveAsync(snapshot, cancellationToken).ConfigureAwait(false);

            // A fresh store instance reads the just-saved snapshot through its checksum and
            // validator path. This prevents the acceptance result from being accepted merely
            // because it remained in memory after the GA run.
            var reopenedStore = new JsonGaResultSnapshotStore(new GaResultSnapshotPathResolver(outputPaths), new GaResultSnapshotValidator());
            var snapshotPath = pathResolver.ResolveSnapshotPath(runId);
            var reopened = await reopenedStore.LoadAsync(snapshotPath, cancellationToken).ConfigureAwait(false)
                ?? throw new InvalidOperationException("Acceptance GA snapshot could not be reopened through the production snapshot store.");
            if (!string.Equals(reopened.Run.OptimizationRunId, runId, StringComparison.Ordinal)
                || reopened.Summary.TotalTargetLiftUnitCount != snapshot.Summary.TotalTargetLiftUnitCount
                || reopened.Summary.LiftedLiftUnitCount != snapshot.Summary.LiftedLiftUnitCount
                || !string.Equals(reopened.CanonicalPayloadHash, snapshot.CanonicalPayloadHash, StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Acceptance GA snapshot changed after save/reopen verification.");
            }

            var runDirectory = pathResolver.ResolveRunDirectory(runId);

#pragma warning disable CS0618 // REV05 deliberately exercises the application's headless report foundation in addition to UI reporting.
            var reportBuilder = new OptimizationSummaryReportBuilder();
            var report = reportBuilder.Build(new OptimizationReportInput
            {
                PlacementInput = input,
                PlacementResult = result,
                PreparedBy = "REV05 acceptance harness",
                CreatedAtUtc = completedAtUtc.UtcDateTime
            });
            var formatter = new ReportValueFormatter();
            var pdfResult = await new PdfReportExporter(formatter)
                .ExportAsync(report, Path.Combine(runDirectory, "lifting_report.pdf"), cancellationToken)
                .ConfigureAwait(false);
            if (!pdfResult.Success || !File.Exists(pdfResult.PrimaryOutputPath) || new FileInfo(pdfResult.PrimaryOutputPath).Length < 1024)
                throw new InvalidOperationException("Acceptance PDF report generation failed or produced a non-substantive file: " + pdfResult.ErrorMessage);

            var csvResult = await new CsvReportExporter(formatter)
                .ExportAsync(report, Path.Combine(runDirectory, "csv"), cancellationToken)
                .ConfigureAwait(false);
#pragma warning restore CS0618
            if (!csvResult.Success || !Directory.Exists(csvResult.PrimaryOutputPath))
                throw new InvalidOperationException("Acceptance CSV report generation failed: " + csvResult.ErrorMessage);
            var detailedCsv = Directory.EnumerateFiles(csvResult.PrimaryOutputPath, "*.csv", SearchOption.TopDirectoryOnly)
                .FirstOrDefault(path => Path.GetFileName(path).Contains("element_lift_details", StringComparison.OrdinalIgnoreCase));
            if (detailedCsv == null || new FileInfo(detailedCsv).Length < 100)
                throw new InvalidOperationException("Acceptance CSV export did not contain a substantive element_lift_details table.");

            // Bind the exact report rows to the exact PDF/CSV bytes.  The independent
            // release verifier uses this sidecar to prove report -> snapshot -> catalog
            // reconciliation without trusting filenames or a non-substantive PDF alone.
            var detailTable = report.Sections
                .SelectMany(section => section.Tables)
                .FirstOrDefault(table => string.Equals(table.Name, "element_lift_details", StringComparison.Ordinal))
                ?? throw new InvalidOperationException("Acceptance report did not contain element_lift_details.");
            static string Sha256File(string path)
            {
                using var stream = File.OpenRead(path);
                return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
            }
            var reportProvenancePath = Path.Combine(runDirectory, "report_provenance.json");
            var reportProvenance = new
            {
                schemaVersion = 1,
                optimizationRunId = runId,
                reportTable = detailTable.Name,
                rowCount = detailTable.Rows.Count,
                columns = detailTable.Columns.Select(column => column.Key).ToArray(),
                rows = detailTable.Rows.Select(row => row.Values).ToArray(),
                pdf = new { path = Path.GetFileName(pdfResult.PrimaryOutputPath), sha256 = Sha256File(pdfResult.PrimaryOutputPath), bytes = new FileInfo(pdfResult.PrimaryOutputPath).Length },
                csv = new { path = Path.GetRelativePath(runDirectory, detailedCsv).Replace('\\', '/'), sha256 = Sha256File(detailedCsv), bytes = new FileInfo(detailedCsv).Length },
                generatedAtUtc = DateTimeOffset.UtcNow
            };
            await File.WriteAllTextAsync(
                reportProvenancePath,
                JsonSerializer.Serialize(reportProvenance, new JsonSerializerOptions { WriteIndented = true }) + Environment.NewLine,
                Encoding.UTF8,
                cancellationToken).ConfigureAwait(false);

            var verificationPath = Path.Combine(runDirectory, "save_reopen_verification.json");
            var verification = new
            {
                schemaVersion = 1,
                optimizationRunId = runId,
                snapshotPath = Path.GetRelativePath(projectRoot, snapshotPath).Replace('\\', '/'),
                snapshotReopened = true,
                canonicalPayloadHashUnchanged = true,
                totalTargets = reopened.Summary.TotalTargetLiftUnitCount,
                liftedTargets = reopened.Summary.LiftedLiftUnitCount,
                coverageRatio = reopened.Summary.CoverageRatio,
                pdf = Path.GetRelativePath(projectRoot, pdfResult.PrimaryOutputPath).Replace('\\', '/'),
                detailedCsv = Path.GetRelativePath(projectRoot, detailedCsv).Replace('\\', '/'),
                reportProvenance = Path.GetRelativePath(projectRoot, reportProvenancePath).Replace('\\', '/'),
                generatedAtUtc = DateTimeOffset.UtcNow
            };
            await File.WriteAllTextAsync(
                verificationPath,
                JsonSerializer.Serialize(verification, new JsonSerializerOptions { WriteIndented = true }) + Environment.NewLine,
                Encoding.UTF8,
                cancellationToken).ConfigureAwait(false);

            // Persist the completed run to project.db so the supported project folder carries
            // its own run history/state after the same execution that created the reports.
            var dbOptions = new DbContextOptionsBuilder<CreatedProjectDbContext>()
                .UseSqlite($"Data Source={projectFile}")
                .Options;
            await using (var db = new CreatedProjectDbContext(dbOptions))
            {
                var project = await db.ProjectInfo.OrderBy(info => info.Id).FirstAsync(cancellationToken).ConfigureAwait(false);
                var craneSnapshotId = await db.CraneSnapshots
                    .Where(crane => crane.ProjectInfoId == project.Id)
                    .Select(crane => (int?)crane.Id)
                    .FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);

                var previousAcceptanceRuns = await db.OptimizationRuns
                    .Where(run => run.ProjectInfoId == project.Id && run.RunName != "REV05-FIXTURE")
                    .ToListAsync(cancellationToken).ConfigureAwait(false);
                if (previousAcceptanceRuns.Count > 0)
                    db.OptimizationRuns.RemoveRange(previousAcceptanceRuns);

                var liftedEvaluations = result.Positions
                    .SelectMany(position => position.ElementResults)
                    .Where(row => row.Status == LiftStatus.Liftable && row.AvailableCapacity > 0 && row.RequiredCapacity > 0)
                    .ToList();
                var avgUtilization = liftedEvaluations.Count == 0
                    ? 0d
                    : liftedEvaluations.Average(row => row.RequiredCapacity / row.AvailableCapacity * 100d);
                var positionsJson = JsonSerializer.Serialize(result.Positions.Select((position, index) => new
                {
                    setupIndex = index + 1,
                    position.X,
                    position.Y,
                    position.Z,
                    position.NewLiftableCount,
                    position.LiftableCount
                }));
                var summaryJson = JsonSerializer.Serialize(new
                {
                    optimizationRunId = runId,
                    snapshot = Path.GetRelativePath(projectRoot, snapshotPath).Replace('\\', '/'),
                    pdf = Path.GetRelativePath(projectRoot, pdfResult.PrimaryOutputPath).Replace('\\', '/'),
                    detailedCsv = Path.GetRelativePath(projectRoot, detailedCsv).Replace('\\', '/'),
                    result.LiftedElementCount,
                    result.TotalElementCount,
                    result.CoverageRatio,
                    result.FitnessScore,
                    softwareAcceptanceFixture = true
                });

                db.OptimizationRuns.Add(new CraneOptimizer.Domain.CreatedProject.OptimizationFiles.ProjectOptimizationRunEntity
                {
                    ProjectInfoId = project.Id,
                    CraneSnapshotId = craneSnapshotId,
                    RunName = runId,
                    Status = "Completed",
                    StartedAtUtc = startedAtUtc.UtcDateTime,
                    FinishedAtUtc = completedAtUtc.UtcDateTime,
                    TotalElements = result.TotalElementCount,
                    SafeElements = result.LiftedElementCount,
                    UnsafeElements = Math.Max(0, result.TotalElementCount - result.LiftedElementCount),
                    CoverageRatio = result.CoverageRatio,
                    AverageUtilizationPercent = avgUtilization,
                    SettingsJson = JsonSerializer.Serialize(effective),
                    ResultSummaryJson = summaryJson,
                    CranePositionsJson = positionsJson
                });
                await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            }

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"[REV05 acceptance] Production snapshot/report/save-reopen artifacts written to: {runDirectory}");
            Console.ResetColor();
        }

        private sealed class FixedActiveProjectPathProvider : IActiveProjectPathProvider
        {
            private readonly string _projectRoot;
            public FixedActiveProjectPathProvider(string projectRoot) => _projectRoot = projectRoot;
            public string? GetActiveProjectPath() => _projectRoot;
        }

        private static string ResolveCatalogDbPath(string configuredPath, string configDir)
        {
            if (!string.IsNullOrWhiteSpace(configuredPath))
            {
                string fullPath = Path.IsPathRooted(configuredPath) ? configuredPath : Path.Combine(configDir, configuredPath);
                if (File.Exists(fullPath)) return Path.GetFullPath(fullPath);
            }

            var relativePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..", "CraneOptimizer.UI", "bin", "Debug", "net8.0-windows", "Data", "crane.db");
            if (File.Exists(relativePath)) return Path.GetFullPath(relativePath);

            var currentDir = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
            while (currentDir != null)
            {
                var searchPath = Path.Combine(currentDir.FullName, "CraneOptimizer.UI", "bin", "Debug", "net8.0-windows", "Data", "crane.db");
                if (File.Exists(searchPath)) return Path.GetFullPath(searchPath);

                var match = currentDir.GetFiles("crane.db", SearchOption.AllDirectories).FirstOrDefault();
                if (match != null) return match.FullName;

                currentDir = currentDir.Parent;
            }

            return configuredPath;
        }

        private static IEnumerable<GASettings> GenerateMatrixCombinations(
            MatrixSettings matrix,
            bool useHungarianAssignment,
            bool enableReachabilitySeeding)
        {
            var popSizes = matrix.PopulationSize.DefaultIfEmpty(30).ToList();
            var maxGens = matrix.MaxGenerations.DefaultIfEmpty(30).ToList();
            var mutRates = matrix.MutationRate.DefaultIfEmpty(0.1).ToList();
            var crossRates = matrix.CrossoverRate.DefaultIfEmpty(0.8).ToList();
            var maxPositions = matrix.MaxCranePositions.DefaultIfEmpty(3).ToList();
            var minRelocs = matrix.MinimumRelocationDistance.DefaultIfEmpty(5.0).ToList();
            var sigmas = matrix.MutationSigma.DefaultIfEmpty(0.05).ToList();
            var gridSteps = matrix.GridStep.DefaultIfEmpty(0.5).ToList();

            foreach (var pop in popSizes)
            foreach (var gen in maxGens)
            foreach (var mut in mutRates)
            foreach (var cross in crossRates)
            foreach (var pos in maxPositions)
            foreach (var reloc in minRelocs)
            foreach (var sigma in sigmas)
            foreach (var grid in gridSteps)
            {
                yield return new GASettings
                {
                    PopulationSize = pop,
                    MaxGenerations = gen,
                    MutationRate = mut,
                    CrossoverRate = cross,
                    MaxCranePositions = pos,
                    MinimumRelocationDistance = reloc,
                    MutationSigma = sigma,
                    GridStep = grid,
                    WireLength = 2.0,
                    AdditionalWt = 0.5,
                    SafetyFactor = 0.9,
                    DeratingRatio = 0.9,
                    EnableProfiling = true,
                    EnableConcurrentOptimization = true,
                    UseHungarianAssignment = useHungarianAssignment,
                    EnableReachabilitySeeding = enableReachabilitySeeding,
                    DiagnosticsLevel = DiagnosticsLevel.Research
                };
            }
        }

        private static GASettings CloneSettings(GASettings src)
        {
            return new GASettings
            {
                PopulationSize = src.PopulationSize,
                MaxGenerations = src.MaxGenerations,
                MutationRate = src.MutationRate,
                CrossoverRate = src.CrossoverRate,
                MaxCranePositions = src.MaxCranePositions,
                MinimumRelocationDistance = src.MinimumRelocationDistance,
                MutationSigma = src.MutationSigma,
                GridStep = src.GridStep,
                WireLength = src.WireLength,
                AdditionalWt = src.AdditionalWt,
                SafetyFactor = src.SafetyFactor,
                DeratingRatio = src.DeratingRatio,
                EnableProfiling = src.EnableProfiling,
                EnableConcurrentOptimization = src.EnableConcurrentOptimization,
                UseHungarianAssignment = src.UseHungarianAssignment,
                EnableReachabilitySeeding = src.EnableReachabilitySeeding,
                DiagnosticsLevel = src.DiagnosticsLevel,
                BatchModeEnabled = src.BatchModeEnabled,
                ExperimentBatchId = src.ExperimentBatchId,
                ExperimentName = src.ExperimentName,
                ParameterSetId = src.ParameterSetId,
                ParameterSetName = src.ParameterSetName,
                RunIndex = src.RunIndex
            };
        }

        public static string GenerateRunId(string projectFile, string craneModel, int seed, GASettings settings)
        {
            string projectDirName = Path.GetFileName(Path.GetDirectoryName(projectFile)) ?? string.Empty;
            string projectPathKey = Path.Combine(projectDirName, Path.GetFileName(projectFile)).Replace('\\', '/');

            var key = $"{projectPathKey.ToLowerInvariant()}_" +
                      $"{craneModel.ToLowerInvariant()}_" +
                      $"{seed}_" +
                      $"{(settings.ParameterSetId ?? "N/A").ToLowerInvariant()}_" +
                      $"{(settings.ParameterSetName ?? "N/A").ToLowerInvariant()}_" +
                      $"{settings.PopulationSize}_" +
                      $"{settings.MaxGenerations}_" +
                      $"{settings.MutationRate.ToString("F3", CultureInfo.InvariantCulture)}_" +
                      $"{settings.MutationSigma.ToString("F3", CultureInfo.InvariantCulture)}_" +
                      $"{settings.CrossoverRate.ToString("F3", CultureInfo.InvariantCulture)}_" +
                      $"{settings.MaxCranePositions}_" +
                      $"{settings.CoverageWeight.ToString("F3", CultureInfo.InvariantCulture)}_" +
                      $"{settings.RelocationWeight.ToString("F3", CultureInfo.InvariantCulture)}_" +
                      $"{settings.RadiusPenaltyWeight.ToString("F3", CultureInfo.InvariantCulture)}_" +
                      $"{settings.RadiusQualityPenaltyWeight.ToString("F3", CultureInfo.InvariantCulture)}_" +
                      $"{settings.OverweightPenaltyWeight.ToString("F3", CultureInfo.InvariantCulture)}_" +
                      $"{settings.HeightPenaltyWeight.ToString("F3", CultureInfo.InvariantCulture)}_" +
                      $"{settings.RadiusStdDevPenaltyWeight.ToString("F3", CultureInfo.InvariantCulture)}_" +
                      $"{settings.BalancePenaltyWeight.ToString("F3", CultureInfo.InvariantCulture)}_" +
                      $"{settings.SimilarPositionPenaltyWeight.ToString("F3", CultureInfo.InvariantCulture)}_" +
                      $"{settings.RelocationDistancePenaltyWeight.ToString("F3", CultureInfo.InvariantCulture)}_" +
                      $"{settings.MinimumRelocationDistance.ToString("F1", CultureInfo.InvariantCulture)}_" +
                      $"{settings.GridStep.ToString("F2", CultureInfo.InvariantCulture)}_" +
                      $"{settings.UseHungarianAssignment.ToString().ToLowerInvariant()}";

            using var sha = System.Security.Cryptography.SHA256.Create();
            var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(key));
            return Convert.ToHexString(bytes).Substring(0, 16);
        }

        private static void GenerateDefaultConfig(string path)
        {
            var config = new GAExperimentConfig
            {
                CatalogDatabasePath = "..\\CraneOptimizer.UI\\bin\\Debug\\net8.0-windows\\Data\\crane.db",
                ProjectFiles = new List<string> { "..\\tryProjects\\try4\\project.craneopt" },
                Cranes = new List<string> { "LTM 1050" },
                ExperimentResultsCsvPath = "ExperimentResults.csv",
                DiagnosticsDirectoryPath = "Diagnostics",
                BenchmarkVersion = "Benchmark_v1",
                SeedMode = "Fixed",
                Seeds = new List<int> { 42 },
                RandomSeedCount = 5,
                MaxRuns = 10000,
                ResumeEnabled = true,
                UseHungarianAssignment = false,
                EnableReachabilitySeeding = false,
                Matrix = new MatrixSettings
                {
                    PopulationSize = new List<int> { 30, 50 },
                    MaxGenerations = new List<int> { 30, 50 },
                    MutationRate = new List<double> { 0.05, 0.1 },
                    CrossoverRate = new List<double> { 0.8 },
                    MaxCranePositions = new List<int> { 3 },
                    MinimumRelocationDistance = new List<double> { 5.0 },
                    MutationSigma = new List<double> { 0.05 },
                    GridStep = new List<double> { 0.5 }
                }
            };

            var options = new JsonSerializerOptions { WriteIndented = true };
            var json = JsonSerializer.Serialize(config, options);
            File.WriteAllText(path, json, Encoding.UTF8);
        }

        private static async Task<List<ExperimentRunResult>> ReadAllRunsFromCsvAsync(string csvPath)
        {
            var list = new List<ExperimentRunResult>();
            if (!File.Exists(csvPath)) return list;

            try
            {
                using var stream = new FileStream(csvPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                using var reader = new StreamReader(stream, Encoding.UTF8);

                string? header = await reader.ReadLineAsync();
                if (header == null) return list;
                var headers = ParseCsvLine(header);
                var headerIndexes = headers
                    .Select((name, index) => new { Name = name, Index = index })
                    .ToDictionary(x => x.Name, x => x.Index, StringComparer.OrdinalIgnoreCase);

                int GetIndex(string name, int legacyIndex)
                {
                    return headerIndexes.TryGetValue(name, out int index) ? index : legacyIndex;
                }

                string GetValue(List<string> row, string name, int legacyIndex, string fallback = "")
                {
                    int index = GetIndex(name, legacyIndex);
                    return index >= 0 && index < row.Count ? row[index] : fallback;
                }

                int GetInt(List<string> row, string name, int legacyIndex, int fallback = 0)
                {
                    string value = GetValue(row, name, legacyIndex);
                    return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed)
                        ? parsed
                        : fallback;
                }

                long GetLong(List<string> row, string name, int legacyIndex)
                {
                    string value = GetValue(row, name, legacyIndex);
                    return long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out long parsed)
                        ? parsed
                        : 0L;
                }

                double GetDouble(List<string> row, string name, int legacyIndex)
                {
                    string value = GetValue(row, name, legacyIndex);
                    return double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out double parsed)
                        ? parsed
                        : 0.0;
                }

                while (await reader.ReadLineAsync() is string line)
                {
                    if (string.IsNullOrWhiteSpace(line)) continue;
                    var parts = ParseCsvLine(line);
                    if (parts.Count < 29) continue;

                    try
                    {
                        var res = new ExperimentRunResult
                        {
                            RunId = GetValue(parts, "RunId", 0),
                            BenchmarkVersion = GetValue(parts, "BenchmarkVersion", -1, "Benchmark_v1"),
                            Timestamp = GetValue(parts, "Timestamp", 1),
                            ProjectFile = GetValue(parts, "ProjectFile", 2),
                            ProjectScale = GetValue(parts, "ProjectScale", -1),
                            ElementCount = GetInt(parts, "ElementCount", -1),
                            CraneModel = GetValue(parts, "CraneModel", 3),
                            PopulationSize = GetInt(parts, "PopulationSize", 4),
                            MaxGenerations = GetInt(parts, "MaxGenerations", 5),
                            MutationRate = GetDouble(parts, "MutationRate", 6),
                            MutationSigma = GetDouble(parts, "MutationSigma", 7),
                            CrossoverRate = GetDouble(parts, "CrossoverRate", 8),
                            MaxCranePositions = GetInt(parts, "MaxCranePositions", 9),
                            MinimumRelocationDistance = GetDouble(parts, "MinimumRelocationDistance", 10),
                            GridStep = GetDouble(parts, "GridStep", 11),
                            RandomSeed = GetInt(parts, "RandomSeed", 12),
                            Coverage = GetDouble(parts, "Coverage", 13),
                            CoveredElements = GetInt(parts, "CoveredElements", 14),
                            TotalElements = GetInt(parts, "TotalElements", 15),
                            Fitness = GetDouble(parts, "Fitness", 16),
                            ExecutionTimeMs = GetLong(parts, "ExecutionTimeMs", 17),
                            GenerationReached = GetInt(parts, "GenerationReached", 18),
                            UniqueCranePositions = GetInt(parts, "UniqueCranePositions", 19),
                            AverageRadius = GetDouble(parts, "AverageRadius", 20),
                            MaximumRadius = GetDouble(parts, "MaximumRadius", 21),
                            RelocationDistance = GetDouble(parts, "RelocationDistance", 22),
                            LiftFailures = GetInt(parts, "LiftFailures", 23),
                            OutOfRadius = GetInt(parts, "OutOfRadius", 24),
                            Overweight = GetInt(parts, "Overweight", 25),
                            HeightFail = GetInt(parts, "HeightFail", 26),
                            SpecialCase = GetInt(parts, "SpecialCase", 27),
                            NotCharted = GetInt(parts, "NotCharted", 28),
                            FailureDominantReason = GetValue(parts, "FailureDominantReason", -1, "None"),
                            FailureDominantPercent = GetDouble(parts, "FailureDominantPercent", -1)
                        };
                        res.RefreshDerivedFields();
                        list.Add(res);
                    }
                    catch {}
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Warning] Failed to parse existing CSV runs: {ex.Message}");
            }

            return list;
        }

        private static List<string> ParseCsvLine(string line)
        {
            var parts = new List<string>();
            var sb = new StringBuilder();
            bool inQuotes = false;

            for (int i = 0; i < line.Length; i++)
            {
                char c = line[i];
                if (c == '\"')
                {
                    if (inQuotes && i + 1 < line.Length && line[i + 1] == '\"')
                    {
                        sb.Append('\"');
                        i++;
                    }
                    else
                    {
                        inQuotes = !inQuotes;
                    }
                }
                else if (c == ',' && !inQuotes)
                {
                    parts.Add(sb.ToString());
                    sb.Clear();
                }
                else
                {
                    sb.Append(c);
                }
            }
            parts.Add(sb.ToString());
            return parts;
        }
    }
}
