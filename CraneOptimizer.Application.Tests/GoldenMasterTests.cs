using Xunit;
using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Engines.CraneConfiguration;
using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Engines.LiftingCalculation;
using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Engines.Constraints;
using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Settings;
using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Models;
using CraneOptimizer.Application.Tests.Legacy;
using CraneOptimizer.Domain.Common;
using CraneOptimizer.Domain.Entities;
using CraneOptimizer.Domain.Schemas;
using System;
using System.Collections.Generic;

namespace CraneOptimizer.Application.Tests
{
    public class GoldenMasterTests
    {
        [Fact]
        public void TestLiftingEnginesAgainstLegacy()
        {
            var configEngine = new CraneConfigurationEngine();
            var liftingEngine = new LiftingCalculationEngine(configEngine);

            var settings = new GASettings
            {
                WireLength = 3.0,
                DeratingRatio = 0.85,
                SafetyFactor = 0.90
            };

            var craneSpec = new CraneSpecification
            {
                MaxRadius = 40,
                MaxBoomLength = 50,
                LoadChart = new List<TargetPoint>
                {
                    new TargetPoint { Radius = 10, BoomLength = 20, Capacity = 50 },
                    new TargetPoint { Radius = 10, BoomLength = 30, Capacity = 40 },
                    new TargetPoint { Radius = 20, BoomLength = 20, Capacity = 30 },
                    new TargetPoint { Radius = 20, BoomLength = 30, Capacity = 20 }
                }
            };

            var cranePositions = new List<DomainPoint3D>
            {
                new DomainPoint3D(0, 0, 0),
                new DomainPoint3D(5, 5, 2),
                new DomainPoint3D(-10, 20, 5)
            };

            var elements = new List<PrecastElement>
            {
                new PrecastElement { ElementCOG = new DomainPoint3D(10, 10, 10), ElementHeight = 4.0, ElementWT = 12.0 },
                new PrecastElement { ElementCOG = new DomainPoint3D(20, -5, 12), ElementHeight = 3.5, ElementWT = 15.5 },
                new PrecastElement { ElementCOG = new DomainPoint3D(-15, 25, 8), ElementHeight = 5.0, ElementWT = 8.0 }
            };

            foreach (var pos in cranePositions)
            {
                foreach (var el in elements)
                {
                    // 1. Radius
                    double radiusNew = liftingEngine.CalculateRadius(pos, el);
                    double radiusOld = LiftCalculationLegacy.CalculateRadius(pos, el);
                    Assert.Equal(radiusOld, radiusNew, 7);

                    // 2. Height
                    double heightNew = liftingEngine.CalculateLiftHeight(el, pos, settings);
                    double heightOld = LiftCalculationLegacy.CalculateLiftHeight(el, pos, settings);
                    Assert.Equal(heightOld, heightNew, 7);

                    // 3. Boom Length
                    double boomNew = configEngine.CalculateRequiredBoomLength(radiusNew, heightNew);
                    double boomOld = LiftCalculationLegacy.CalculateRequiredBoomLength(radiusOld, heightOld);
                    Assert.Equal(boomOld, boomNew, 7);

                    // 4. Capacity
                    double capNew = configEngine.CraneCapacity(craneSpec, radiusNew, boomNew, settings);
                    double capOld = LiftCalculationLegacy.CraneCapacity(craneSpec, radiusOld, boomOld, settings);
                    Assert.Equal(capOld, capNew, 7);

                    // 5. Status
                    double reqWeight = liftingEngine.CalculateRequiredWeight(el, settings);
                    var statusNew = liftingEngine.DetermineLiftStatus(radiusNew, boomNew, reqWeight, craneSpec, settings);
                    var statusOld = LiftCalculationLegacy.DetermineLiftStatus(radiusOld, boomOld, reqWeight, craneSpec, settings);
                    Assert.Equal(statusOld, statusNew);
                }
            }
        }

        [Fact]
        public void TestConstraintEngineAgainstLegacy()
        {
            var engine = new ConstraintEngine();
            var settings = new GASettings
            {
                CraneFootprintRadius = 2.5,
                MinimumRelocationDistance = 8.0,
                MaxRadiusForNormalisation = 40.0,
                MaxCranePositions = 5
            };

            var allowedZone = new GreenAreaZone
            {
                Vertices = new List<DomainPoint3D>
                {
                    new DomainPoint3D(-50, -50, 0),
                    new DomainPoint3D(50, -50, 0),
                    new DomainPoint3D(50, 50, 0),
                    new DomainPoint3D(-50, 50, 0)
                }
            };

            var forbiddenZone = new GreenAreaZone
            {
                Vertices = new List<DomainPoint3D>
                {
                    new DomainPoint3D(-5, -5, 0),
                    new DomainPoint3D(5, -5, 0),
                    new DomainPoint3D(5, 5, 0),
                    new DomainPoint3D(-5, 5, 0)
                }
            };

            var allowedZones = new List<GreenAreaZone> { allowedZone };
            var forbiddenZones = new List<GreenAreaZone> { forbiddenZone };

            var testPoints = new List<DomainPoint3D>
            {
                new DomainPoint3D(0, 0, 0),
                new DomainPoint3D(10, 10, 0),
                new DomainPoint3D(3, 3, 0),
                new DomainPoint3D(12, 12, 0),
                new DomainPoint3D(45, -45, 0),
                new DomainPoint3D(55, 55, 0)
            };

            // 1. IsValidSetupPoint
            foreach (var p in testPoints)
            {
                bool validNew = engine.IsValidSetupPoint(p, allowedZones, forbiddenZones, settings);
                bool validOld = CranePlacementConstraintServiceLegacy.IsValidSetupPoint(p, allowedZones, forbiddenZones, settings);
                Assert.Equal(validOld, validNew);
            }

            // 2. IsMeaningfullySeparated
            var existing = new List<DomainPoint3D> { new DomainPoint3D(10, 10, 0) };
            foreach (var p in testPoints)
            {
                bool sepNew = engine.IsMeaningfullySeparated(p, existing, settings);
                bool sepOld = CranePlacementConstraintServiceLegacy.IsMeaningfullySeparated(p, existing, settings);
                Assert.Equal(sepOld, sepNew);
            }

            // 3. FilterDistinctValidPoints
            var filteredNew = engine.FilterDistinctValidPoints(testPoints, allowedZones, forbiddenZones, settings);
            var filteredOld = CranePlacementConstraintServiceLegacy.FilterDistinctValidPoints(testPoints, allowedZones, forbiddenZones, settings);
            Assert.Equal(filteredOld.Count, filteredNew.Count);
            for (int i = 0; i < filteredNew.Count; i++)
            {
                Assert.Equal(filteredOld[i].X, filteredNew[i].X, 7);
                Assert.Equal(filteredOld[i].Y, filteredNew[i].Y, 7);
            }

            // 4. CalculateSimilarityPenalty
            double penaltyNew = engine.CalculateSimilarityPenalty(testPoints, settings);
            double penaltyOld = CranePlacementConstraintServiceLegacy.CalculateSimilarityPenalty(testPoints, settings);
            Assert.Equal(penaltyOld, penaltyNew, 7);

            // 5. CalculateNormalizedRelocationDistance
            double distNew = engine.CalculateNormalizedRelocationDistance(testPoints, settings);
            double distOld = CranePlacementConstraintServiceLegacy.CalculateNormalizedRelocationDistance(testPoints, settings);
            Assert.Equal(distOld, distNew, 7);
        }
    }
}
