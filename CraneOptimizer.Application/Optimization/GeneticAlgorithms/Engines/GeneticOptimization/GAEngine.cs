using CraneOptimizer.Application.Optimization.GeneticAlgorithms.DTOs;
using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Interfaces;
using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Models;
using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Settings;
using CraneOptimizer.Domain.Common;
using CraneOptimizer.Domain.Schemas;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CraneOptimizer.Application.Optimization.Utilities;
using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Services;

namespace CraneOptimizer.Application.Optimization.GeneticAlgorithms.Engines.GeneticOptimization
{
    /// <summary>
    /// Main orchestration engine for the Genetic Algorithm.
    /// Coordinates all GA components.
    /// </summary>
    public class GAEngine : IGeneticAlgorithmEngine
    {
        private readonly IGAInitializer _initializer;
        private readonly IElementAssignmentService _assignmentService;
        private readonly IFitnessEvaluator _fitnessEvaluator;
        private readonly ISelectionStrategy _selection;
        private readonly ICrossoverOperator _crossover;
        private readonly IMutationOperator _mutation;
        private readonly IReplacementStrategy _replacement;
        private readonly IConstraintEngine _constraintEngine;

        public GAEngine(
            IGAInitializer initializer,
            IElementAssignmentService assignmentService,
            IFitnessEvaluator fitnessEvaluator,
            ISelectionStrategy selection,
            ICrossoverOperator crossover,
            IMutationOperator mutation,
            IReplacementStrategy replacement,
            IConstraintEngine constraintEngine)
        {
            _initializer = initializer;
            _assignmentService = assignmentService;
            _fitnessEvaluator = fitnessEvaluator;
            _selection = selection;
            _crossover = crossover;
            _mutation = mutation;
            _replacement = replacement;
            _constraintEngine = constraintEngine;
        }

        public CraneSolution Run(PlacementInput input, CancellationToken cancellationToken = default)
        {
            using (new GAProfiler.ProfileScope("GAEngine.Run"))
            {
                int stagnantGenerations = 0;
                double bestFitness = double.MinValue;
                CraneSolution bestSolution = null!;

                GARandomManager.Initialize(input.Settings.RandomSeed);

                var population = _initializer.InitializePopulation(
                    input.Zones,
                    input.ForbiddenZones,
                    input.Elements,
                    input.Settings.PopulationSize,
                    input.Settings.MaxCranePositions,
                    input.Settings,
                    input.CraneSpec);

                // ── Phase 1 Performance: Build pre-indexed data structures ──
                var loadChartIndex = new LoadChartIndex(input.CraneSpec);
                var elementCache = new ElementStaticCache(input.Elements, input.Settings);

                var parallelOptions = new ParallelOptions
                {
                    CancellationToken = cancellationToken
                };

                for (int generation = 0; generation < input.Settings.MaxGenerations; generation++)
                {
                    using (new GAProfiler.ProfileScope("GAEngine.GenerationLoop"))
                    {
                        if (GAProfiler.Enabled)
                        {
                            System.Threading.Interlocked.Increment(ref GAProfiler.ActualGenerationLoopCount);
                        }
                        cancellationToken.ThrowIfCancellationRequested();

                        Parallel.ForEach(population.Individuals, parallelOptions, solution =>
                        {
                            EvaluateSolution(solution, input, loadChartIndex, elementCache);
                        });

                        var currentBest = population.Individuals
                            .OrderByDescending(s => s.Fitness)
                            .First();

                        if (currentBest.Fitness > bestFitness)
                        {
                            double improvementPercent = bestFitness <= 0
                                ? double.MaxValue
                                : ((currentBest.Fitness - bestFitness) / Math.Abs(bestFitness)) * 100.0;

                            bestFitness = currentBest.Fitness;
                            bestSolution = currentBest.DeepCopy();

                            if (input.Settings.ConvergenceThresholdPercent > 0
                                && improvementPercent < input.Settings.ConvergenceThresholdPercent)
                            {
                                stagnantGenerations++;
                            }
                            else
                            {
                                stagnantGenerations = 0;
                            }
                        }
                        else
                        {
                            stagnantGenerations++;
                        }

                        if (GAProfiler.Enabled)
                        {
                            int stageIdx = input.Settings.StageIndex;
                            var logEntry = new GAProfiler.GenerationLogEntry
                            {
                                RunId = input.Settings.RunId ?? string.Empty,
                                OptimizationRunId = input.Settings.OptimizationRunId ?? string.Empty,
                                StageRunId = input.Settings.StageRunId ?? string.Empty,
                                StageIndex = stageIdx,
                                GenerationIndex = generation,
                                BestFitness = currentBest.Fitness,
                                AverageFitness = population.Individuals.Average(s => s.Fitness),
                                BestCoverageRatio = currentBest.CoverageRatio,
                                AverageCoverageRatio = population.Individuals.Average(s => s.CoverageRatio),
                                BestPositionCount = currentBest.Chromosome.CranePositions.Count,
                                Count1Position = population.Individuals.Count(s => s.Chromosome.CranePositions.Count == 1),
                                Count2Positions = population.Individuals.Count(s => s.Chromosome.CranePositions.Count == 2),
                                Count3Positions = population.Individuals.Count(s => s.Chromosome.CranePositions.Count == 3),
                                MutationSuccessRate = 0.0,
                                InvalidMutationCount = 0,
                                StagnationCounter = stagnantGenerations
                            };
                            GAProfiler.GenerationLogs.Add(logEntry);
                        }

                        if (stagnantGenerations >= input.Settings.EarlyStoppingGenerations)
                            break;

                        var parents = _selection.SelectParents(
                            population,
                            population.Individuals.Count);

                        var children = new List<CraneSolution>();

                        for (int i = 0; i < parents.Count - 1; i += 2)
                        {
                            cancellationToken.ThrowIfCancellationRequested();

                            if (GARandomManager.Instance.NextDouble() < input.Settings.CrossoverRate)
                            {
                                var child1 = _crossover.Crossover(parents[i], parents[i + 1], input.Settings);
                                var child2 = _crossover.Crossover(parents[i + 1], parents[i], input.Settings);
                                SanitizeSolution(child1, input);
                                SanitizeSolution(child2, input);
                                children.Add(child1);
                                children.Add(child2);
                            }
                            else
                            {
                                var child1 = parents[i].DeepCopy();
                                var child2 = parents[i + 1].DeepCopy();
                                SanitizeSolution(child1, input);
                                SanitizeSolution(child2, input);
                                children.Add(child1);
                                children.Add(child2);
                            }
                        }

                        foreach (var child in children)
                        {
                            cancellationToken.ThrowIfCancellationRequested();

                            if (GARandomManager.Instance.NextDouble() < input.Settings.MutationRate)
                                _mutation.Mutate(child, input.Zones, input.ForbiddenZones, input.Settings, generation);

                            SanitizeSolution(child, input);
                        }

                        Parallel.ForEach(children, parallelOptions, child =>
                        {
                            EvaluateSolution(child, input, loadChartIndex, elementCache);
                        });

                        population = _replacement.Replace(population, children);
                    }
                }

                if (GAProfiler.Enabled)
                {
                    if (stagnantGenerations >= input.Settings.EarlyStoppingGenerations)
                    {
                        if (input.Settings.ConvergenceThresholdPercent > 0)
                        {
                            GAProfiler.StoppedByConvergence = true;
                        }
                    }
                    else
                    {
                        GAProfiler.StoppedByMaxGenerations = true;
                    }
                }

                return bestSolution;
            }
        }

        /// <summary>
        /// Cleans a generated child solution before evaluation.
        /// Inputs are a child solution and the current placement input; the output is an
        /// in-place chromosome update that removes invalid, forbidden, or near-duplicate
        /// setup points while preserving at least one valid fallback point where possible.
        /// </summary>
        private void SanitizeSolution(CraneSolution solution, PlacementInput input)
        {
            var filtered = _constraintEngine.FilterDistinctValidPoints(
                solution.Chromosome.CranePositions,
                input.Zones,
                input.ForbiddenZones,
                input.Settings);

            if (filtered.Count == 0)
            {
                var fallback = FindFirstValidZoneCentroid(input.Zones, input.ForbiddenZones, input.Settings);
                if (fallback != null)
                    filtered.Add(fallback);
            }

            solution.Chromosome.CranePositions = filtered;
        }

        /// <summary>
        /// Finds a valid fallback point from allowed zones.
        /// This keeps GA children evaluable if crossover or mutation removes every point.
        /// </summary>
        private DomainPoint3D? FindFirstValidZoneCentroid(
            IReadOnlyList<GreenAreaZone> zones,
            IReadOnlyList<GreenAreaZone> forbiddenZones,
            GASettings settings)
        {
            foreach (var zone in zones)
            {
                if (zone.Vertices.Count == 0)
                    continue;

                var candidate = new DomainPoint3D(
                    zone.Vertices.Average(vertex => vertex.X),
                    zone.Vertices.Average(vertex => vertex.Y),
                    zone.Vertices.Average(vertex => vertex.Z));

                if (_constraintEngine.IsValidSetupPoint(candidate, zones, forbiddenZones, settings))
                    return candidate;

                if (TryFindFirstValidGridPoint(zone, forbiddenZones, settings, out var gridCandidate))
                    return gridCandidate;
            }

            return null;
        }

        private bool TryFindFirstValidGridPoint(
            GreenAreaZone zone,
            IReadOnlyList<GreenAreaZone> forbiddenZones,
            GASettings settings,
            out DomainPoint3D point)
        {
            var bbox = SpatialMath.BoundingBox(zone.Vertices);
            int divisions = Math.Max(4, settings.PositionSamplingAttempts);
            double stepX = (bbox.maxX - bbox.minX) / divisions;
            double stepY = (bbox.maxY - bbox.minY) / divisions;

            if (stepX <= 0 || stepY <= 0)
            {
                point = new DomainPoint3D();
                return false;
            }

            for (int ix = 0; ix <= divisions; ix++)
            {
                for (int iy = 0; iy <= divisions; iy++)
                {
                    var candidate = new DomainPoint3D(
                        bbox.minX + (ix * stepX),
                        bbox.minY + (iy * stepY),
                        zone.Vertices.Average(vertex => vertex.Z));

                    if (_constraintEngine.IsValidSetupPoint(candidate, new[] { zone }, forbiddenZones, settings))
                    {
                        point = candidate;
                        return true;
                    }
                }
            }

            point = new DomainPoint3D();
            return false;
        }

        private void EvaluateSolution(
            CraneSolution solution,
            PlacementInput input,
            LoadChartIndex loadChartIndex,
            ElementStaticCache elementCache)
        {
            var evaluation = _assignmentService.EvaluateAssignment(
                solution,
                input.Elements,
                input.Assignment,
                input.CraneSpec,
                input.Settings,
                loadChartIndex,
                elementCache);

            var fitness = _fitnessEvaluator.EvaluateFitness(
                solution,
                evaluation,
                input.Settings);

            solution.Fitness = fitness.FitnessScore;
            solution.CoverageRatio = fitness.CoverageRatio;
        }
    }
}
