using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Models;
using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Settings;
using CraneOptimizer.Domain.Entities;
using CraneOptimizer.Domain.GreenAreaEntities;
using CraneOptimizer.Domain.IfcEntities;

namespace CraneOptimizer.Application.Optimization.GeneticAlgorithms.Engine
{
    /// <summary>
    /// Main Genetic Algorithm engine.
    /// Coordinates all GA steps.
    /// </summary>
    public interface IGeneticAlgorithmEngine
    {
        /// <summary>
        /// Executes the optimization process.
        /// </summary>
        CraneSolution Run(List<CranePlacementZone> zones, List<PrecastElement> elements, CraneSpecification craneSpec, GASettings settings);
    }
}