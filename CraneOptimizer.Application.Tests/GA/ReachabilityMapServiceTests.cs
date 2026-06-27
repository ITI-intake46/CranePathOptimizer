using Xunit;
using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Services;
using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Engines.Constraints;
using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Settings;
using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Models;
using CraneOptimizer.Domain.Common;
using CraneOptimizer.Domain.Entities;
using CraneOptimizer.Domain.Schemas;
using System.Collections.Generic;
using System.Linq;

namespace CraneOptimizer.Application.Tests.GA
{
    public class ReachabilityMapServiceTests
    {
        private readonly ConstraintEngine _constraintEngine;
        private readonly LoadChartCapacityResolver _capacityResolver;
        private readonly ReachabilityMapService _service;

        public ReachabilityMapServiceTests()
        {
            _constraintEngine = new ConstraintEngine();
            _capacityResolver = new LoadChartCapacityResolver();
            _service = new ReachabilityMapService(_constraintEngine, _capacityResolver);
        }

        [Fact]
        public void TestTrivialCase()
        {
            // Single element at (10, 10, 0)
            var elements = new List<PrecastElement>
            {
                new PrecastElement
                {
                    ElementID = "EL-1",
                    ElementCOG = new DomainPoint3D(10, 10, 0),
                    ElementHeight = 1.0,
                    ElementWT = 5.0
                }
            };

            // Allowed zone around (10, 10)
            var allowedZone = new GreenAreaZone
            {
                Name = "Zone1",
                Vertices = new List<DomainPoint3D>
                {
                    new DomainPoint3D(8, 8, 0),
                    new DomainPoint3D(12, 8, 0),
                    new DomainPoint3D(12, 12, 0),
                    new DomainPoint3D(8, 12, 0)
                }
            };

            // Crane spec with sufficient radius and capacity
            var craneSpec = new CraneSpecification
            {
                Id = 1,
                MaxRadius = 20.0,
                MaxBoomLength = 30.0,
                LoadChart = new List<TargetPoint>
                {
                    new TargetPoint { Radius = 5.0, BoomLength = 10.0, Capacity = 15.0 },
                    new TargetPoint { Radius = 10.0, BoomLength = 15.0, Capacity = 10.0 },
                    new TargetPoint { Radius = 20.0, BoomLength = 25.0, Capacity = 8.0 }
                }
            };

            var settings = new GASettings
            {
                GridStep = 1.0,
                DeratingRatio = 1.0,
                SafetyFactor = 1.0,
                CraneFootprintRadius = 0.1
            };

            var hotSpots = _service.ComputeHotSpots(
                elements,
                new[] { allowedZone },
                Enumerable.Empty<GreenAreaZone>(),
                craneSpec,
                settings,
                topN: 5);

            Assert.NotEmpty(hotSpots);
            // The best point should be closest to (10, 10) where lift distance is smallest
            var bestPoint = hotSpots.First();
            Assert.Equal(1, bestPoint.ReachableCount);
            Assert.Equal(1.0, _service.LastDiagnostics?.MaxHotSpotCoverage);
        }

        [Fact]
        public void TestZeroCoverageCase()
        {
            // Element is at (100, 100, 0), far away
            var elements = new List<PrecastElement>
            {
                new PrecastElement
                {
                    ElementID = "EL-1",
                    ElementCOG = new DomainPoint3D(100, 100, 0),
                    ElementHeight = 1.0,
                    ElementWT = 5.0
                }
            };

            var allowedZone = new GreenAreaZone
            {
                Name = "Zone1",
                Vertices = new List<DomainPoint3D>
                {
                    new DomainPoint3D(0, 0, 0),
                    new DomainPoint3D(5, 0, 0),
                    new DomainPoint3D(5, 5, 0),
                    new DomainPoint3D(0, 5, 0)
                }
            };

            var craneSpec = new CraneSpecification
            {
                Id = 1,
                MaxRadius = 20.0,
                MaxBoomLength = 30.0,
                LoadChart = new List<TargetPoint>
                {
                    new TargetPoint { Radius = 10.0, BoomLength = 15.0, Capacity = 10.0 }
                }
            };

            var settings = new GASettings
            {
                GridStep = 1.0,
                CraneFootprintRadius = 0.1
            };

            var hotSpots = _service.ComputeHotSpots(
                elements,
                new[] { allowedZone },
                Enumerable.Empty<GreenAreaZone>(),
                craneSpec,
                settings,
                topN: 10);

            Assert.True(_service.LastDiagnostics?.ZeroCoveragePoints > 0);
            Assert.Equal(0.0, _service.LastDiagnostics?.MaxHotSpotCoverage);
            Assert.All(hotSpots, hs => Assert.Equal(0, hs.ReachableCount));
        }

        [Fact]
        public void TestTopNClipping()
        {
            var elements = new List<PrecastElement>
            {
                new PrecastElement
                {
                    ElementID = "EL-1",
                    ElementCOG = new DomainPoint3D(2, 2, 0),
                    ElementHeight = 1.0,
                    ElementWT = 5.0
                }
            };

            // Allowed zone generates 9 grid points at step=1.0: (0,0) to (2,2)
            var allowedZone = new GreenAreaZone
            {
                Name = "Zone1",
                Vertices = new List<DomainPoint3D>
                {
                    new DomainPoint3D(-0.1, -0.1, 0),
                    new DomainPoint3D(2.1, -0.1, 0),
                    new DomainPoint3D(2.1, 2.1, 0),
                    new DomainPoint3D(-0.1, 2.1, 0)
                }
            };

            var craneSpec = new CraneSpecification
            {
                Id = 1,
                MaxRadius = 20.0,
                MaxBoomLength = 30.0,
                LoadChart = new List<TargetPoint>
                {
                    new TargetPoint { Radius = 10.0, BoomLength = 15.0, Capacity = 10.0 }
                }
            };

            var settings = new GASettings
            {
                GridStep = 1.0,
                CraneFootprintRadius = 0.05
            };

            int topN = 3;
            var hotSpots = _service.ComputeHotSpots(
                elements,
                new[] { allowedZone },
                Enumerable.Empty<GreenAreaZone>(),
                craneSpec,
                settings,
                topN: topN);

            Assert.Equal(topN, hotSpots.Count);
        }
    }
}
