using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Interfaces;
using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Models;
using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Settings;
using CraneOptimizer.Domain.Entities;
using CraneOptimizer.Domain.GreenAreaEntities;
using CraneOptimizer.Domain.IfcEntities;
using System;

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

        public CraneSolution Run(
            List<CranePlacementZone> zones,
            List<PrecastElement> elements,
            Dictionary<int, List<string>> assignment,
            CraneSpecification craneSpec,
            GASettings settings)
        {
            int stagnantGenerations = 0;
            double bestFitness = double.MinValue;
            CraneSolution bestSolution = null;
            // 1️⃣ Initialize population
            var population = _initializer.InitializePopulation(
                zones,
                elements,
                settings.PopulationSize,
                settings.MaxCranePositions);

            for (int generation = 0; generation < settings.MaxGenerations; generation++)
            {
                foreach (var solution in population.Individuals)
                {
                    // 2️⃣ Evaluate assignment
                    var evaluation = _assignmentService.EvaluateAssignment(
                        solution,
                        elements,
                        assignment,
                        craneSpec,
                        settings);

                    // 3️⃣ Evaluate fitness
                    var fitness = _fitnessEvaluator.EvaluateFitness(
                        solution,
                        evaluation,
                        settings);

                    solution.Fitness = fitness.FitnessScore;
                }
                // 2️⃣ Track best solution
                var currentBest = population.Individuals
                    .OrderByDescending(s => s.Fitness)
                    .First();

                if (currentBest.Fitness > bestFitness)
                {
                    bestFitness = currentBest.Fitness;
                    bestSolution = currentBest;
                    stagnantGenerations = 0;
                }
                else
                {
                    stagnantGenerations++;
                }

                // 3️⃣ Early stopping
                if (stagnantGenerations >= settings.EarlyStoppingGenerations)
                    break;

                // 4️⃣ Selection
                var parents = _selection.SelectParents(
                    population,
                    population.Individuals.Count);

                // 5️⃣ Crossover
                var children = new List<CraneSolution>();

                for (int i = 0; i < parents.Count - 1; i += 2)
                {
                    var child = _crossover.Crossover(parents[i], parents[i + 1]);
                    children.Add(child);
                }

                // 6️⃣ Mutation
                foreach (var child in children)
                {
                    if (Random.Shared.NextDouble() < settings.MutationRate)
                        _mutation.Mutate(child, zones);
                }
                foreach (var child in children)
                {
                    var evaluation = _assignmentService.EvaluateAssignment(
                        child,
                        elements,
                        assignment,
                        craneSpec,
                        settings);

                    var fitness = _fitnessEvaluator.EvaluateFitness(
                        child,
                        evaluation,
                        settings);

                    child.Fitness = fitness.FitnessScore;
                }

                // 7️⃣ Replacement
                population = _replacement.Replace(population, children);
            }

            // 8️⃣ Return best solution
            return bestSolution;
        }
    }
}