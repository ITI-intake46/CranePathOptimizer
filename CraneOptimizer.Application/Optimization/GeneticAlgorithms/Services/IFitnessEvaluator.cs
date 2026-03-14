using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Models;
using CraneOptimizer.Domain.Entities;
using CraneOptimizer.Domain.IfcEntities;

namespace CraneOptimizer.Application.Optimization.GeneticAlgorithms.Services
{
    /// <summary>
    /// Responsible for evaluating the quality of a solution.
    /// </summary>
    public interface IFitnessEvaluator
    {
        /// <summary>
        /// Calculates the fitness score of a crane position.
        /// </summary>
        double EvaluateFitness(CraneSolution solution, List<PrecastElement> elements, CraneSpecification craneSpec);
    }
}