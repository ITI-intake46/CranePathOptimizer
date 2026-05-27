using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Interfaces;
using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Models;
using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Settings;
using CraneOptimizer.Domain.Common;
using CraneOptimizer.Domain.Entities;
using CraneOptimizer.Domain.Schemas;
using System;
using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Services;

namespace CraneOptimizer.Application.Optimization.GeneticAlgorithms.Engines.LiftingCalculation
{
    public class LiftingCalculationEngine : ILiftingCalculationEngine
    {
        private const double CapacityToleranceTons = 0.001;
        private readonly ICraneConfigurationEngine _configEngine;
        private readonly ILoadChartCapacityResolver _capacityResolver;

        public LiftingCalculationEngine(ICraneConfigurationEngine configEngine)
            : this(configEngine, null)
        {
        }

        public LiftingCalculationEngine(ICraneConfigurationEngine configEngine, ILoadChartCapacityResolver? capacityResolver)
        {
            _configEngine = configEngine;
            _capacityResolver = capacityResolver ?? new LoadChartCapacityResolver();
        }

        public double CalculateRadius(DomainPoint3D cranePosition, PrecastElement element)
        {
            return CalculateRadius(cranePosition, element, new GASettings());
        }

        public double CalculateRadius(DomainPoint3D cranePosition, PrecastElement element, GASettings settings)
        {
            double slewX = cranePosition.X + settings.SlewOffsetX;
            double slewY = cranePosition.Y + settings.SlewOffsetY;

            double dx = slewX - element.ElementCOG.X;
            double dy = slewY - element.ElementCOG.Y;

            return Math.Sqrt(dx * dx + dy * dy);
        }

        public double CalculateLiftHeight(
            PrecastElement element,
            DomainPoint3D cranePosition,
            GASettings settings)
        {
            double elementHalfHeight = (element.ElementHeight ?? 0) / 2.0;
            double relativeTopElevation = element.ElementCOG.Z + elementHalfHeight - cranePosition.Z;

            return Math.Max(0d, relativeTopElevation) + settings.WireLength;
        }

        public double CalculateRequiredWeight(PrecastElement element, GASettings settings)
        {
            return (element.ElementWT ?? 0) + settings.AdditionalWt;
        }

        public LiftStatus DetermineLiftStatus(
            double radius,
            double boomLength,
            double requiredCapacity,
            CraneSpecification craneSpec,
            GASettings settings)
        {
            var resolveResult = _capacityResolver.Resolve(craneSpec, radius, boomLength, requiredCapacity, settings);
            return resolveResult.LiftStatus;
        }
    }
}
