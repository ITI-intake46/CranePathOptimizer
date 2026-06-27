using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Interfaces;
using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Models;
using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Settings;
using CraneOptimizer.Domain.Entities;
using CraneOptimizer.Domain.Schemas;
using System.Collections.Generic;
using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Engines.GeneticOptimization;

namespace CraneOptimizer.Application.Optimization.GeneticAlgorithms.Services
{
    public class RoutingAssignmentService : IElementAssignmentService
    {
        private readonly ElementAssignmentService _greedyService;
        private readonly HungarianAssignmentService _hungarianService;

        public RoutingAssignmentService(
            ElementAssignmentService greedyService,
            HungarianAssignmentService hungarianService)
        {
            _greedyService = greedyService;
            _hungarianService = hungarianService;
        }

        public AssignmentResult EvaluateAssignment(
            CraneSolution solution,
            List<PrecastElement> elements,
            Dictionary<int, List<string>> assignment,
            CraneSpecification craneSpec,
            GASettings settings)
        {
            if (settings.UseHungarianAssignment)
            {
                return _hungarianService.EvaluateAssignment(solution, elements, assignment, craneSpec, settings);
            }
            else
            {
                return _greedyService.EvaluateAssignment(solution, elements, assignment, craneSpec, settings);
            }
        }

        public AssignmentResult EvaluateAssignment(
            CraneSolution solution,
            List<PrecastElement> elements,
            Dictionary<int, List<string>> assignment,
            CraneSpecification craneSpec,
            GASettings settings,
            LoadChartIndex index,
            ElementStaticCache elementCache)
        {
            if (settings.UseHungarianAssignment)
            {
                return _hungarianService.EvaluateAssignment(solution, elements, assignment, craneSpec, settings, index, elementCache);
            }
            else
            {
                return _greedyService.EvaluateAssignment(solution, elements, assignment, craneSpec, settings, index, elementCache);
            }
        }

        public IReadOnlyList<ElementEvaluationResult> EvaluatePosition(
            CraneSolution solution,
            int craneIndex,
            List<PrecastElement> elements,
            CraneSpecification craneSpec,
            GASettings settings)
        {
            if (settings.UseHungarianAssignment)
            {
                return _hungarianService.EvaluatePosition(solution, craneIndex, elements, craneSpec, settings);
            }
            else
            {
                return _greedyService.EvaluatePosition(solution, craneIndex, elements, craneSpec, settings);
            }
        }
    }
}
