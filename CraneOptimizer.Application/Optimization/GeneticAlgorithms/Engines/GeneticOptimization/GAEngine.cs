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
using CraneOptimizer.Application.Optimization.Utilities;

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
                    input.Settings);

                for (int generation = 0; generation < input.Settings.MaxGenerations; generation++)
                {
                    using (new GAProfiler.ProfileScope("GAEngine.GenerationLoop"))
                    {
                        if (GAProfiler.Enabled)
                        {
                            System.Threading.Interlocked.Increment(ref GAProfiler.ActualGenerationLoopCount);
                        }
                        cancellationToken.ThrowIfCancellationRequested();

                        foreach (var solution in population.Individuals)
                        {
                            cancellationToken.ThrowIfCancellationRequested();

                            var evaluation = _assignmentService.EvaluateAssignment(
                                solution,
                                input.Elements,
                                input.Assignment,
                                input.CraneSpec,
                                input.Settings);

                            var fitness = _fitnessEvaluator.EvaluateFitness(
                                solution,
                                evaluation,
                                input.Settings);

                            solution.Fitness = fitness.FitnessScore;
                            solution.CoverageRatio = fitness.CoverageRatio;
                        }

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
                                var child1 = _crossover.Crossover(parents[i], parents[i + 1]);
                                var child2 = _crossover.Crossover(parents[i + 1], parents[i]);
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
                                _mutation.Mutate(child, input.Zones, input.ForbiddenZones, input.Settings);

                            SanitizeSolution(child, input);
                        }

                        foreach (var child in children)
                        {
                            cancellationToken.ThrowIfCancellationRequested();

                            var evaluation = _assignmentService.EvaluateAssignment(
                                child,
                                input.Elements,
                                input.Assignment,
                                input.CraneSpec,
                                input.Settings);

                            var fitness = _fitnessEvaluator.EvaluateFitness(
                                child,
                                evaluation,
                                input.Settings);

                            child.Fitness = fitness.FitnessScore;
                            child.CoverageRatio = fitness.CoverageRatio;
                        }

                        population = _replacement.Replace(population, children);
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
        /// Finds a simple valid fallback point from allowed-zone centroids.
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
            }

            return null;
        }
    }
}
