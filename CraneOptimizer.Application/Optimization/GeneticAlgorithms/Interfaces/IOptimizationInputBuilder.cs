using CraneOptimizer.Application.Optimization.GeneticAlgorithms.DTOs;
using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Settings;
using CraneOptimizer.Domain.Entities;
using CraneOptimizer.Domain.Schemas;
using System.Collections.Generic;

namespace CraneOptimizer.Application.Optimization.GeneticAlgorithms.Interfaces
{
    public interface IOptimizationInputBuilder
    {
        PlacementInput Build(
            GASettings settings,
            Crane crane,
            IReadOnlyList<PrecastElement> elements,
            IReadOnlyList<GreenAreaZone> zones);
    }
}
