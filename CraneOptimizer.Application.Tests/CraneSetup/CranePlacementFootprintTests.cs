using CraneOptimizer.Domain.Common;
using CraneOptimizer.Domain.Entities;
using CraneOptimizer.Domain.Schemas;
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
                Vertices = new List<DomainPoint3D>
                {
                    new(-5d, -5d, 0d),
                    new(5d, -5d, 0d),
                    new(5d, 5d, 0d),
                    new(-5d, 5d, 0d)
                }
            };
        }
    }
}
