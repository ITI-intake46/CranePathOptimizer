using Xunit;
using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Models;
using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Services;
using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Settings;
using CraneOptimizer.Domain.Entities;
using System.Collections.Generic;

namespace CraneOptimizer.Application.Tests
{
    public class LoadChartCapacityResolverTests
    {
        private readonly LoadChartCapacityResolver _resolver;
        private readonly GASettings _settings;

        public LoadChartCapacityResolverTests()
        {
            _resolver = new LoadChartCapacityResolver();
            _settings = new GASettings
            {
                DeratingRatio = 1.0,
                SafetyFactor = 1.0
            };
        }

        [Fact]
        public void TestBlankCellReturnsNotCharted()
        {
            var craneSpec = new CraneSpecification
            {
                MaxRadius = 40,
                MaxBoomLength = 50,
                LoadChart = new List<TargetPoint>
                {
                    new TargetPoint { Radius = 10, BoomLength = 20, Capacity = -1.0 } // blank sentinel
                }
            };

            var result = _resolver.Resolve(craneSpec, 10, 20, 5, _settings);

            Assert.Equal(LiftStatus.NotCharted, result.LiftStatus);
            Assert.Equal(ChartStatus.InvalidBlankCell, result.ChartStatus);
            Assert.True(result.IsBlankCell);
            Assert.False(result.IsZeroCell);
        }

        [Fact]
        public void TestZeroCellReturnsNotCharted()
        {
            var craneSpec = new CraneSpecification
            {
                MaxRadius = 40,
                MaxBoomLength = 50,
                LoadChart = new List<TargetPoint>
                {
                    new TargetPoint { Radius = 10, BoomLength = 20, Capacity = 0.0 } // explicit zero
                }
            };

            var result = _resolver.Resolve(craneSpec, 10, 20, 5, _settings);

            Assert.Equal(LiftStatus.NotCharted, result.LiftStatus);
            Assert.Equal(ChartStatus.InvalidZeroCell, result.ChartStatus);
            Assert.True(result.IsZeroCell);
            Assert.False(result.IsBlankCell);
        }

        [Fact]
        public void TestConservativeSteppedRadiusLookup()
        {
            var craneSpec = new CraneSpecification
            {
                MaxRadius = 40,
                MaxBoomLength = 50,
                LoadChart = new List<TargetPoint>
                {
                    new TargetPoint { Radius = 10, BoomLength = 20, Capacity = 10.0 },
                    new TargetPoint { Radius = 15, BoomLength = 20, Capacity = 8.0 }
                }
            };

            // Required radius is 12, next listed >= 12 is 15.
            var result = _resolver.Resolve(craneSpec, 12, 20, 5, _settings);

            Assert.Equal(LiftStatus.Liftable, result.LiftStatus);
            Assert.Equal(ChartStatus.ListedCapacityFound, result.ChartStatus);
            Assert.Equal(15.0, result.SelectedChartRadius);
            Assert.Equal(20.0, result.SelectedChartBoomLength);
            Assert.True(result.IsSteppedRadius);
            Assert.False(result.IsExactMatch);
            Assert.Equal(8.0, result.AvailableCapacity);
        }

        [Fact]
        public void TestConservativeSteppedBoomLengthLookup()
        {
            var craneSpec = new CraneSpecification
            {
                MaxRadius = 40,
                MaxBoomLength = 50,
                LoadChart = new List<TargetPoint>
                {
                    new TargetPoint { Radius = 10, BoomLength = 20, Capacity = 10.0 },
                    new TargetPoint { Radius = 10, BoomLength = 30, Capacity = 9.0 }
                }
            };

            // Required boom is 25, next listed >= 25 is 30.
            var result = _resolver.Resolve(craneSpec, 10, 25, 5, _settings);

            Assert.Equal(LiftStatus.Liftable, result.LiftStatus);
            Assert.Equal(ChartStatus.ListedCapacityFound, result.ChartStatus);
            Assert.Equal(10.0, result.SelectedChartRadius);
            Assert.Equal(30.0, result.SelectedChartBoomLength);
            Assert.Equal(9.0, result.AvailableCapacity);
        }

        [Fact]
        public void TestOutsideChartEnvelope()
        {
            var craneSpec = new CraneSpecification
            {
                MaxRadius = 40,
                MaxBoomLength = 50,
                LoadChart = new List<TargetPoint>
                {
                    new TargetPoint { Radius = 10, BoomLength = 20, Capacity = 10.0 },
                    new TargetPoint { Radius = 20, BoomLength = 20, Capacity = 8.0 }
                }
            };

            // Exceeds max radius in chart (max radius is 20)
            var result = _resolver.Resolve(craneSpec, 25, 20, 5, _settings);

            Assert.Equal(LiftStatus.NotCharted, result.LiftStatus);
            Assert.Equal(ChartStatus.OutsideChartEnvelope, result.ChartStatus);
        }

        [Fact]
        public void TestOverweightCapacityLimit()
        {
            var craneSpec = new CraneSpecification
            {
                MaxRadius = 40,
                MaxBoomLength = 50,
                LoadChart = new List<TargetPoint>
                {
                    new TargetPoint { Radius = 10, BoomLength = 20, Capacity = 10.0 }
                }
            };

            // Weight is 12, capacity is 10
            var result = _resolver.Resolve(craneSpec, 10, 20, 12, _settings);

            Assert.Equal(LiftStatus.Overweight, result.LiftStatus);
            Assert.Equal(ChartStatus.ListedCapacityFound, result.ChartStatus);
            Assert.Equal(10.0, result.AvailableCapacity);
            Assert.Equal(-2.0, result.SafetyMargin);
        }

        [Fact]
        public void TestValidExactMatchCapacity()
        {
            var craneSpec = new CraneSpecification
            {
                MaxRadius = 40,
                MaxBoomLength = 50,
                LoadChart = new List<TargetPoint>
                {
                    new TargetPoint { Radius = 10, BoomLength = 20, Capacity = 10.0 }
                }
            };

            // Exact match for 10m radius, 20m boom
            var result = _resolver.Resolve(craneSpec, 10, 20, 5, _settings);

            Assert.Equal(LiftStatus.Liftable, result.LiftStatus);
            Assert.Equal(ChartStatus.ListedCapacityFound, result.ChartStatus);
            Assert.True(result.IsExactMatch);
            Assert.False(result.IsSteppedRadius);
            Assert.Equal(10.0, result.AvailableCapacity);
            Assert.Equal(5.0, result.SafetyMargin);
        }
    }
}
