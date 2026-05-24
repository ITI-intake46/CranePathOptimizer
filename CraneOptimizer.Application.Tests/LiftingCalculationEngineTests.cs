using Xunit;
using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Engines.CraneConfiguration;
using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Engines.LiftingCalculation;
using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Models;
using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Settings;
using CraneOptimizer.Domain.Common;
using CraneOptimizer.Domain.Entities;
using CraneOptimizer.Domain.Schemas;
using System.Collections.Generic;

namespace CraneOptimizer.Application.Tests
{
    public class LiftingCalculationEngineTests
    {
        [Fact]
        public void TestRadiusCalculation()
        {
            var configEngine = new CraneConfigurationEngine();
            var liftingEngine = new LiftingCalculationEngine(configEngine);

            var cranePos = new DomainPoint3D(0, 0, 0);
            var element = new PrecastElement
            {
                ElementCOG = new DomainPoint3D(3, 4, 0)
            };

            double radius = liftingEngine.CalculateRadius(cranePos, element);
            // distance should be 5.0
            Assert.Equal(5.0, radius, 5);
        }

        [Fact]
        public void TestLiftHeightCalculation()
        {
            var configEngine = new CraneConfigurationEngine();
            var liftingEngine = new LiftingCalculationEngine(configEngine);

            var cranePos = new DomainPoint3D(0, 0, 10);
            var element = new PrecastElement
            {
                ElementCOG = new DomainPoint3D(0, 0, 20),
                ElementHeight = 4.0
            };
            var settings = new GASettings
            {
                WireLength = 2.5
            };

            double liftHeight = liftingEngine.CalculateLiftHeight(element, cranePos, settings);
            // relativeTopElevation = COG.Z (20) + elementHalfHeight (2) - crane.Z (10) = 12
            // liftHeight = 12 + WireLength (2.5) = 14.5
            Assert.Equal(14.5, liftHeight, 5);
        }

        [Fact]
        public void TestDetermineLiftStatus()
        {
            var configEngine = new CraneConfigurationEngine();
            var liftingEngine = new LiftingCalculationEngine(configEngine);

            var settings = new GASettings
            {
                DeratingRatio = 1.0,
                SafetyFactor = 1.0
            };

            var craneSpec = new CraneSpecification
            {
                MaxRadius = 40,
                MaxBoomLength = 50,
                LoadChart = new List<TargetPoint>
                {
                    new TargetPoint { Radius = 10, BoomLength = 20, Capacity = 10 }
                }
            };

            // Case 1: Out of Radius
            var status1 = liftingEngine.DetermineLiftStatus(45, 20, 5, craneSpec, settings);
            Assert.Equal(LiftStatus.OutOfRadius, status1);

            // Case 2: Height Fail (Boom length exceed)
            var status2 = liftingEngine.DetermineLiftStatus(10, 55, 5, craneSpec, settings);
            Assert.Equal(LiftStatus.HeightFail, status2);

            // Case 3: Overweight (Capacity exceed)
            var status3 = liftingEngine.DetermineLiftStatus(10, 20, 15, craneSpec, settings);
            Assert.Equal(LiftStatus.Overweight, status3);

            // Case 4: Special Case (No capacity available)
            var craneSpecNoChart = new CraneSpecification
            {
                MaxRadius = 40,
                MaxBoomLength = 50,
                LoadChart = new List<TargetPoint>()
            };
            var status4 = liftingEngine.DetermineLiftStatus(10, 20, 5, craneSpecNoChart, settings);
            Assert.Equal(LiftStatus.SpecialCase, status4);

            // Case 5: Liftable
            var status5 = liftingEngine.DetermineLiftStatus(10, 20, 8, craneSpec, settings);
            Assert.Equal(LiftStatus.Liftable, status5);
        }
    }
}
