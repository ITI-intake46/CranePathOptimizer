using Xunit;
using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Engines.Constraints;
using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Services;
using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Settings;
using CraneOptimizer.Domain.Common;
using CraneOptimizer.Domain.Schemas;
using CraneOptimizer.Domain.Schemas.Enums;
using System;
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
        public void IsValidSetupPoint_RejectsCenterInsideAllowedWhenFootprintCrossesBoundary()
        {
            var engine = new ConstraintEngine();
            var settings = new GASettings { CraneFootprintRadius = 2.0 };

            Assert.False(engine.IsValidSetupPoint(
                new DomainPoint3D(1, 5, 0),
                new[] { CreateSquare(0, 0, 10, 10, ZoneType.Allowed) },
                Array.Empty<GreenAreaZone>(),
                settings));
        }

        [Fact]
        public void IsValidSetupPoint_AcceptsFootprintFullyInsideAllowedZone()
        {
            var engine = new ConstraintEngine();
            var settings = new GASettings { CraneFootprintRadius = 2.0 };

            Assert.True(engine.IsValidSetupPoint(
                new DomainPoint3D(5, 5, 0),
                new[] { CreateSquare(0, 0, 10, 10, ZoneType.Allowed) },
                Array.Empty<GreenAreaZone>(),
                settings));
        }

        [Fact]
        public void IsValidSetupPoint_AllowsFootprintTouchingAllowedBoundaryWithinTolerance()
        {
            var engine = new ConstraintEngine();
            var settings = new GASettings { CraneFootprintRadius = 2.0 };

            Assert.True(engine.IsValidSetupPoint(
                new DomainPoint3D(2, 5, 0),
                new[] { CreateSquare(0, 0, 10, 10, ZoneType.Allowed) },
                Array.Empty<GreenAreaZone>(),
                settings));
        }

        [Fact]
        public void IsValidSetupPoint_RejectsCenterOutsideAllowedZone()
        {
            var engine = new ConstraintEngine();
            var settings = new GASettings { CraneFootprintRadius = 1.0 };

            Assert.False(engine.IsValidSetupPoint(
                new DomainPoint3D(12, 5, 0),
                new[] { CreateSquare(0, 0, 10, 10, ZoneType.Allowed) },
                Array.Empty<GreenAreaZone>(),
                settings));
        }

        [Fact]
        public void IsValidSetupPoint_RejectsFootprintOverlappingForbiddenWhenCenterOutsideForbidden()
        {
            var engine = new ConstraintEngine();
            var settings = new GASettings { CraneFootprintRadius = 1.5 };

            Assert.False(engine.IsValidSetupPoint(
                new DomainPoint3D(2.5, 5, 0),
                new[] { CreateSquare(0, 0, 10, 10, ZoneType.Allowed) },
                new[] { CreateSquare(4, 4, 6, 6, ZoneType.Forbidden) },
                settings));
        }

        [Fact]
        public void IsValidSetupPoint_RejectsCenterInsideForbiddenZone()
        {
            var engine = new ConstraintEngine();
            var settings = new GASettings { CraneFootprintRadius = 0.5 };

            Assert.False(engine.IsValidSetupPoint(
                new DomainPoint3D(5, 5, 0),
                new[] { CreateSquare(0, 0, 10, 10, ZoneType.Allowed) },
                new[] { CreateSquare(4, 4, 6, 6, ZoneType.Forbidden) },
                settings));
        }

        [Fact]
        public void IsValidSetupPoint_RejectsFootprintSplitAcrossSeparateAllowedZones()
        {
            var engine = new ConstraintEngine();
            var settings = new GASettings { CraneFootprintRadius = 1.0 };
            var allowedZones = new[]
            {
                CreateSquare(0, 0, 4, 10, ZoneType.Allowed),
                CreateSquare(4.5, 0, 8.5, 10, ZoneType.Allowed)
            };

            Assert.False(engine.IsValidSetupPoint(
                new DomainPoint3D(3.75, 5, 0),
                allowedZones,
                Array.Empty<GreenAreaZone>(),
                settings));
        }

        [Fact]
        public void IsValidSetupPoint_AllowsRenderedReachCircleCrossingBoundaryWhenFootprintFits()
        {
            var engine = new ConstraintEngine();
            var settings = new GASettings
            {
                CraneFootprintRadius = 0.5,
                RenderedOperatingEnvelopeRadius = 2.0
            };

            Assert.True(engine.IsValidSetupPoint(
                new DomainPoint3D(1, 5, 0),
                new[] { CreateSquare(0, 0, 10, 10, ZoneType.Allowed) },
                Array.Empty<GreenAreaZone>(),
                settings));
        }

        [Fact]
        public void IsValidSetupPoint_AcceptsWhenFootprintFitsButRenderedEnvelopeDoesNot()
        {
            var engine = new ConstraintEngine();
            var settings = new GASettings
            {
                CraneFootprintRadius = 1.0,
                RenderedOperatingEnvelopeRadius = 6.0
            };

            Assert.True(engine.IsValidSetupPoint(
                new DomainPoint3D(5, 5, 0),
                new[] { CreateSquare(0, 0, 10, 10, ZoneType.Allowed) },
                Array.Empty<GreenAreaZone>(),
                settings));
        }

        [Fact]
        public void IsValidSetupPoint_AcceptsFootprintFullyInsideAllowedZoneRegardlessOfRenderedEnvelope()
        {
            var engine = new ConstraintEngine();
            var settings = new GASettings
            {
                CraneFootprintRadius = 1.0,
                RenderedOperatingEnvelopeRadius = 5.0
            };

            var validation = engine.ValidateSetupPointDetailed(
                new DomainPoint3D(10, 10, 0),
                new[] { CreateSquare(0, 0, 20, 20, ZoneType.Allowed) },
                Array.Empty<GreenAreaZone>(),
                settings);

            Assert.True(validation.IsValid);
            Assert.Equal(1.0, validation.RequiredContainmentRadius, 6);
            Assert.Equal(10.0, validation.MinimumAllowedBoundaryDistance, 6);
        }

        [Fact]
        public void IsValidSetupPoint_IgnoresRenderedEnvelopeTouchingAllowedBoundary()
        {
            var engine = new ConstraintEngine();
            var settings = new GASettings
            {
                CraneFootprintRadius = 0.5,
                RenderedOperatingEnvelopeRadius = 2.0000004
            };

            Assert.True(engine.IsValidSetupPoint(
                new DomainPoint3D(2, 5, 0),
                new[] { CreateSquare(0, 0, 10, 10, ZoneType.Allowed) },
                Array.Empty<GreenAreaZone>(),
                settings));
        }

        [Fact]
        public void IsValidSetupPoint_AllowsRenderedEnvelopeOverlappingForbiddenWhenFootprintDoesNot()
        {
            var engine = new ConstraintEngine();
            var settings = new GASettings
            {
                CraneFootprintRadius = 0.5,
                RenderedOperatingEnvelopeRadius = 2.0
            };

            Assert.True(engine.IsValidSetupPoint(
                new DomainPoint3D(8.5, 11, 0),
                new[] { CreateSquare(0, 0, 20, 20, ZoneType.Allowed) },
                new[] { CreateSquare(10, 10, 12, 12, ZoneType.Forbidden) },
                settings));
        }

        [Fact]
        public void IsValidSetupPoint_AllowsRenderedEnvelopeSplitAcrossSeparateAllowedZonesWhenFootprintFitsOneZone()
        {
            var engine = new ConstraintEngine();
            var settings = new GASettings
            {
                CraneFootprintRadius = 0.5,
                RenderedOperatingEnvelopeRadius = 2.0
            };
            var allowedZones = new[]
            {
                CreateSquare(0, 0, 4, 10, ZoneType.Allowed),
                CreateSquare(4.25, 0, 8.25, 10, ZoneType.Allowed)
            };

            Assert.True(engine.IsValidSetupPoint(
                new DomainPoint3D(3.5, 5, 0),
                allowedZones,
                Array.Empty<GreenAreaZone>(),
                settings));
        }

        [Fact]
        public void IsValidSetupPoint_AllowsConcaveAllowedZoneWhenOnlyRenderedEnvelopeCrossesConcaveBoundary()
        {
            var engine = new ConstraintEngine();
            var settings = new GASettings
            {
                CraneFootprintRadius = 0.5,
                RenderedOperatingEnvelopeRadius = 2.0
            };

            var concave = new GreenAreaZone
            {
                Name = "ConcaveAllowed",
                Type = ZoneType.Allowed,
                Vertices = new List<DomainPoint3D>
                {
                    new(0, 0, 0),
                    new(10, 0, 0),
                    new(10, 4, 0),
                    new(4, 4, 0),
                    new(4, 10, 0),
                    new(0, 10, 0)
                }
            };

            Assert.True(engine.IsValidSetupPoint(
                new DomainPoint3D(3, 3, 0),
                new[] { concave },
                Array.Empty<GreenAreaZone>(),
                settings));
        }

        [Fact]
        public void ResolveRequiredZoneContainmentRadius_UsesGroundFootprintOnly()
        {
            var settings = new GASettings
            {
                CraneFootprintRadius = 3.0,
                RenderedOperatingEnvelopeRadius = 8.0
            };

            Assert.Equal(3.0, CraneEnvelopeContainmentRadiusResolver.ResolveRequiredZoneContainmentRadius(settings));
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

        private static GreenAreaZone CreateSquare(double minX, double minY, double maxX, double maxY, ZoneType type)
        {
            return new GreenAreaZone
            {
                Name = type.ToString(),
                Type = type,
                Vertices = new List<DomainPoint3D>
                {
                    new DomainPoint3D(minX, minY, 0),
                    new DomainPoint3D(maxX, minY, 0),
                    new DomainPoint3D(maxX, maxY, 0),
                    new DomainPoint3D(minX, maxY, 0)
                }
            };
        }
    }
}
