using CraneOptimizer.Application.Optimization.GeneticAlgorithms.DTOs;
using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Interfaces;
using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Models;
using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Services;
using System.Threading;

namespace CraneOptimizer.Application.Optimization.GeneticAlgorithms.Engine
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

        public GAEngine(
            IGAInitializer initializer,
            IElementAssignmentService assignmentService,
            IFitnessEvaluator fitnessEvaluator,
            ISelectionStrategy selection,
            ICrossoverOperator crossover,
            IMutationOperator mutation,
            IReplacementStrategy replacement)
        {
            _initializer = initializer;
            _assignmentService = assignmentService;
            _fitnessEvaluator = fitnessEvaluator;
            _selection = selection;
            _crossover = crossover;
            _mutation = mutation;
            _replacement = replacement;
        }

        public CraneSolution Run(PlacementInput input, CancellationToken cancellationToken = default)
        {
            int stagnantGenerations = 0;
            double bestFitness = double.MinValue;
            CraneSolution bestSolution = null!;

            GARandomManager.Initialize(input.Settings.RandomSeed);

            var population = _initializer.InitializePopulation(
                input.Zones,
                input.Elements,
                input.Settings.PopulationSize,
                input.Settings.MaxCranePositions,
                input.Settings);

            for (int generation = 0; generation < input.Settings.MaxGenerations; generation++)
            {
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
                        children.Add(child1);
                        children.Add(child2);
                    }
                    else
                    {
                        children.Add(parents[i].DeepCopy());
                        children.Add(parents[i + 1].DeepCopy());
                    }
                }

                foreach (var child in children)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    if (GARandomManager.Instance.NextDouble() < input.Settings.MutationRate)
                        _mutation.Mutate(child, input.Zones, input.Settings);
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
                }

                population = _replacement.Replace(population, children);
            }

            return bestSolution;
        }
    }
}
