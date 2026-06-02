using Xunit;
using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Engines.CraneConfiguration;
using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Settings;
using CraneOptimizer.Domain.Entities;
using CraneOptimizer.Domain.Schemas;
using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Models;
using System;
using System.Collections.Generic;

namespace CraneOptimizer.Application.Tests
{
    public class CraneConfigurationEngineTests
    {
        [Fact]
        public void TestRequiredBoomLength()
        {
            var engine = new CraneConfigurationEngine();
            double radius = 3.0;
            double height = 4.0;
            
            // Hypotenuse: sqrt(3^2 + 4^2) = 5.0
            double result = engine.CalculateRequiredBoomLength(radius, height);
            
            Assert.Equal(5.0, result, 5);
        }

        [Fact]
        public void TestLoadChartDiscreteLookup()
        {
            var engine = new CraneConfigurationEngine();
            var settings = new GASettings
            {
                DeratingRatio = 0.8,
                SafetyFactor = 0.9
            };

            var craneSpec = new CraneSpecification
            {
                LoadChart = new List<TargetPoint>
                {
                    // Chart format: Radius, BoomLength, Capacity
                    new TargetPoint { Radius = 10, BoomLength = 20, Capacity = 50 },
                    new TargetPoint { Radius = 10, BoomLength = 30, Capacity = 40 },
                    new TargetPoint { Radius = 20, BoomLength = 20, Capacity = 30 },
                    new TargetPoint { Radius = 20, BoomLength = 30, Capacity = 20 }
                }
            };

            // 1. Exact radius and boom match
            double capExact = engine.CraneCapacity(craneSpec, 10, 20, settings);
            // Expected: R=10, B=20 => 50 * 0.8 * 0.9 = 36.0
            Assert.Equal(36.0, capExact, 5);

            // 2. Boom rounded up to next available chart boom
            double capBoomRoundUp = engine.CraneCapacity(craneSpec, 10, 25, settings);
            // Expected: R=10, B=30 => 40 * 0.8 * 0.9 = 28.8
            Assert.Equal(28.8, capBoomRoundUp, 5);

            // 3. Radius rounded up to next available chart radius
            double capRadiusRoundUp = engine.CraneCapacity(craneSpec, 15, 20, settings);
            // Expected: R=20, B=20 => 30 * 0.8 * 0.9 = 21.6
            Assert.Equal(21.6, capRadiusRoundUp, 5);

            // 4. Both radius and boom rounded up
            double capBothRoundUp = engine.CraneCapacity(craneSpec, 15, 25, settings);
            // Expected: R=20, B=30 => 20 * 0.8 * 0.9 = 14.4
            Assert.Equal(14.4, capBothRoundUp, 5);

            // 5. Radius exceeds chart range
            double capRadiusExceeds = engine.CraneCapacity(craneSpec, 25, 20, settings);
            // Expected: 0
            Assert.Equal(0, capRadiusExceeds, 5);

            // 6. Boom exceeds chart range
            double capBoomExceeds = engine.CraneCapacity(craneSpec, 20, 35, settings);
            // Expected: 0
            Assert.Equal(0, capBoomExceeds, 5);

            // 7. Irregular/missing chart entries & no valid conservative point
            var irregularSpec = new CraneSpecification
            {
                LoadChart = new List<TargetPoint>
                {
                    new TargetPoint { Radius = 20, BoomLength = 20, Capacity = 30 },
                    new TargetPoint { Radius = 30, BoomLength = 30, Capacity = 15 }
                }
            };

            // R=15, B=28 => next radius >= 15 is 20. But at R=20, there is no boom >= 28. Expected: 0
            double capIrregular = engine.CraneCapacity(irregularSpec, 15, 28, settings);
            Assert.Equal(0, capIrregular, 5);

            // R=35, B=20 => no entry has Radius >= 35 => 0
            double capNoValidR = engine.CraneCapacity(irregularSpec, 35, 20, settings);
            Assert.Equal(0, capNoValidR, 5);

            // R=20, B=35 => no entry has BoomLength >= 35 => 0
            double capNoValidB = engine.CraneCapacity(irregularSpec, 20, 35, settings);
            Assert.Equal(0, capNoValidB, 5);
        }

        [Fact]
        public void TestEvaluateCraneCapacityDiagnostic()
        {
            var engine = new CraneConfigurationEngine();
            var settings = new GASettings
            {
                DeratingRatio = 0.8,
                SafetyFactor = 0.9
            };

            // 6. missing/empty load chart
            var emptySpec = new CraneSpecification { LoadChart = new List<TargetPoint>() };
            var diagEmpty = engine.EvaluateCraneCapacityDiagnostic(emptySpec, 10, 20, settings);
            Assert.Equal(CapacityFailureReason.MissingLoadChartData, diagEmpty.FailureReason);

            var chartSpec = new CraneSpecification
            {
                LoadChart = new List<TargetPoint>
                {
                    new TargetPoint { Radius = 10, BoomLength = 20, Capacity = 50 },
                    new TargetPoint { Radius = 10, BoomLength = 30, Capacity = 0 }, // 5. zero capacity chart entry
                    new TargetPoint { Radius = 20, BoomLength = 20, Capacity = 30 },
                    new TargetPoint { Radius = 20, BoomLength = 30, Capacity = 20 }
                }
            };

            // 1. radius beyond max chart radius
            var diagRadiusBeyond = engine.EvaluateCraneCapacityDiagnostic(chartSpec, 25, 20, settings);
            Assert.Equal(CapacityFailureReason.RadiusBeyondLoadChart, diagRadiusBeyond.FailureReason);

            // 2. boom beyond max chart boom
            var diagBoomBeyond = engine.EvaluateCraneCapacityDiagnostic(chartSpec, 15, 35, settings);
            Assert.Equal(CapacityFailureReason.BoomBeyondLoadChart, diagBoomBeyond.FailureReason);

            // 3. missing chart cell but another conservative point exists
            var diagValid = engine.EvaluateCraneCapacityDiagnostic(chartSpec, 15, 25, settings);
            Assert.Equal(CapacityFailureReason.None, diagValid.FailureReason);
            Assert.Equal(14.4, diagValid.Capacity, 5);

            // 4. no conservative chart point exists
            var irregularSpec = new CraneSpecification
            {
                LoadChart = new List<TargetPoint>
                {
                    new TargetPoint { Radius = 20, BoomLength = 30, Capacity = 20 },
                    new TargetPoint { Radius = 30, BoomLength = 20, Capacity = 30 }
                }
            };
            var diagNoConservative = engine.EvaluateCraneCapacityDiagnostic(irregularSpec, 25, 25, settings);
            Assert.Equal(CapacityFailureReason.OutsideChartEnvelope, diagNoConservative.FailureReason);

            // 5. zero capacity chart entry
            var diagZeroCap = engine.EvaluateCraneCapacityDiagnostic(chartSpec, 10, 30, settings);
            Assert.Equal(CapacityFailureReason.InvalidZeroCell, diagZeroCap.FailureReason);
        }
    }
}
