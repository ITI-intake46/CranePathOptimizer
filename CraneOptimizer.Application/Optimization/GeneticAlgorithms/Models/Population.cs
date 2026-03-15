using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Models;

namespace CraneOptimizer.Application.Optimization.GeneticAlgorithm.Models
{
    /// <summary>
    /// Represents a population in the genetic algorithm.
    /// A population contains multiple crane placement solutions.
    /// </summary>
    public class Population
    {
        /// <summary>
        /// List of individuals in the current generation.
        /// </summary>
        public List<CraneSolution> Individuals { get; set; } = new();

        /// <summary>
        /// Current generation index.
        /// </summary>
        public int Generation { get; set; }
    }
}