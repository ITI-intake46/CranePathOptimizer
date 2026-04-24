using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Interfaces;
using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Models;

namespace CraneOptimizer.Application.Optimization.GeneticAlgorithms.Services
{
    /// <summary>
    /// Default crossover operator for crane chromosomes.
    /// Combines crane positions from two parents to create a child solution.
    /// </summary>
    public class CrossoverOperator : ICrossoverOperator
    {
        private Random _random => GARandomManager.Instance;

        /// <summary>
        /// Generates a child solution by combining positions from two parents.
        /// </summary>
        public CraneSolution Crossover(CraneSolution parent1, CraneSolution parent2)
        {
            var child = new CraneSolution
            {
                Chromosome = new CraneChromosome()
            };

            // TODO:
            // 1️⃣ Choose crossover strategy
            // Example: single-point crossover

            // TODO:
            // 2️⃣ Copy some crane positions from parent1

            // TODO:
            // 3️⃣ Copy remaining positions from parent2

            // TODO:
            // 4️⃣ Ensure resulting chromosome is valid
            // (no empty chromosome, positions remain inside zones)

            var p1 = parent1.Chromosome.CranePositions;
            var p2 = parent2.Chromosome.CranePositions;

            int minLength = Math.Min(p1.Count, p2.Count);

            // Guard: if one of them is empty, take from the other
            if (minLength == 0)
            {
                child.Chromosome.CranePositions.AddRange(p1.Count > 0 ? p1 : p2);
                return child;
            }

            // 1️⃣ choose crossover point
            int cut = _random.Next(1, minLength);

            // 2️⃣ take from parent1
            for (int i = 0; i < cut; i++)
                child.Chromosome.CranePositions.Add(p1[i]);

            // 3️⃣ take from parent2
            for (int i = cut; i < p2.Count; i++)
                child.Chromosome.CranePositions.Add(p2[i]);

            // 4️⃣ ensure not empty
            if (child.Chromosome.CranePositions.Count == 0)
                child.Chromosome.CranePositions.Add(p1[0]);

            return child;
        }
    }
}