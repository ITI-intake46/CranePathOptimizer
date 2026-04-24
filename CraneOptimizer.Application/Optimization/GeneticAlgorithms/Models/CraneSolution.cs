namespace CraneOptimizer.Application.Optimization.GeneticAlgorithms.Models
{
    /// <summary>
    /// Represents a GA individual solution.
    /// Each solution contains a chromosome and its evaluated fitness score.
    /// </summary>
    public class CraneSolution
    {
        /// <summary>
        /// Chromosome containing the crane placement sequence.
        /// </summary>
        public CraneChromosome Chromosome { get; set; } = new();

        /// <summary>
        /// Final fitness score of the solution.
        /// Higher values represent better crane placement strategies.
        /// </summary>
        public double Fitness { get; set; }

        /// <summary>
        /// Total number of penalties accumulated during evaluation.
        /// Penalties include radius violations, overloads, or height failures.
        /// </summary>
        public int Penalty { get; set; }

        /// <summary>
        /// Percentage of building elements that can be successfully lifted.
        /// </summary>
        public double CoverageRatio { get; set; }

        public CraneSolution DeepCopy()
        {
            return new CraneSolution
            {
                Chromosome = new CraneChromosome
                {
                    CranePositions = new System.Collections.Generic.List<Domain.Common.DomainPoint3D>(Chromosome.CranePositions)
                },
                Fitness = this.Fitness,
                Penalty = this.Penalty,
                CoverageRatio = this.CoverageRatio
            };
        }
    }
}