using CraneOptimizer.Domain.Common;

namespace CraneOptimizer.Application.Optimization.GeneticAlgorithm.Models
{
    /// <summary>
    /// Represents the chromosome of a GA individual.
    /// In this project the chromosome contains the crane coordinates.
    /// </summary>
    public class CraneChromosome
    {
        /// <summary>
        /// X coordinate of the crane position.
        /// </summary>
        public double X { get; set; }

        /// <summary>
        /// Y coordinate of the crane position.
        /// </summary>
        public double Y { get; set; }

        /// <summary>
        /// Converts chromosome to domain point.
        /// </summary>
        public Point3D ToPoint()
        {
            return new Point3D(X, Y, 0);
        }
    }
}