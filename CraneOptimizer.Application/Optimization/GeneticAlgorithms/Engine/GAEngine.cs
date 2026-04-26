using CraneOptimizer.Application.Optimization.GeneticAlgorithms.DTOs;
using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Interfaces;
using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Models;
using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Services;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace CraneOptimizer.Application.Optimization.GeneticAlgorithms.Engine
{
    public class GAEngine : IGeneticAlgorithmEngine
    {
        private readonly IGAInitializer _initializer;
        private readonly IElementAssignmentService _legacyAssignmentService;
        private readonly CraneOptimizer.Application.Services.ElementAssignmentService _greedyAssignmentService;
        private readonly ICraneConstraintService _constraintService;
        private readonly IFitnessEvaluator _fitnessEvaluator;
        private readonly ISelectionStrategy _selection;
        private readonly ICrossoverOperator _crossover;
        private readonly IMutationOperator _mutation;
        private readonly IReplacementStrategy _replacement;

        public GAEngine(
            IGAInitializer initializer,
            IElementAssignmentService legacyAssignmentService,
            CraneOptimizer.Application.Services.ElementAssignmentService greedyAssignmentService,
            ICraneConstraintService constraintService,
            IFitnessEvaluator fitnessEvaluator,
            ISelectionStrategy selection,
            ICrossoverOperator crossover,
            IMutationOperator mutation,
            IReplacementStrategy replacement)
        {
            _initializer = initializer;
            _legacyAssignmentService = legacyAssignmentService;
            _greedyAssignmentService = greedyAssignmentService;
            _constraintService = constraintService;
            _fitnessEvaluator = fitnessEvaluator;
            _selection = selection;
            _crossover = crossover;
            _mutation = mutation;
            _replacement = replacement;
        }

        private void Evaluate(CraneSolution solution, PlacementInput input)
        {
            // 1️⃣ Legacy assignment & evaluation
            var evaluation = _legacyAssignmentService.EvaluateAssignment(solution, input);

            // 2️⃣ Base fitness
            var fitnessResult = _fitnessEvaluator.EvaluateFitness(
                solution,
                evaluation,
                input.Settings);

            double fitness = fitnessResult.FitnessScore;

            // 3️⃣ Greedy assignment optimization
            var greedyAssignment = _greedyAssignmentService.Assign(solution.Chromosome.CranePositions, input);
            
            // Reward coverage
            fitness += greedyAssignment.TotalAssigned * 100.0;

            // Penalise idle cranes
            fitness -= greedyAssignment.UnusedCraneCount * 50.0;

            // FIX 1 — penalise constraint violations per crane
            foreach (var pos in solution.Chromosome.CranePositions)
            {
                var constraint = _constraintService.Validate(pos, input);
                fitness -= constraint.NormalizedPenalty;
            }

            solution.Fitness = fitness;
        }

        public CraneSolution Run(PlacementInput input)
        {
            int stagnantGenerations = 0;
            double bestFitness = double.MinValue;
            CraneSolution bestSolution = null;
            // Initialize deterministic random manager
            GARandomManager.Initialize(input.Settings.RandomSeed);

            // 1️⃣ Initialize population
            var population = _initializer.InitializePopulation(
                input.AllowedZones,
                input.Elements,
                input.Settings.PopulationSize,
                input.Settings.MaxCranePositions,
                input.Settings);

            for (int generation = 0; generation < input.Settings.MaxGenerations; generation++)
            {
                // Parallel evaluation
                Parallel.ForEach(
                    population.Individuals,
                    new ParallelOptions { MaxDegreeOfParallelism = System.Environment.ProcessorCount },
                    individual => Evaluate(individual, input));

                // 2️⃣ Track best solution
                var currentBest = population.Individuals
                    .OrderByDescending(s => s.Fitness)
                    .First();

                if (currentBest.Fitness > bestFitness)
                {
                    bestFitness = currentBest.Fitness;
                    bestSolution = currentBest.DeepCopy();
                    stagnantGenerations = 0;
                }
                else
                {
                    stagnantGenerations++;
                }

                // 3️⃣ Early stopping
                if (stagnantGenerations >= input.Settings.EarlyStoppingGenerations)
                    break;

                // 4️⃣ Selection
                var parents = _selection.SelectParents(
                    population,
                    population.Individuals.Count);

                // 5️⃣ Crossover
                var children = new List<CraneSolution>();

                for (int i = 0; i < parents.Count - 1; i += 2)
                {
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

                // 6️⃣ Mutation
                foreach (var child in children)
                {
                    if (GARandomManager.Instance.NextDouble() < input.Settings.MutationRate)
                        _mutation.Mutate(child, input.AllowedZones);
                }

                // Parallel evaluation for children
                Parallel.ForEach(
                    children,
                    new ParallelOptions { MaxDegreeOfParallelism = System.Environment.ProcessorCount },
                    child => Evaluate(child, input));

                // 7️⃣ Replacement
                population = _replacement.Replace(population, children);
            }

            // 8️⃣ Return best solution
            return bestSolution;
        }
    }
}