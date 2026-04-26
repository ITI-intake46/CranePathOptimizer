using CraneOptimizer.Application.Optimization.GeneticAlgorithms.DTOs;
using CraneOptimizer.Domain.Common;
using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Models;
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
        AssignmentResult EvaluateAssignment(
            CraneSolution solution,
            PlacementInput input);
    }
}