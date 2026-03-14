using CraneOptimizer.Application.Optimization.GeneticAlgorithm.Models;

namespace CraneOptimizer.Application.Optimization.GeneticAlgorithms.Models
{
    public class CraneSolution
    {
        public CraneChromosome Chromosome { get; set; }

        public double Fitness { get; set; }

        public int Penalty { get; set; }
    }
}
