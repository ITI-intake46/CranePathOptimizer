using Xunit;
using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Engines.CraneConfiguration;
using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Settings;
using CraneOptimizer.Domain.Entities;
using CraneOptimizer.Domain.Schemas;
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
        public void TestLoadChartInterpolation()
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

            // Test case 1: Exact match on radius & boom
            double capExact = engine.CraneCapacity(craneSpec, 10, 20, settings);
            // Expected capacity = 50 * 0.8 * 0.9 = 36.0
            Assert.Equal(36.0, capExact, 5);

            // Test case 2: Interpolate along boom length (radius=10, boom=25)
            double capAlongBoom = engine.CraneCapacity(craneSpec, 10, 25, settings);
            // Radius 10 capacity for Boom 20 is 50, Boom 30 is 40. Midpoint (Boom 25) = 45.
            // Expected = 45 * 0.8 * 0.9 = 32.4
            Assert.Equal(32.4, capAlongBoom, 5);

            // Test case 3: Interpolate along radius (radius=15, boom=20)
            double capAlongRadius = engine.CraneCapacity(craneSpec, 15, 20, settings);
            // Boom 20 capacity for Radius 10 is 50, Radius 20 is 30. Midpoint (Radius 15) = 40.
            // Expected = 40 * 0.8 * 0.9 = 28.8
            Assert.Equal(28.8, capAlongRadius, 5);

            // Test case 4: Bilinear interpolation (radius=15, boom=25)
            double capBilinear = engine.CraneCapacity(craneSpec, 15, 25, settings);
            // Radius 10, Boom 25 -> 45
            // Radius 20, Boom 25 -> 25
            // Midpoint Radius 15, Boom 25 -> 35
            // Expected = 35 * 0.8 * 0.9 = 25.2
            Assert.Equal(25.2, capBilinear, 5);
        }
    }
}
