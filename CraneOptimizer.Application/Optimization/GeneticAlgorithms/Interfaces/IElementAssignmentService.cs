using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Models;
using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Services;
using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Settings;
using CraneOptimizer.Domain.Entities;
using CraneOptimizer.Domain.Schemas;

namespace CraneOptimizer.Application.Optimization.GeneticAlgorithms.Interfaces
{
    /// <summary>
    /// Evaluates the feasibility of lifting assigned elements
    /// for the crane positions defined in the chromosome.
    /// </summary>
    public interface IElementAssignmentService
    {
        /// <summary>
        /// Evaluates lifting feasibility for the given assignment.
        /// </summary>
        /// <param name="solution">
        /// GA solution containing crane positions.
        /// </param>
        /// <param name="elements">
        /// Precast elements extracted from the IFC model.
        /// </param>
        /// <param name="assignment">
        /// Mapping between crane index and element IDs
        /// provided by the user.
        /// </param>
        /// <param name="craneSpec">
        /// Crane specification including load chart.
        /// </param>
        /// <param name="settings">
        /// Genetic algorithm configuration settings.
        /// </param>
        /// <returns>
        /// Detailed evaluation results and assignment mapping.
        /// </returns>
        AssignmentResult EvaluateAssignment(
            CraneSolution solution,
            List<PrecastElement> elements,
            Dictionary<int, List<string>> assignment,
            CraneSpecification craneSpec,
            GASettings settings);

        /// <summary>
        /// Hot-path overload for the GA generation loop.
        /// Accepts pre-built LoadChartIndex and ElementStaticCache to
        /// avoid per-call LINQ and repeated null-coalescing arithmetic.
        /// Diagnostics are disabled on this path.
        /// </summary>
        AssignmentResult EvaluateAssignment(
            CraneSolution solution,
            List<PrecastElement> elements,
            Dictionary<int, List<string>> assignment,
            CraneSpecification craneSpec,
            GASettings settings,
            LoadChartIndex index,
            ElementStaticCache elementCache);

        /// <summary>
        /// Evaluates every element against one specific crane position.
        /// This is used for viewport result previews where each GA point must
        /// have its own independent color classification.
        /// </summary>
        IReadOnlyList<ElementEvaluationResult> EvaluatePosition(
            CraneSolution solution,
            int craneIndex,
            List<PrecastElement> elements,
            CraneSpecification craneSpec,
            GASettings settings);
    }
}

