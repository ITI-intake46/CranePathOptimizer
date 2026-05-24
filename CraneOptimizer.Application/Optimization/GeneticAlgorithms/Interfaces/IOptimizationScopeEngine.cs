using CraneOptimizer.Application.Optimization.GeneticAlgorithms.DTOs;
using CraneOptimizer.Domain.Entities;
using CraneOptimizer.Domain.Schemas;
using System.Collections.Generic;

namespace CraneOptimizer.Application.Optimization.GeneticAlgorithms.Interfaces
{
    public interface IOptimizationScopeEngine
    {
        List<PrecastElement> FilterElements(
            List<PrecastElement> allElements,
            OptimizationScopeFilter filter);
    }
}
