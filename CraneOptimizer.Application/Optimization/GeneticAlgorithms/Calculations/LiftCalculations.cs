using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Interfaces;
using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Engines.CraneConfiguration;
using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Engines.LiftingCalculation;
using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Models;
using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Settings;
using CraneOptimizer.Domain.Common;
using CraneOptimizer.Domain.Entities;
using CraneOptimizer.Domain.Schemas;

namespace CraneOptimizer.Application.Optimization.GeneticAlgorithms.Calculations
{
    /// <summary>
    /// Temporary compatibility wrapper delegating to new engines to ensure no immediate breaking changes.
    /// </summary>
    public static class LiftCalculation
    {
        private static readonly ICraneConfigurationEngine _configEngine = new CraneConfigurationEngine();
        private static readonly ILiftingCalculationEngine _liftingEngine = new LiftingCalculationEngine(_configEngine);

        public static double CalculateRadius(DomainPoint3D cranePosition, PrecastElement element)
        {
            return _liftingEngine.CalculateRadius(cranePosition, element);
        }

        public static double CalculateLiftHeight(
            PrecastElement element,
            DomainPoint3D cranePosition,
            GASettings settings)
        {
            return _liftingEngine.CalculateLiftHeight(element, cranePosition, settings);
        }

        public static double CalculateRequiredBoomLength(double radius, double height)
        {
            return _configEngine.CalculateRequiredBoomLength(radius, height);
        }

        public static double CraneCapacity(
            CraneSpecification craneSpec,
            double radius,
            double boomLength,
            GASettings settings)
        {
            return _configEngine.CraneCapacity(craneSpec, radius, boomLength, settings);
        }

        public static LiftStatus DetermineLiftStatus(
            double radius,
            double boomLength,
            double requiredCapacity,
            CraneSpecification craneSpec,
            GASettings settings)
        {
            return _liftingEngine.DetermineLiftStatus(radius, boomLength, requiredCapacity, craneSpec, settings);
        }
    }
}
