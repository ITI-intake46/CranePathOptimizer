using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Interfaces;
using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Models;
using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Engines.GeneticOptimization;
using System;
using System.Collections.Generic;
using System.Linq;

namespace CraneOptimizer.Application.Optimization.GeneticAlgorithms.Operators
{
    /// <summary>
    /// Tournament selection strategy.
    /// Selects parents by running small competitions
    /// between randomly chosen individuals.
    /// </summary>
    public class TournamentSelection : ISelectionStrategy
    {
        private Random _random => GARandomManager.Instance;

        /// <summary>
        /// Number of individuals competing in each tournament.
        /// Typical values: 2 or 3.
        /// </summary>
        private const int TournamentSize = 3;

        /// <summary>
        /// Selects parent solutions from the population.
        /// </summary>
        public List<CraneSolution> SelectParents(
            Population population,
            int numberOfParents)
        {
            var parents = new List<CraneSolution>();

            for (int i = 0; i < numberOfParents; i++)
            {
                // 1️⃣ randomly pick tournament candidates
                var candidates = PickRandomCandidates(population);

                // 2️⃣ choose the best individual from the tournament
                var winner = SelectBestCandidate(candidates);

                parents.Add(winner);
            }

            return parents;
        }

        /// <summary>
        /// Randomly selects individuals from the population
        /// to participate in a tournament.
        /// </summary>
        private List<CraneSolution> PickRandomCandidates(Population population)
        {
            var candidates = new List<CraneSolution>();

            var pool = population.Individuals.ToList();

            for (int i = 0; i < TournamentSize; i++)
            {
                if (pool.Count == 0) break;
                int index = _random.Next(pool.Count);
                candidates.Add(pool[index]);
                pool.RemoveAt(index);
            }

            return candidates;
        }

        /// <summary>
        /// Chooses the individual with the highest fitness
        /// among tournament candidates.
        /// </summary>
        private CraneSolution SelectBestCandidate(List<CraneSolution> candidates)
        {
            // Deterministic tie-breaking to avoid random selection of identical fitness
            return candidates
                .OrderByDescending(c => c.Fitness)
                .ThenByDescending(c => c.CoverageRatio)
                .ThenBy(c => c.Chromosome.CranePositions.Count)
                .First();
        }
    }
}
