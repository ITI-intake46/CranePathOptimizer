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

            // 1️⃣ احسب عدد الـ elite (10%)
            int eliteCount = (int)(populationSize * 0.1);

            // 2️⃣ خد أفضل من القديم
            var elites = oldPopulation.Individuals
                .OrderByDescending(i => i.Fitness)
                .Take(eliteCount)
                .ToList();

            // 3️⃣ merge (old + children)
            var merged = oldPopulation.Individuals
                .Concat(children)
                .ToList();

            // 4️⃣ sort وخد الأفضل
            var nextGeneration = merged
                .OrderByDescending(i => i.Fitness)
                .Take(populationSize)
                .ToList();

            // 5️⃣ ضمان إن الـ elites موجودين
            nextGeneration = nextGeneration
                .Concat(elites)
                .OrderByDescending(i => i.Fitness)
                .Take(populationSize)
                .ToList();

            return new Population
            {
                Individuals = nextGeneration,
                Generation = oldPopulation.Generation + 1
            };
        }
    }
}