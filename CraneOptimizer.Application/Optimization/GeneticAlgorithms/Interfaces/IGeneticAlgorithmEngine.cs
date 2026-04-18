using CraneOptimizer.Application.Optimization.GeneticAlgorithms.DTOs;
using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Models;

namespace CraneOptimizer.Application.Optimization.GeneticAlgorithms.Interfaces
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
        /// <param name="zones">
        /// Crane placement zones defined by the user.
        /// </param>
        /// <param name="elements">
        /// Precast elements extracted from the IFC model.
        /// </param>
        /// <param name="assignment">
        /// Mapping between crane index and element IDs
        /// defined by the user in the UI.
        /// </param>
        /// <param name="craneSpec">
        /// Selected crane specification including load chart.
        /// </param>
        /// <param name="settings">
        /// Genetic algorithm configuration parameters.
        /// </param>
        CraneSolution Run(PlacementInput input);
    }
}