using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Interfaces;
using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Models;
using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Settings;
using CraneOptimizer.Domain.Entities;
using CraneOptimizer.Domain.IfcEntities;

namespace CraneOptimizer.Application.Optimization.GeneticAlgorithms.Services
{
    public class ElementAssignmentService : IElementAssignmentService
    {

        public AssignmentResult EvaluateAssignment(CraneSolution solution, List<PrecastElement> elements, Dictionary<int, List<string>> assignment, CraneSpecification craneSpec, GASettings settings)
        {
            throw new NotImplementedException();
        }
    }
}