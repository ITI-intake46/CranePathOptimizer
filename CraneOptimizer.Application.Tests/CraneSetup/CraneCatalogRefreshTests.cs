using CraneOptimizer.Domain.Common;
using CraneOptimizer.Domain.Entities;
using CraneOptimizer.UI.Core.Models;
using CraneOptimizer.UI.Features.F3_CraneSetup.ViewModels;

namespace CraneOptimizer.Application.Tests.CraneSetup
{
    public sealed class CraneCatalogRefreshTests
    {
        [Fact]
        public void ApplyCraneDefinition_UpdatesPlacedCraneAndPreservesMatchingSpecification()
        {
            var oldSpecification = new CraneSpecification
            {
                Id = 12,
                MinBoomLength = 10d,
                MaxBoomLength = 60d
            };
            var sceneCrane = new SceneCrane
            {
                CraneDefinition = new Crane
                {
                    Id = 7,
                    VehicleHeight = 3d,
                    Specifications = new List<CraneSpecification> { oldSpecification }
                },
                ActiveSpecification = oldSpecification,
                PivotOffset = new DomainPoint3D(0d, 0d, 2.4d),
                BoomLength = 55d
            };
            var refreshedSpecification = new CraneSpecification
            {
                Id = 12,
                MinBoomLength = 8d,
                MaxBoomLength = 40d
            };
            var refreshedCrane = new Crane
            {
                Id = 7,
                Name = "Updated crane",
                VehicleHeight = 5.005d,
                Specifications = new List<CraneSpecification> { refreshedSpecification }
            };

            CraneManagerViewModel.ApplyCraneDefinition(sceneCrane, refreshedCrane);

            Assert.Same(refreshedCrane, sceneCrane.CraneDefinition);
            Assert.Same(refreshedSpecification, sceneCrane.ActiveSpecification);
            Assert.Equal(4.004d, sceneCrane.PivotOffset.Z, precision: 3);
            Assert.Equal(40d, sceneCrane.BoomLength, precision: 3);
        }
    }
}
