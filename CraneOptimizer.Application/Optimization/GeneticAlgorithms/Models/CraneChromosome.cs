using CraneOptimizer.Domain.Common;

namespace CraneOptimizer.Application.Optimization.GeneticAlgorithms.Models
{
    /// <summary>
    /// Represents the chromosome of a GA individual.
    /// A chromosome contains a sequence of crane setup positions.
    /// Each position represents a crane relocation during construction.
    /// </summary>
    public class CraneChromosome
    {
        /// <summary>
        /// List of crane setup positions used by this chromosome.
        /// The order represents the sequence of crane relocations.
        /// </summary>
        public List<Point3D> CranePositions { get; set; } = new();

        /// <summary>
        /// Number of crane relocations represented by this chromosome.
        /// </summary>
        public int RelocationCount => CranePositions.Count;
    }
}