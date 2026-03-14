using CraneOptimizer.Application.Optimization.GeneticAlgorithm.Models;
using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Models;
using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Services;
using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Settings;
using CraneOptimizer.Domain.Entities;
using CraneOptimizer.Domain.GreenAreaEntities;
using CraneOptimizer.Domain.IfcEntities;

namespace CraneOptimizer.Application.Optimization.GeneticAlgorithms.Engine
{
    /// <summary>
    /// Implementation of the genetic algorithm engine.
    /// Responsible for coordinating all GA operations.
    /// </summary>
    public class GeneticAlgorithmEngine : IGeneticAlgorithmEngine
    {
        private readonly IGAInitializer _initializer;
        private readonly IFitnessEvaluator _fitnessEvaluator;
        private readonly ISelectionStrategy _selection;
        private readonly ICrossoverOperator _crossover;
        private readonly IMutationOperator _mutation;
        private readonly IReplacementStrategy _replacement;

        public GeneticAlgorithmEngine(
            IGAInitializer initializer,
            IFitnessEvaluator fitnessEvaluator,
            ISelectionStrategy selection,
            ICrossoverOperator crossover,
            IMutationOperator mutation,
            IReplacementStrategy replacement)
        {
            _initializer = initializer;
            _fitnessEvaluator = fitnessEvaluator;
            _selection = selection;
            _crossover = crossover;
            _mutation = mutation;
            _replacement = replacement;
        }

        /// <summary>
        /// Executes the genetic algorithm and returns the best crane solution.
        /// </summary>
        public CraneSolution Run(List<CranePlacementZone> zones, List<PrecastElement> elements, CraneSpecification craneSpec, GASettings settings)
        {
            // 1️⃣ Initialize first population
            Population population = _initializer.InitializePopulation(zones, settings.PopulationSize);

            // 2️⃣ Evaluate initial population fitness
            foreach (var individual in population.Individuals)
            {
                individual.Fitness =
                    _fitnessEvaluator.EvaluateFitness(individual, elements, craneSpec);
            }

            // 3️⃣ GA main loop
            for (int generation = 0; generation < settings.MaxGenerations; generation++)
            {
                // Selection
                var parents = _selection.SelectParents(population, settings.PopulationSize);

                var offspring = new List<CraneSolution>();

                // Crossover + Mutation
                for (int i = 0; i < parents.Count - 1; i += 2)
                {
                    var child = _crossover.Crossover(parents[i], parents[i + 1]);

                    _mutation.Mutate(child);

                    // Evaluate child
                    child.Fitness =
                        _fitnessEvaluator.EvaluateFitness(child, elements, craneSpec);

                    offspring.Add(child);
                }

                // Replacement
                population = _replacement.Replace(population, offspring);
            }

            // 4️⃣ Return best solution
            return population.Individuals.OrderByDescending(x => x.Fitness).First();
        }
    }
}