using Xunit;
using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Models;
using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Services;
using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Settings;
using CraneOptimizer.Domain.Entities;
using System.Collections.Generic;
using CraneOptimizer.Application.Optimization.GeneticAlgorithms.DTOs;

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

        [Fact]
        public void TestProblematicElementsResolveToNotChartedZeroCell()
        {
            // Problematic columns: RequiredRadius = 18.734m, SelectedChartRadius = 20.0m, SelectedChartBoom = 20.1m, Capacity = 0.0
            var craneSpec = new CraneSpecification
            {
                MaxRadius = 40,
                MaxBoomLength = 50,
                LoadChart = new List<TargetPoint>
                {
                    new TargetPoint { Radius = 10, BoomLength = 20.1, Capacity = 10.0 },
                    new TargetPoint { Radius = 20, BoomLength = 20.1, Capacity = 0.0 } // explicit zero cell
                }
            };

            var result = _resolver.Resolve(craneSpec, 18.734, 19.829, 1.84, _settings);

            Assert.Equal(LiftStatus.NotCharted, result.LiftStatus);
            Assert.Equal(ChartStatus.InvalidZeroCell, result.ChartStatus);
            Assert.True(result.IsZeroCell);
            Assert.False(result.IsBlankCell);
            Assert.Equal(20.0, result.SelectedChartRadius);
            Assert.Equal(20.1, result.SelectedChartBoomLength);
        }

        [Fact]
        public void TestGreenReferenceElementsResolveToLiftable()
        {
            // Green references: RequiredRadius = 17.169m, SelectedChartRadius = 18.0m/20.0m, SelectedChartBoom = 20.1m, Capacity = 3.249
            var craneSpec = new CraneSpecification
            {
                MaxRadius = 40,
                MaxBoomLength = 50,
                LoadChart = new List<TargetPoint>
                {
                    new TargetPoint { Radius = 20, BoomLength = 20.1, Capacity = 3.249 }
                }
            };

            var result = _resolver.Resolve(craneSpec, 17.169, 18.358, 1.84, _settings);

            Assert.Equal(LiftStatus.Liftable, result.LiftStatus);
            Assert.Equal(ChartStatus.ListedCapacityFound, result.ChartStatus);
            Assert.Equal(3.249, result.AvailableCapacity);
            Assert.Equal(3.249 - 1.84, result.SafetyMargin);
        }

        [Fact]
        public void TestCentralizedPolicyClassificationDoesNotFallToUnknown()
        {
            var classification = LiftClassificationPolicy.Classify(
                LiftStatus.NotCharted,
                ChartStatus.InvalidZeroCell,
                1.84,
                19.829,
                40.0,
                true);

            Assert.Equal("InvalidZeroCell", classification.FailureReason);
            Assert.Equal("InvalidZeroCell", classification.FailureCategory);
            Assert.Contains("suggest review crane load chart zero cell rating.", classification.SuggestedAction);
        }

        [Fact]
        public void TestCentralizedFailureRankDeterminisitc()
        {
            Assert.Equal(5, LiftClassificationPolicy.GetFailureRank(LiftStatus.Liftable));
            Assert.Equal(4, LiftClassificationPolicy.GetFailureRank(LiftStatus.Overweight));
            Assert.Equal(3, LiftClassificationPolicy.GetFailureRank(LiftStatus.NotCharted));
            Assert.Equal(2, LiftClassificationPolicy.GetFailureRank(LiftStatus.HeightFail));
            Assert.Equal(1, LiftClassificationPolicy.GetFailureRank(LiftStatus.OutOfRadius));
            Assert.Equal(0, LiftClassificationPolicy.GetFailureRank(LiftStatus.SpecialCase));
        }

        [Fact]
        public void TestCentralizedStatisticsIncludeNotChartedExplicitly()
        {
            var stats = new LiftStatistics();
            LiftClassificationPolicy.UpdateStatistics(stats, LiftStatus.NotCharted);

            Assert.Equal(1, stats.NotCharted);
            Assert.Equal(0, stats.SpecialCase);
        }
    }
}
