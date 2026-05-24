using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Interfaces;
using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Models;
using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Settings;
using CraneOptimizer.Domain.Common;
using CraneOptimizer.Domain.Entities;
using CraneOptimizer.Domain.Schemas;
using System;

namespace CraneOptimizer.Application.Optimization.GeneticAlgorithms.Engines.LiftingCalculation
{
    public class LiftingCalculationEngine : ILiftingCalculationEngine
    {
        private const double CapacityToleranceTons = 0.001;
        private readonly ICraneConfigurationEngine _configEngine;

        public LiftingCalculationEngine(ICraneConfigurationEngine configEngine)
        {
            _configEngine = configEngine;
        }

        public double CalculateRadius(DomainPoint3D cranePosition, PrecastElement element)
        {
            double dx = cranePosition.X - element.ElementCOG.X;
            double dy = cranePosition.Y - element.ElementCOG.Y;

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
            // ── 1. Geometric reach check ───────────────────────────
            if (_configEngine.ExceedsMaxRadius(radius, craneSpec))
                return LiftStatus.OutOfRadius;

            // ── 2. Boom length check ───────────────────────────────
            if (_configEngine.ExceedsMaxBoomLength(boomLength, craneSpec))
                return LiftStatus.HeightFail;

            // ── 3. Load chart data availability ───────────────────
            double available = _configEngine.CraneCapacity(craneSpec, radius, boomLength, settings);

            if (available <= 0)
                return LiftStatus.SpecialCase;

            // ── 4. Capacity check (with FP tolerance) ─────────────
            if (requiredCapacity > available + CapacityToleranceTons)
                return LiftStatus.Overweight;

            // ── 5. All checks passed ───────────────────────────────
            return LiftStatus.Liftable;
        }
    }
}
