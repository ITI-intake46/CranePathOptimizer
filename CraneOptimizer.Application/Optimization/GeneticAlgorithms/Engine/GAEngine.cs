using CraneOptimizer.Application.Optimization.GeneticAlgorithms.DTOs;
using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Interfaces;
using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Models;
using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Services;
using CraneOptimizer.Application.Services;
using CraneOptimizer.Domain.Assignment;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace CraneOptimizer.Application.Optimization.GeneticAlgorithms.Engine
{
    public class GAEngine : IGeneticAlgorithmEngine
    {
        private readonly IGAInitializer _initializer;
        private readonly GreedyAssignmentEngine _assignmentEngine;
        private readonly IFitnessEvaluator _fitnessEvaluator;
        private readonly ISelectionStrategy _selection;
        private readonly ICrossoverOperator _crossover;
        private readonly IMutationOperator _mutation;
        private readonly IReplacementStrategy _replacement;

        public GAEngine(
            IGAInitializer initializer,
            GreedyAssignmentEngine assignmentEngine,
            IFitnessEvaluator fitnessEvaluator,
            ISelectionStrategy selection,
            ICrossoverOperator crossover,
            IMutationOperator mutation,
            IReplacementStrategy replacement)
        {
            _initializer = initializer;
            _assignmentEngine = assignmentEngine;
            _fitnessEvaluator = fitnessEvaluator;
            _selection = selection;
            _crossover = crossover;
            _mutation = mutation;
            _replacement = replacement;
        }

        private void Evaluate(CraneSolution solution, PlacementInput input)
        {
            // 1️⃣ Unified assignment & evaluation
            // This single call handles user overrides, greedy mapping, and constraint validation.
            var assignment = _assignmentEngine.Evaluate(solution, input);

            // 2️⃣ Base fitness calculation
            // The evaluator handles coverage, failures, and subtracts the assignment's TotalConstraintPenalty.
            var fitnessResult = _fitnessEvaluator.EvaluateFitness(
                solution,
                assignment,
                input.Settings);

            double fitness = fitnessResult.FitnessScore;

            // 3️⃣ Evolutionary rewards
            // Reward high element coverage
            fitness += assignment.TotalAssigned * 100.0;

            // Penalise unused cranes to encourage compact solutions
            fitness -= assignment.UnusedCraneCount * 50.0;

            solution.Fitness = fitness;
        }

        public CraneSolution Run(PlacementInput input)
        {
            int stagnantGenerations = 0;
            double bestFitness = double.MinValue;
            CraneSolution bestSolution = null;
            
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
                Parallel.ForEach(
                    population.Individuals,
                    new ParallelOptions { MaxDegreeOfParallelism = System.Environment.ProcessorCount },
                    individual => Evaluate(individual, input));

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

                if (stagnantGenerations >= input.Settings.EarlyStoppingGenerations)
                    break;

                var parents = _selection.SelectParents(
                    population,
                    population.Individuals.Count);

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

                foreach (var child in children)
                {
                    if (GARandomManager.Instance.NextDouble() < input.Settings.MutationRate)
                        _mutation.Mutate(child, input.AllowedZones);
                }

                Parallel.ForEach(
                    children,
                    new ParallelOptions { MaxDegreeOfParallelism = System.Environment.ProcessorCount },
                    child => Evaluate(child, input));

                population = _replacement.Replace(population, children);
            }

            return bestSolution;
        }
    }
}