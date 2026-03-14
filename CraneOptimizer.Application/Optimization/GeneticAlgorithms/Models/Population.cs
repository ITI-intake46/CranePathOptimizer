using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Models;

namespace CraneOptimizer.Application.Optimization.GeneticAlgorithm.Models
{
    /// <summary>
    /// Represents a population of solutions in a single generation.
    /// </summary>
    public class Population
    {
        /// <summary>
        /// List of candidate crane positions.
        /// </summary>
        public List<CraneSolution> Individuals { get; set; } = new();

        /// <summary>
        /// Current generation index.
        /// </summary>
        public int GenerationNumber { get; set; }
    }
}