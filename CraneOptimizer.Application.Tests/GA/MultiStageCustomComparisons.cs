using Xunit;
using Xunit.Abstractions;
using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Services;
using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Interfaces;
using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Settings;
using CraneOptimizer.Application.Optimization.GeneticAlgorithms.DTOs;
using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Models;
using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Engines.CraneConfiguration;
using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Engines.LiftingCalculation;
using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Engines.Constraints;
using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Engines.Coverage;
using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Engines.GeneticOptimization;
using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Engines.PlacementOrchestration;
using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Operators;
using CraneOptimizer.Domain.Common;
using CraneOptimizer.Domain.Entities;
using CraneOptimizer.Domain.Schemas;
using CraneOptimizer.Domain.Schemas.Enums;
using CraneOptimizer.Domain.Entities.Enums;
using Microsoft.Data.Sqlite;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CraneOptimizer.Application.Optimization.Utilities;

namespace CraneOptimizer.Application.Tests.GA
{
    public class MultiStageCustomComparisons
    {
        private readonly ITestOutputHelper _output;

        private const string ProjectDbPath = @"F:\06-ITI\00-Project\tryProjects\rrr\project.db";
        private const string ZoneDbPath = @"F:\06-ITI\00-Project\tryProjects\auditmodel\project.db";
        private const string CraneDbPath = @"F:\06-ITI\00-Project\Main Project\CraneOptimizer\CraneOptimizer.UI\bin\Debug\net8.0-windows\Data\crane.db";

        public MultiStageCustomComparisons(ITestOutputHelper output)
        {
            _output = output;
        }

        [Fact]
        public async Task ExecuteMultiStageComparisons()
        {
            if (!File.Exists(ProjectDbPath) || !File.Exists(ZoneDbPath) || !File.Exists(CraneDbPath))
            {
                throw Xunit.Sdk.SkipException.ForSkip("Required benchmark databases are not available on this machine.");
            }

            // 1. Load project data
            var elements = LoadElements(ProjectDbPath);
            var (allowedZones, forbiddenZones) = LoadZones(ZoneDbPath);
            var (craneSpec, footprintRadius) = LoadCraneSpec(CraneDbPath, craneId: 1); // 1 = STC800

            _output.WriteLine($"Loaded {elements.Count} elements, allowed zones = {allowedZones.Count}, forbidden zones = {forbiddenZones.Count}.");

            int[] positionsToTest = new int[] { 3, 4, 5, 6 };

            foreach (var maxPositions in positionsToTest)
            {
                _output.WriteLine("\n==================================================");
                _output.WriteLine($"TESTING: MaxCranePositions = {maxPositions}");
                _output.WriteLine("==================================================");

                var settings = new GASettings
                {
                    PopulationSize = 60,
                    MaxGenerations = 80,
                    MutationRate = 0.10,
                    MutationSigma = 0.005,
                    CrossoverRate = 0.80,
                    WireLength = 2.0,
                    AdditionalWt = 0.0,
                    DeratingRatio = 0.9,
                    SafetyFactor = 0.95,
                    MaxCranePositions = maxPositions,
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
                    AdaptiveRelocationScaleFactor = 0.40,
                    UseHungarianAssignment = true
                };

                var configEngine = new CraneConfigurationEngine();
                var liftingEngine = new LiftingCalculationEngine(configEngine);
                var constraintEngine = new ConstraintEngine();
                var coverageEngine = new CoverageEngine();
                var capacityResolver = new LoadChartCapacityResolver();
                var reachabilityMapService = new ReachabilityMapService(constraintEngine, capacityResolver);
                var initializer = new GAInitializer(constraintEngine, reachabilityMapService);
                var greedyService = new ElementAssignmentService(liftingEngine, configEngine, capacityResolver);
                var hungarianService = new HungarianAssignmentService(liftingEngine, configEngine, capacityResolver);
                var assignmentService = new RoutingAssignmentService(greedyService, hungarianService);
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
                    ProjectName = "rrr",
                    TotalElementsCount = elements.Count
                };

                var multiStageResult = await orchestrator.RunAsync(
                    input,
                    craneSpec.Crane,
                    settings,
                    settings.MultiStageCoverageTarget,
                    CancellationToken.None);

                _output.WriteLine($"Stages executed: {multiStageResult.StagesExecuted}");
                _output.WriteLine($"Termination reason: {multiStageResult.TerminationReason}");
                for (int i = 0; i < multiStageResult.AllStageResults.Count; i++)
                {
                    var stageRes = multiStageResult.AllStageResults[i];
                    _output.WriteLine($"  Stage {i + 1} (MaxPositions={i + 1}): Coverage={stageRes.CoverageRatio:P2} ({stageRes.LiftedElementCount}/{elements.Count}), Fitness={stageRes.FitnessScore:F4}, Positions={stageRes.Positions.Count}");
                }
                _output.WriteLine($"Best result coverage: {multiStageResult.BestResult.CoverageRatio:P2}");
                _output.WriteLine($"Best result fitness: {multiStageResult.BestResult.FitnessScore:F4}");
                _output.WriteLine($"Setup count: {multiStageResult.BestResult.Positions.Count}");

                foreach (var pos in multiStageResult.BestResult.Positions)
                {
                    _output.WriteLine($"  Setup Point: ({pos.X:F3}, {pos.Y:F3}, {pos.Z:F3}) -> Unique-New Lifted: {pos.NewLiftableCount}");
                    
                    // Let's also find total elements liftable from this point (without deduplication/exclusive assignment)
                    int totalLiftableFromPoint = 0;
                    var posPoint = new DomainPoint3D(pos.X, pos.Y, pos.Z);
                    foreach (var el in elements)
                    {
                        double radius = liftingEngine.CalculateRadius(posPoint, el, settings);
                        double boomLength = configEngine.CalculateRequiredBoomLength(posPoint, el, settings);
                        double requiredCapacity = liftingEngine.CalculateRequiredWeight(el, settings);
                        var resolveResult = capacityResolver.Resolve(craneSpec, radius, boomLength, requiredCapacity, settings);
                        if (resolveResult.LiftStatus == LiftStatus.Liftable)
                        {
                            totalLiftableFromPoint++;
                        }
                    }
                    _output.WriteLine($"    Total liftable from this point (geometrically/capacity): {totalLiftableFromPoint}");
                }
            }
        }

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
        public async Task ExecuteSequentialFallbackComparisons()
        {
            if (!File.Exists(ProjectDbPath) || !File.Exists(ZoneDbPath) || !File.Exists(CraneDbPath))
            {
                throw Xunit.Sdk.SkipException.ForSkip("Required benchmark databases are not available on this machine.");
            }

            // 1. Load project data
            var elements = LoadElements(ProjectDbPath);
            var (allowedZones, forbiddenZones) = LoadZones(ZoneDbPath);
            var (craneSpec, footprintRadius) = LoadCraneSpec(CraneDbPath, craneId: 1); // 1 = STC800

            int[] positionsToTest = new int[] { 3, 4, 6 };

            foreach (var maxPositions in positionsToTest)
            {
                _output.WriteLine("\n==================================================");
                _output.WriteLine($"SEQUENTIAL FALLBACK TESTING: MaxCranePositions = {maxPositions}");
                _output.WriteLine("==================================================");

                var settings = new GASettings
                {
                    PopulationSize = 60,
                    MaxGenerations = 80,
                    MutationRate = 0.10,
                    MutationSigma = 0.005,
                    CrossoverRate = 0.80,
                    WireLength = 2.0,
                    AdditionalWt = 0.0,
                    DeratingRatio = 0.9,
                    SafetyFactor = 0.95,
                    MaxCranePositions = maxPositions,
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

                    // Target settings:
                    EnableConcurrentOptimization = false,
                    EnableMultiStageDiscovery = false
                };

                var configEngine = new CraneConfigurationEngine();
                var liftingEngine = new LiftingCalculationEngine(configEngine);
                var constraintEngine = new ConstraintEngine();
                var coverageEngine = new CoverageEngine();
                var capacityResolver = new LoadChartCapacityResolver();
                var reachabilityMapService = new ReachabilityMapService(constraintEngine, capacityResolver);
                var initializer = new GAInitializer(constraintEngine, reachabilityMapService);
                var greedyService = new ElementAssignmentService(liftingEngine, configEngine, capacityResolver);
                var hungarianService = new HungarianAssignmentService(liftingEngine, configEngine, capacityResolver);
                var assignmentService = new RoutingAssignmentService(greedyService, hungarianService);
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

                var input = new PlacementInput
                {
                    Zones = allowedZones,
                    ForbiddenZones = forbiddenZones,
                    Elements = new List<PrecastElement>(elements),
                    Assignment = new Dictionary<int, List<string>>(),
                    CraneSpec = craneSpec,
                    Settings = settings,
                    ProjectName = "rrr",
                    TotalElementsCount = elements.Count
                };

                var runResult = await Task.Run(() => placementEngine.RunOptimization(input, CancellationToken.None));

                _output.WriteLine($"Actual maxPoints configured at runtime: {Math.Clamp(maxPositions, 1, 10)}");
                _output.WriteLine($"Greedy iterations attempted: {GAProfiler.GreedySetCoverIterationCount}");
                _output.WriteLine($"Returned setup count: {runResult.Positions.Count}");
                
                int totalLifted = runResult.Positions.Sum(p => p.NewLiftableCount);
                _output.WriteLine($"Lifted count: {totalLifted}");
                _output.WriteLine($"Coverage: {runResult.CoverageRatio:P2}");

                for (int i = 0; i < runResult.Positions.Count; i++)
                {
                    var pos = runResult.Positions[i];
                    _output.WriteLine($"  Setup Point {i + 1}: ({pos.X:F3}, {pos.Y:F3}, {pos.Z:F3}) -> Unique-New Lifted: {pos.NewLiftableCount}");
                }
            }
        }
    }
}
