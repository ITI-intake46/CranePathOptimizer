using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Interfaces;
using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Models;

namespace CraneOptimizer.Application.Optimization.GeneticAlgorithms.Services
{
    /// <summary>
    /// Replacement strategy used to build the next generation.
    /// Combines old population with new children.
    /// </summary>
    public class ReplacementStrategy : IReplacementStrategy
    {
        /// <summary>
        /// Replaces the current population with a new generation. (elites strategy)
        /// </summary>
        public Population Replace(Population oldPopulation, List<CraneSolution> children)
        {
            // TODO:
            // 1️⃣ Combine old population with children

            // 2️⃣ Sort individuals by fitness

            // 3️⃣ Keep best individuals up to population size

            // 4️⃣ Create new population object

            int populationSize = oldPopulation.Individuals.Count;

            // 1. Calculate elite count (10%)
            int eliteCount = (int)(populationSize * 0.1);

            // 2. Extract best individuals from the old population
            var elites = oldPopulation.Individuals
                .OrderByDescending(i => i.Fitness)
                .ThenByDescending(i => i.CoverageRatio)
                .ThenBy(i => i.Chromosome.CranePositions.Sum(p => p.X + p.Y + p.Z))
                .Take(eliteCount)
                .ToList();

            // 3. Merge old + children, but exclude elites to prevent duplication
            var nonElitesMerged = oldPopulation.Individuals
                .Except(elites)
                .Concat(children)
                .ToList();

            // 4. Sort remaining merged pool and select enough to fill the population
            var selectedNonElites = nonElitesMerged
                .OrderByDescending(i => i.Fitness)
                .ThenByDescending(i => i.CoverageRatio)
                .ThenBy(i => i.Chromosome.CranePositions.Sum(p => p.X + p.Y + p.Z))
                .Take(populationSize - eliteCount)
                .ToList();

            // 5. Combine elites and selected non-elites to form exactly populationSize
            var nextGeneration = elites
                .Concat(selectedNonElites)
                .ToList();

            return new Population
            {
                Individuals = nextGeneration,
                Generation = oldPopulation.Generation + 1
            };
        }
    }
}