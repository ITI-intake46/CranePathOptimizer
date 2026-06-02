using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Models;
using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Settings;
using CraneOptimizer.Domain.Common;
using CraneOptimizer.Domain.Entities;
using CraneOptimizer.Domain.Schemas;

namespace CraneOptimizer.Application.Optimization.GeneticAlgorithms.Interfaces
{
    public interface ILiftingCalculationEngine
    {
        double CalculateRadius(DomainPoint3D cranePosition, PrecastElement element);
        double CalculateRadius(DomainPoint3D cranePosition, PrecastElement element, GASettings settings);
        double CalculateLiftHeight(PrecastElement element, DomainPoint3D cranePosition, GASettings settings);
        double CalculateRequiredWeight(PrecastElement element, GASettings settings);
        LiftStatus DetermineLiftStatus(
            double radius,
            double boomLength,
            double requiredCapacity,
            CraneSpecification craneSpec,
            GASettings settings);
    }
}
