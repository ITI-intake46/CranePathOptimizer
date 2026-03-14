using CraneOptimizer.Application.Optimization.GeneticAlgorithm.Models;
using CraneOptimizer.Domain.GreenAreaEntities;

namespace CraneOptimizer.Application.Optimization.GeneticAlgorithms.Services
{
    /// <summary>
    /// Responsible for generating the initial population.
    /// </summary>
    public interface IGAInitializer
    {
        /// <summary>
        /// Generates random crane positions inside the allowed crane zones.
        /// </summary>
        Population InitializePopulation(List<CranePlacementZone> zones, int populationSize);
    }
}