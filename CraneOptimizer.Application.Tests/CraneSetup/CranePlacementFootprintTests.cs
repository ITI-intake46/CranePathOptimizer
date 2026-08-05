using CraneOptimizer.Domain.Common;
using CraneOptimizer.Domain.Entities;
using CraneOptimizer.Domain.Schemas;
using CraneOptimizer.Domain.Schemas.Enums;
using CraneOptimizer.UI.Core.Models;
using CraneOptimizer.UI.Features.F3_CraneSetup.Services;

namespace CraneOptimizer.Application.Tests.CraneSetup
{
    public sealed class CranePlacementFootprintTests
    {
        [Fact]
        public void ValidatePlacement_UsesVehicleFootprintWhenOnRubber()
        {
            SceneCrane crane = CreateCrane("On Rubber");
            var service = new CraneValidationService();

            bool isValid = service.ValidatePlacement(crane, new[] { CreateTenMeterSquare() });

            Assert.True(isValid);
        }

        [Fact]
        public void ValidatePlacement_UsesOutriggerFootprintForOutriggerOperation()
        {
            SceneCrane crane = CreateCrane("100%");
            var service = new CraneValidationService();

            bool isValid = service.ValidatePlacement(crane, new[] { CreateTenMeterSquare() });

            Assert.False(isValid);
        }

        [Fact]
        public void ValidatePlacement_DoesNotRequireMaximumReachCircleInsideAllowedZone()
        {
            SceneCrane crane = CreateCrane("On Rubber");
            crane.ActiveSpecification!.MaxRadius = 100d;
            var service = new CraneValidationService();

            bool isValid = service.ValidatePlacement(crane, new[] { CreateTenMeterSquare() });

            Assert.True(isValid);
        }

        [Fact]
        public void ValidatePlacement_RejectsForbiddenZoneOverlap()
        {
            SceneCrane crane = CreateCrane("On Rubber");
            crane.Position = new DomainPoint3D(2d, 0d, 0d);
            var service = new CraneValidationService();

            bool isValid = service.ValidatePlacement(crane, new[]
            {
                CreateTenMeterSquare(),
                CreateForbiddenSquare()
            });

            Assert.False(isValid);
        }

        private static SceneCrane CreateCrane(string configuration)
        {
            var specification = new CraneSpecification
            {
                OutriggerConfig = configuration,
                MaxRadius = 0d
            };
            var definition = new Crane
            {
                VehicleLength = 4d,
                VehicleWidth = 2d,
                OutriggerLength = 12d,
                OutriggerWidth = 12d
            };

            return new SceneCrane
            {
                CraneDefinition = definition,
                ActiveSpecification = specification,
                Position = new DomainPoint3D(0d, 0d, 0d)
            };
        }

        private static GreenAreaZone CreateTenMeterSquare()
        {
            return new GreenAreaZone
            {
                Name = "Test zone",
                Type = ZoneType.Allowed,
                Vertices = new List<DomainPoint3D>
                {
                    new(-5d, -5d, 0d),
                    new(5d, -5d, 0d),
                    new(5d, 5d, 0d),
                    new(-5d, 5d, 0d)
                }
            };
        }

        private static GreenAreaZone CreateForbiddenSquare()
        {
            return new GreenAreaZone
            {
                Name = "Forbidden",
                Type = ZoneType.Forbidden,
                Vertices = new List<DomainPoint3D>
                {
                    new(3d, -1d, 0d),
                    new(4d, -1d, 0d),
                    new(4d, 1d, 0d),
                    new(3d, 1d, 0d)
                }
            };
        }
    }
}
