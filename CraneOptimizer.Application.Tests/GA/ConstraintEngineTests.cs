using Xunit;
using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Engines.Constraints;
using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Settings;
using CraneOptimizer.Domain.Common;
using CraneOptimizer.Domain.Schemas;
using System.Collections.Generic;

namespace CraneOptimizer.Application.Tests
{
    public class ConstraintEngineTests
    {
        [Fact]
        public void TestIsValidSetupPoint()
        {
            var engine = new ConstraintEngine();
            var settings = new GASettings
            {
                CraneFootprintRadius = 2.0
            };

            // Simple square allowed zone from (0,0) to (10,10)
            var allowedZone = new GreenAreaZone
            {
                Vertices = new List<DomainPoint3D>
                {
                    new DomainPoint3D(0, 0, 0),
                    new DomainPoint3D(10, 0, 0),
                    new DomainPoint3D(10, 10, 0),
                    new DomainPoint3D(0, 10, 0)
                }
            };

            // Forbidden zone from (4,4) to (6,6)
            var forbiddenZone = new GreenAreaZone
            {
                Vertices = new List<DomainPoint3D>
                {
                    new DomainPoint3D(4, 4, 0),
                    new DomainPoint3D(6, 4, 0),
                    new DomainPoint3D(6, 6, 0),
                    new DomainPoint3D(4, 6, 0)
                }
            };

            var allowedZones = new List<GreenAreaZone> { allowedZone };
            var forbiddenZones = new List<GreenAreaZone> { forbiddenZone };

            // Case 1: Point is inside allowed zone and far from forbidden zone
            var pValid = new DomainPoint3D(2, 2, 0);
            Assert.True(engine.IsValidSetupPoint(pValid, allowedZones, forbiddenZones, settings));

            // Case 2: Point is outside allowed zone
            var pOutside = new DomainPoint3D(12, 12, 0);
            Assert.False(engine.IsValidSetupPoint(pOutside, allowedZones, forbiddenZones, settings));

            // Case 3: Point is inside allowed zone but footprint overlaps forbidden zone
            var pOverlapsForbidden = new DomainPoint3D(5, 3.5, 0); // Center is at (5, 3.5), footprint radius is 2, so it reaches Y=5.5 (overlaps Y=4..6)
            Assert.False(engine.IsValidSetupPoint(pOverlapsForbidden, allowedZones, forbiddenZones, settings));
        }

        [Fact]
        public void TestIsMeaningfullySeparated()
        {
            var engine = new ConstraintEngine();
            var settings = new GASettings
            {
                MinimumRelocationDistance = 10.0
            };

            var existing = new List<DomainPoint3D>
            {
                new DomainPoint3D(0, 0, 0),
                new DomainPoint3D(20, 20, 0)
            };

            // Case 1: Far enough
            var pFar = new DomainPoint3D(0, 15, 0); // Distance is 15.0 >= 10.0
            Assert.True(engine.IsMeaningfullySeparated(pFar, existing, settings));

            // Case 2: Too close
            var pClose = new DomainPoint3D(0, 5, 0); // Distance is 5.0 < 10.0
            Assert.False(engine.IsMeaningfullySeparated(pClose, existing, settings));
        }

        [Fact]
        public void TestCalculateSimilarityPenaltyAndRelocationDistance()
        {
            var engine = new ConstraintEngine();
            var settings = new GASettings
            {
                MinimumRelocationDistance = 10.0,
                MaxRadiusForNormalisation = 40.0,
                MaxCranePositions = 4
            };

            // If less than 2 points, penalty and distance are 0
            Assert.Equal(0, engine.CalculateSimilarityPenalty(new List<DomainPoint3D> { new DomainPoint3D(0,0,0) }, settings));
            Assert.Equal(0, engine.CalculateNormalizedRelocationDistance(new List<DomainPoint3D> { new DomainPoint3D(0,0,0) }, settings));

            var points = new List<DomainPoint3D>
            {
                new DomainPoint3D(0, 0, 0),
                new DomainPoint3D(0, 5, 0) // Distance is 5.0, which is < 10.0
            };

            // Similarity Penalty:
            // pairCount = 1
            // distance = 5.0 < 10.0
            // penalty = (10 - 5) / 10 = 0.5
            double penalty = engine.CalculateSimilarityPenalty(points, settings);
            Assert.Equal(0.5, penalty, 5);

            // Normalized relocation distance:
            // totalDistance = 5.0
            // normalizer = MaxRadius (40) * (MaxPositions (4) - 1) = 120
            // ratio = 5 / 120 = 0.041666...
            double distance = engine.CalculateNormalizedRelocationDistance(points, settings);
            Assert.Equal(5.0 / 120.0, distance, 5);
        }
    }
}
