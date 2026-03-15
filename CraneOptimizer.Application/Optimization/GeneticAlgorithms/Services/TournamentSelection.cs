using CraneOptimizer.Application.Optimization.GeneticAlgorithm.Models;
using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Interfaces;
using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Models;

namespace CraneOptimizer.Application.Optimization.GeneticAlgorithms.Services
{
    /// <summary>
    /// Tournament selection strategy.
    /// Selects parents by running small competitions
    /// between randomly chosen individuals.
    /// </summary>
    public class TournamentSelection : ISelectionStrategy
    {
        private readonly Random _random = new();

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

            // TODO:
            // randomly select TournamentSize individuals
            // from population.Individuals

            return candidates;
        }

        /// <summary>
        /// Chooses the individual with the highest fitness
        /// among tournament candidates.
        /// </summary>
        private CraneSolution SelectBestCandidate(List<CraneSolution> candidates)
        {
            // TODO:
            // return candidate with highest Fitness

            return candidates.First();
        }
    }
}