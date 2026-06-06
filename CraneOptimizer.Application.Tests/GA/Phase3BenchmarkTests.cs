using Xunit;
using Xunit.Abstractions;
using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Engines.CraneConfiguration;
using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Engines.LiftingCalculation;
using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Engines.Constraints;
using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Engines.Coverage;
using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Engines.GeneticOptimization;
using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Engines.PlacementOrchestration;
using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Operators;
using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Settings;
using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Models;
using CraneOptimizer.Application.Optimization.GeneticAlgorithms.DTOs;
using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Services;
using CraneOptimizer.Domain.Common;
using CraneOptimizer.Domain.Entities;
using CraneOptimizer.Domain.Schemas;
using CraneOptimizer.Domain.Schemas.Enums;
using CraneOptimizer.Domain.Entities.Enums;
using CraneOptimizer.Application.Optimization.Utilities;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Microsoft.Data.Sqlite;

namespace CraneOptimizer.Application.Tests.GA
{
    public class Phase3BenchmarkTests
    {
        private readonly ITestOutputHelper _output;

        // Paths to the try4 project and crane databases
        private const string ProjectDbPath = @"F:\06-ITI\00-Project\tryProjects\try4\project.db";
        private const string CraneDbPath = @"F:\06-ITI\00-Project\Main Project\CraneOptimizer\CraneOptimizer.UI\bin\Debug\net8.0-windows\Data\crane.db";

        public Phase3BenchmarkTests(ITestOutputHelper output)
        {
            _output = output;
        }

        [Theory]
        [InlineData(1, "STC800_80T")]
        [InlineData(2, "180Tons")]
        public void Phase3_Try4_Benchmark_ConcurrentVsLegacy(int craneId, string craneLabel)
        {
            var logLines = new List<string>();
            Action<string> log = (msg) =>
            {
                _output.WriteLine(msg);
                logLines.Add(msg);
            };

            if (!File.Exists(ProjectDbPath) || !File.Exists(CraneDbPath))
            {
                log("SKIP: try4 project.db or crane.db not found on this machine.");
                return;
            }

            // ── Load project data from SQLite ──────────────────────
            var elements = LoadElements(ProjectDbPath);
            var (allowedZones, forbiddenZones) = LoadZones(ProjectDbPath);
            var (craneSpec, footprintRadius) = LoadCraneSpec(CraneDbPath, craneId: craneId);

            log($"Loaded {elements.Count} elements, {allowedZones.Count} allowed zones, {forbiddenZones.Count} forbidden zones");
            log($"Crane: {craneLabel} - MaxRadius={craneSpec.MaxRadius}, MaxBoom={craneSpec.MaxBoomLength}, LoadChart={craneSpec.LoadChart.Count} points, Footprint={footprintRadius}m");

            // ── Build shared GA settings ───────────────────────────
            var baseSettings = new GASettings
            {
                PopulationSize = 30,
                MaxGenerations = 30,
                MutationRate = 0.15,
                MutationSigma = 0.05,
                CrossoverRate = 0.8,
                WireLength = 2.0,
                AdditionalWt = 0.0,
                DeratingRatio = 0.9,
                SafetyFactor = 0.95,
                MaxCranePositions = 3,
                RandomSeed = 42,
                CoverageWeight = 10,
                RelocationWeight = 5,
                RadiusPenaltyWeight = 3,
                RadiusQualityPenaltyWeight = 1.0,
                OverweightPenaltyWeight = 10,
                HeightPenaltyWeight = 5,
                MaxRadiusForNormalisation = 50,
                GridStep = 0,
                EarlyStoppingGenerations = 20,
                ConvergenceThresholdPercent = 0,
                RadiusStdDevPenaltyWeight = 2.0,
                BalancePenaltyWeight = 2.0,
                MinimumRelocationDistance = 10.0,
                DuplicatePositionDistance = 2.0 * footprintRadius, // 2x Footprint Radius
                SimilarPositionPenaltyWeight = 8.0,
                RelocationDistancePenaltyWeight = 1.5,
                MutationExplorationMultiplier = 2.0,
                EnableAdaptiveSigma = false,
                SigmaCoolingExponent = 1.5,
                MinimumMutationSigma = 0.5,
                PositionSamplingAttempts = 80,
                CraneFootprintRadius = footprintRadius,
                EnableProfiling = true
            };

            // ── Run 1: Legacy Sequential ───────────────────────────
            log("\n════════════════════════════════════════════════════");
            log("  RUN 1: LEGACY SEQUENTIAL GREEDY SET COVER");
            log("════════════════════════════════════════════════════");

            var settingsLegacy = CloneSettings(baseSettings);
            settingsLegacy.EnableConcurrentOptimization = false;

            var resultLegacy = RunOptimization(elements, allowedZones, forbiddenZones, craneSpec, settingsLegacy, "Legacy");
            long emptyLegacy = GAProfiler.EmptyChromosomeCount;

            log($"  Coverage:       {resultLegacy.CoverageRatio:P2}");
            log($"  Fitness:        {resultLegacy.FitnessScore:F4}");
            log($"  Setup Count:    {resultLegacy.Positions.Count}");
            log($"  Lifted:         {resultLegacy.LiftedElementCount} / {resultLegacy.TotalElementCount}");
            log($"  Empty Chromo:   {emptyLegacy}");
            foreach (var pos in resultLegacy.Positions)
            {
                log($"    Setup ({pos.X:F3}, {pos.Y:F3}, {pos.Z:F3}) → NewLiftable={pos.NewLiftableCount}");
            }

            // ── Run 2: Concurrent Multi-Setup ──────────────────────
            log("\n════════════════════════════════════════════════════");
            log("  RUN 2: CONCURRENT MULTI-SETUP GA");
            log("════════════════════════════════════════════════════");

            GAProfiler.EmptyChromosomeCount = 0;

            var settingsConcurrent = CloneSettings(baseSettings);
            settingsConcurrent.EnableConcurrentOptimization = true;

            var resultConcurrent = RunOptimization(elements, allowedZones, forbiddenZones, craneSpec, settingsConcurrent, "Concurrent");
            long emptyConcurrent = GAProfiler.EmptyChromosomeCount;

            log($"  Coverage:       {resultConcurrent.CoverageRatio:P2}");
            log($"  Fitness:        {resultConcurrent.FitnessScore:F4}");
            log($"  Setup Count:    {resultConcurrent.Positions.Count}");
            log($"  Lifted:         {resultConcurrent.LiftedElementCount} / {resultConcurrent.TotalElementCount}");
            log($"  Empty Chromo:   {emptyConcurrent}");
            foreach (var pos in resultConcurrent.Positions)
            {
                log($"    Setup ({pos.X:F3}, {pos.Y:F3}, {pos.Z:F3}) → NewLiftable={pos.NewLiftableCount}");
            }

            // ── Comparison ─────────────────────────────────────────
            log("\n════════════════════════════════════════════════════");
            log("  COMPARISON: LEGACY vs CONCURRENT");
            log("════════════════════════════════════════════════════");
            log($"  Coverage:       {resultLegacy.CoverageRatio:P2}  →  {resultConcurrent.CoverageRatio:P2}  (Δ = {(resultConcurrent.CoverageRatio - resultLegacy.CoverageRatio):+0.00%;-0.00%})");
            log($"  Fitness:        {resultLegacy.FitnessScore:F4}  →  {resultConcurrent.FitnessScore:F4}");
            log($"  Setup Count:    {resultLegacy.Positions.Count}  →  {resultConcurrent.Positions.Count}");
            log($"  Lifted:         {resultLegacy.LiftedElementCount}  →  {resultConcurrent.LiftedElementCount}  (Δ = {resultConcurrent.LiftedElementCount - resultLegacy.LiftedElementCount:+0;-0})");
            log($"  Empty Chromo:   {emptyLegacy}  →  {emptyConcurrent}");

            string targetDir = @"C:\Users\Dell\.gemini\antigravity\brain\52c00ca6-6f04-4aee-9ba2-a2ece4d69034";
            if (Directory.Exists(targetDir))
            {
                File.WriteAllLines(Path.Combine(targetDir, $"try4_benchmark_output_crane{craneId}.txt"), logLines);
            }

            // ── Assertions (regression guard) ──────────────────────
            Assert.True(resultConcurrent.CoverageRatio >= resultLegacy.CoverageRatio * 0.95,
                $"Concurrent coverage ({resultConcurrent.CoverageRatio:P2}) should not regress more than 5% below legacy ({resultLegacy.CoverageRatio:P2}).");
        }

        // ══════════════════════════════════════════════════════
        // HELPER: Run optimization with given settings
        // ══════════════════════════════════════════════════════
        private PlacementResult RunOptimization(
            List<PrecastElement> elements,
            List<GreenAreaZone> allowedZones,
            List<GreenAreaZone> forbiddenZones,
            CraneSpecification craneSpec,
            GASettings settings,
            string label)
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
                initializer, assignmentService, fitnessEvaluator,
                selection, crossover, mutation, replacement, constraintEngine);

            var diagnosticsService = new GACsvDiagnosticsService(liftingEngine, configEngine);
            var placementEngine = new CranePlacementEngine(
                gaEngine, assignmentService, coverageEngine, configEngine, diagnosticsService);

            var input = new PlacementInput
            {
                Zones = allowedZones,
                ForbiddenZones = forbiddenZones,
                Elements = new List<PrecastElement>(elements),
                Assignment = new Dictionary<int, List<string>>(),
                CraneSpec = craneSpec,
                Settings = settings,
                ProjectName = $"try4_{label}",
                TotalElementsCount = elements.Count
            };

            var sw = Stopwatch.StartNew();
            var result = placementEngine.RunOptimization(input);
            sw.Stop();
            _output.WriteLine($"  Runtime:        {sw.ElapsedMilliseconds} ms");
            return result;
        }

        // ══════════════════════════════════════════════════════
        // DATA LOADERS
        // ══════════════════════════════════════════════════════

        private List<PrecastElement> LoadElements(string dbPath)
        {
            var elements = new List<PrecastElement>();
            using var conn = new SqliteConnection($"Data Source={dbPath};Mode=ReadOnly");
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT GlobalId, WeightTons, COG_X, COG_Y, COG_Z, BBoxMaxZ - BBoxMinZ, Mark, ElementType FROM ProjectIfcElements WHERE IncludeInGa = 1;";
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                elements.Add(new PrecastElement
                {
                    ElementID = reader.GetString(0),
                    ElementWT = reader.IsDBNull(1) ? 0.0 : reader.GetDouble(1),
                    ElementCOG = new DomainPoint3D(
                        reader.IsDBNull(2) ? 0.0 : reader.GetDouble(2),
                        reader.IsDBNull(3) ? 0.0 : reader.GetDouble(3),
                        reader.IsDBNull(4) ? 0.0 : reader.GetDouble(4)),
                    ElementHeight = reader.IsDBNull(5) ? 0.0 : reader.GetDouble(5),
                    ElementMark = reader.IsDBNull(6) ? null : reader.GetString(6),
                    ElementType = reader.IsDBNull(7) ? string.Empty : reader.GetString(7)
                });
            }
            return elements;
        }

        private (List<GreenAreaZone> allowed, List<GreenAreaZone> forbidden) LoadZones(string dbPath)
        {
            var allowed = new List<GreenAreaZone>();
            var forbidden = new List<GreenAreaZone>();
            using var conn = new SqliteConnection($"Data Source={dbPath};Mode=ReadOnly");
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT VerticesJson, ZoneType FROM SiteZones;";
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                string json = reader.GetString(0);
                string zoneType = reader.GetString(1);
                var vertices = ParseVerticesJson(json);
                ZoneType typeVal = zoneType == "Allowed" ? ZoneType.Allowed : ZoneType.Forbidden;
                var zone = new GreenAreaZone { Vertices = vertices, Type = typeVal };

                if (zoneType == "Allowed")
                    allowed.Add(zone);
                else
                    forbidden.Add(zone);
            }
            return (allowed, forbidden);
        }

        private List<DomainPoint3D> ParseVerticesJson(string json)
        {
            var points = new List<DomainPoint3D>();
            // Minimal JSON array parser for [{x:..., y:..., z:...}, ...]
            json = json.Trim();
            if (!json.StartsWith("[")) return points;

            int i = 0;
            while (i < json.Length)
            {
                int objStart = json.IndexOf('{', i);
                if (objStart < 0) break;
                int objEnd = json.IndexOf('}', objStart);
                if (objEnd < 0) break;

                string obj = json.Substring(objStart + 1, objEnd - objStart - 1);
                double x = 0, y = 0, z = 0;
                foreach (var pair in obj.Split(','))
                {
                    var kv = pair.Split(':');
                    if (kv.Length != 2) continue;
                    string key = kv[0].Trim().Trim('"').ToLowerInvariant();
                    if (double.TryParse(kv[1].Trim(), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double val))
                    {
                        if (key == "x") x = val;
                        else if (key == "y") y = val;
                        else if (key == "z") z = val;
                    }
                }
                points.Add(new DomainPoint3D(x, y, z));
                i = objEnd + 1;
            }
            return points;
        }

        private (CraneSpecification spec, double footprintRadius) LoadCraneSpec(string dbPath, int craneId)
        {
            using var conn = new SqliteConnection($"Data Source={dbPath};Mode=ReadOnly");
            conn.Open();

            // Load crane metadata
            using var craneCmd = conn.CreateCommand();
            craneCmd.CommandText = "SELECT Name, Manufacturer, CraneType, VehicleLength, VehicleWidth, VehicleHeight, OutriggerLength, OutriggerWidth FROM Cranes WHERE Id = @cid;";
            craneCmd.Parameters.AddWithValue("@cid", craneId);
            using var craneReader = craneCmd.ExecuteReader();
            
            string cName = "Unknown";
            string cMfg = "Unknown";
            int cType = 0;
            double vehL = 0, vehW = 0, vehH = 0;
            double? outL = null, outW = null;
            if (craneReader.Read())
            {
                cName = craneReader.GetString(0);
                cMfg = craneReader.GetString(1);
                cType = craneReader.GetInt32(2);
                vehL = craneReader.GetDouble(3);
                vehW = craneReader.GetDouble(4);
                vehH = craneReader.GetDouble(5);
                outL = craneReader.IsDBNull(6) ? null : craneReader.GetDouble(6);
                outW = craneReader.IsDBNull(7) ? null : craneReader.GetDouble(7);
            }
            craneReader.Close();

            var crane = new Crane
            {
                Id = craneId,
                Name = cName,
                Manufacturer = cMfg,
                CraneType = (CraneType)cType,
                VehicleLength = vehL,
                VehicleWidth = vehW,
                VehicleHeight = vehH,
                OutriggerLength = outL,
                OutriggerWidth = outW
            };

            // Load spec
            using var specCmd = conn.CreateCommand();
            specCmd.CommandText = "SELECT Id, MaxRadius, MaxBoomLength, MinRadius, MinBoomLength, CounterWeight, MachineRotation, CraneDerating FROM CraneSpecifications WHERE CraneId = @cid;";
            specCmd.Parameters.AddWithValue("@cid", craneId);
            using var specReader = specCmd.ExecuteReader();
            specReader.Read();
            int specId = specReader.GetInt32(0);
            double maxRadius = specReader.GetDouble(1);
            double maxBoom = specReader.GetDouble(2);
            double minRadius = specReader.GetDouble(3);
            double minBoom = specReader.GetDouble(4);
            double counterWeight = specReader.GetDouble(5);
            double? machineRotation = specReader.IsDBNull(6) ? null : specReader.GetDouble(6);
            double? craneDerating = specReader.IsDBNull(7) ? null : specReader.GetDouble(7);
            specReader.Close();

            double footprint = Math.Max(outL ?? 6.0, outW ?? 6.0) / 2.0;

            // Load load chart
            using var chartCmd = conn.CreateCommand();
            chartCmd.CommandText = "SELECT Radius, BoomLength, Capacity FROM TargetPoints WHERE CraneSpecificationId = @sid;";
            chartCmd.Parameters.AddWithValue("@sid", specId);
            using var chartReader = chartCmd.ExecuteReader();
            var loadChart = new List<TargetPoint>();
            while (chartReader.Read())
            {
                loadChart.Add(new TargetPoint
                {
                    Radius = chartReader.GetDouble(0),
                    BoomLength = chartReader.GetDouble(1),
                    Capacity = chartReader.GetDouble(2)
                });
            }

            var spec = new CraneSpecification
            {
                Id = specId,
                MaxRadius = maxRadius,
                MaxBoomLength = maxBoom,
                MinRadius = minRadius,
                MinBoomLength = minBoom,
                CounterWeight = counterWeight,
                MachineRotation = machineRotation,
                CraneDerating = craneDerating,
                CraneId = craneId,
                Crane = crane,
                LoadChart = loadChart
            };

            return (spec, footprint);
        }

        [Fact]
        public async Task Phase3_Try4_MultiStage_STC800_Verification()
        {
            if (!File.Exists(ProjectDbPath) || !File.Exists(CraneDbPath))
            {
                _output.WriteLine("SKIP: try4 project.db or crane.db not found on this machine.");
                return;
            }

            // 1. Load project data
            var elements = LoadElements(ProjectDbPath);
            var (allowedZones, forbiddenZones) = LoadZones(ProjectDbPath);
            var (craneSpec, footprintRadius) = LoadCraneSpec(CraneDbPath, craneId: 1); // 1 = STC800

            // 2. Build multi-stage input settings matching the prompt:
            // - EnableMultiStageDiscovery = true
            // - MultiStageCoverageTarget = 0.95
            // - MaxCranePositions = 3
            var settings = new GASettings
            {
                PopulationSize = 100,
                MaxGenerations = 100,
                MutationRate = 0.15,
                MutationSigma = 0.05,
                CrossoverRate = 0.8,
                WireLength = 2.0,
                AdditionalWt = 0.0,
                DeratingRatio = 0.9,
                SafetyFactor = 0.95,
                MaxCranePositions = 3,
                RandomSeed = 42,
                CoverageWeight = 10,
                RelocationWeight = 5,
                RadiusPenaltyWeight = 3,
                RadiusQualityPenaltyWeight = 1.0,
                OverweightPenaltyWeight = 10,
                HeightPenaltyWeight = 5,
                MaxRadiusForNormalisation = 50,
                GridStep = 0,
                EarlyStoppingGenerations = 20,
                ConvergenceThresholdPercent = 0,
                RadiusStdDevPenaltyWeight = 2.0,
                BalancePenaltyWeight = 2.0,
                MinimumRelocationDistance = 10.0,
                DuplicatePositionDistance = 2.0 * footprintRadius,
                SimilarPositionPenaltyWeight = 8.0,
                RelocationDistancePenaltyWeight = 1.5,
                MutationExplorationMultiplier = 2.0,
                EnableAdaptiveSigma = false,
                SigmaCoolingExponent = 1.5,
                MinimumMutationSigma = 0.5,
                PositionSamplingAttempts = 80,
                CraneFootprintRadius = footprintRadius,
                EnableProfiling = true,
                EnableConcurrentOptimization = true,

                // Multi-stage settings
                EnableMultiStageDiscovery = true,
                MultiStageCoverageTarget = 0.95,
                AdaptiveRelocationScaleFactor = 0.10
            };

            var configEngine = new CraneConfigurationEngine();
            var liftingEngine = new LiftingCalculationEngine(configEngine);
            var constraintEngine = new ConstraintEngine();
            var coverageEngine = new CoverageEngine();
            var capacityResolver = new LoadChartCapacityResolver();
            var reachabilityMapService = new ReachabilityMapService(constraintEngine, capacityResolver);
            var initializer = new GAInitializer(constraintEngine, reachabilityMapService);
            var assignmentService = new ElementAssignmentService(liftingEngine, configEngine);
            var fitnessEvaluator = new FitnessEvaluator(constraintEngine);
            var selection = new TournamentSelection();
            var crossover = new CrossoverOperator();
            var mutation = new MutationOperator(constraintEngine);
            var replacement = new ReplacementStrategy();

            var gaEngine = new GAEngine(
                initializer, assignmentService, fitnessEvaluator,
                selection, crossover, mutation, replacement, constraintEngine);

            var diagnosticsService = new GACsvDiagnosticsService(liftingEngine, configEngine);
            var placementEngine = new CranePlacementEngine(
                gaEngine, assignmentService, coverageEngine, configEngine, diagnosticsService, reachabilityMapService);

            var orchestrator = new MultiStageGAOrchestrator(placementEngine);

            var input = new PlacementInput
            {
                Zones = allowedZones,
                ForbiddenZones = forbiddenZones,
                Elements = new List<PrecastElement>(elements),
                Assignment = new Dictionary<int, List<string>>(),
                CraneSpec = craneSpec,
                Settings = settings,
                ProjectName = "try4_MultiStageVerification",
                TotalElementsCount = elements.Count
            };

            _output.WriteLine("Starting Multi-Stage GA Orchestration Run...");
            var multiStageResult = await orchestrator.RunAsync(
                input,
                craneSpec.Crane,
                settings,
                settings.MultiStageCoverageTarget,
                CancellationToken.None);

            _output.WriteLine($"Finished. Stages executed: {multiStageResult.StagesExecuted}");
            _output.WriteLine($"Termination reason: {multiStageResult.TerminationReason}");
            for (int i = 0; i < multiStageResult.AllStageResults.Count; i++)
            {
                var stageRes = multiStageResult.AllStageResults[i];
                _output.WriteLine($"Stage {i+1} (MaxPositions={i+1}): Coverage={stageRes.CoverageRatio:P2}, Fitness={stageRes.FitnessScore:F4}, Positions={stageRes.Positions.Count}");
            }
            _output.WriteLine($"Best result coverage: {multiStageResult.BestResult.CoverageRatio:P2}");
            _output.WriteLine($"Best result fitness: {multiStageResult.BestResult.FitnessScore:F4}");
            _output.WriteLine($"Setup count: {multiStageResult.BestResult.Positions.Count}");

            foreach (var pos in multiStageResult.BestResult.Positions)
            {
                _output.WriteLine($"Setup Point: ({pos.X:F3}, {pos.Y:F3}, {pos.Z:F3}) -> Lifted: {pos.NewLiftableCount}");
            }

            // Verify:
            // 1. Triggers Stage 2 (or more): since Stage 1 with MaxCranePositions=1 cannot reach 0.95 coverage, it must run Stage 2.
            Assert.True(multiStageResult.StagesExecuted >= 2, "Orchestrator should escalate to at least Stage 2.");

            // 2. Places 2+ crane positions:
            Assert.True(multiStageResult.BestResult.Positions.Count >= 2, "Best placement should have 2+ positions.");

            // 3. Reaches fitness > 0.65:
            Assert.True(multiStageResult.BestResult.FitnessScore > 0.65, $"Best fitness ({multiStageResult.BestResult.FitnessScore:F4}) should exceed 0.65.");
        }

        private GASettings CloneSettings(GASettings src)
        {
            return new GASettings
            {
                PopulationSize = src.PopulationSize,
                MaxGenerations = src.MaxGenerations,
                MutationRate = src.MutationRate,
                MutationSigma = src.MutationSigma,
                CrossoverRate = src.CrossoverRate,
                WireLength = src.WireLength,
                AdditionalWt = src.AdditionalWt,
                DeratingRatio = src.DeratingRatio,
                SafetyFactor = src.SafetyFactor,
                MaxCranePositions = src.MaxCranePositions,
                RandomSeed = src.RandomSeed,
                CoverageWeight = src.CoverageWeight,
                RelocationWeight = src.RelocationWeight,
                RadiusPenaltyWeight = src.RadiusPenaltyWeight,
                RadiusQualityPenaltyWeight = src.RadiusQualityPenaltyWeight,
                OverweightPenaltyWeight = src.OverweightPenaltyWeight,
                HeightPenaltyWeight = src.HeightPenaltyWeight,
                MaxRadiusForNormalisation = src.MaxRadiusForNormalisation,
                GridStep = src.GridStep,
                EarlyStoppingGenerations = src.EarlyStoppingGenerations,
                ConvergenceThresholdPercent = src.ConvergenceThresholdPercent,
                RadiusStdDevPenaltyWeight = src.RadiusStdDevPenaltyWeight,
                BalancePenaltyWeight = src.BalancePenaltyWeight,
                MinimumRelocationDistance = src.MinimumRelocationDistance,
                DuplicatePositionDistance = src.DuplicatePositionDistance,
                SimilarPositionPenaltyWeight = src.SimilarPositionPenaltyWeight,
                RelocationDistancePenaltyWeight = src.RelocationDistancePenaltyWeight,
                MutationExplorationMultiplier = src.MutationExplorationMultiplier,
                EnableAdaptiveSigma = src.EnableAdaptiveSigma,
                SigmaCoolingExponent = src.SigmaCoolingExponent,
                MinimumMutationSigma = src.MinimumMutationSigma,
                PositionSamplingAttempts = src.PositionSamplingAttempts,
                CraneFootprintRadius = src.CraneFootprintRadius,
                EnableProfiling = src.EnableProfiling,
                EnableConcurrentOptimization = src.EnableConcurrentOptimization
            };
        }
    }
}
