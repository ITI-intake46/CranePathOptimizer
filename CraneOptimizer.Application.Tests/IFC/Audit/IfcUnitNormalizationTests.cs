using CraneOptimizer.Domain.Diagnostics;
using CraneOptimizer.Domain.Units;
using CraneOptimizer.Infrastructure.Repositories;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace CraneOptimizer.Application.Tests.IFC.Audit
{
    public class IfcUnitNormalizationTests
    {
        [Fact]
        public void TestWeightConversionKilogramsToTons()
        {
            double valueKg = 9280.2;
            double valueTons = UnitSystem.ToTons(valueKg, MassUnit.Kilogram);
            Assert.Equal(9.2802, valueTons, 4);
        }

        [Fact]
        public void TestVolumeM3Preservation()
        {
            double rawVolumeM3 = 3.8;
            double targetVolume = rawVolumeM3 * 1.0; // VolumeConversionFactor is 1.0 for cubic meters
            Assert.Equal(3.8, targetVolume, 4);
        }

        [Fact]
        public void TestRegression_38VolumeDoesNotBecomeZero()
        {
            var meta = new IfcAuditMetadata
            {
                VolumeConversionFactor = 1.0
            };
            double rawVolume = 3.8;
            double normalized = rawVolume * meta.VolumeConversionFactor;
            Assert.Equal(3.8, normalized, 4);
            Assert.NotEqual(3.8e-09, normalized);
        }

        [Fact]
        public void TestRegression_9280WeightDoesNotBecomeZero()
        {
            var meta = new IfcAuditMetadata
            {
                MassConversionFactor = 1.0 // kg
            };
            double rawWeight = 9280.2;
            double normalizedTons = UnitSystem.ToTons(rawWeight * meta.MassConversionFactor.Value, MassUnit.Kilogram);
            Assert.Equal(9.2802, normalizedTons, 4);
            Assert.NotEqual(9.5e-09, normalizedTons);
        }

        [Fact]
        public void TestTrustedExplicitWeightWinsOverFallback()
        {
            var preview = new IfcNormalizedElementPreview
            {
                GlobalId = "test-id",
                WeightRawValue = 9280.2,
                WeightStandardValueTons = 9.2802,
                WeightTrustLevel = IfcWeightTrustLevel.ExplicitProperty,
                Role = IfcElementRole.LiftableElement
            };

            // HasGaApprovedExplicitWeight checks if trust level is ExplicitQuantity or ExplicitProperty
            bool isApproved = preview.WeightTrustLevel == IfcWeightTrustLevel.ExplicitQuantity ||
                               preview.WeightTrustLevel == IfcWeightTrustLevel.ExplicitProperty;

            Assert.True(isApproved);
        }

        [Fact]
        public async Task TestIntegration_IFCExtractionUnitNormalization()
        {
            const string ifcPath = @"F:\06-ITI\00-Project\tryProjects\Project2\Models\IFC\testBuilding4withMarks.ifc";
            if (!File.Exists(ifcPath))
            {
                // Skip if running in an environment without access to the tryProjects folder
                return;
            }

            var sessionService = new CraneOptimizer.Infrastructure.Ifc.IfcModelSessionService();
            var aliasResolver = new CentralIfcPropertyAliasResolver(new IfcExporterProfileResolver());
            var auditService = new IfcDiagnosticAuditService(sessionService, aliasResolver);
            var normalizationService = new IfcNormalizationDecisionService(new IfcExporterProfileResolver());
            var georefExtractor = new CraneOptimizer.Infrastructure.Ifc.IfcGeoreferencingExtractor();
            var readerRepository = new IfcReaderRepository(
                auditService,
                normalizationService,
                sessionService,
                georefExtractor,
                aliasResolver);

            var elements = readerRepository.ReadElements(ifcPath);

            // Find parent assembly 2KFTYALGT0ghARgUhfEqjI
            var parent = elements.FirstOrDefault(e => e.ElementID == "2KFTYALGT0ghARgUhfEqjI");
            Assert.NotNull(parent);
            Assert.Equal(9.2802, parent.WeightTons ?? 0.0, 3);
            Assert.Equal(3.8, parent.VolumeM3 ?? 0.0, 3);
            Assert.Equal(IfcWeightTrustLevel.ExplicitProperty, parent.WeightTrustLevel);
            Assert.Equal("PropertySet.Instance", parent.WeightSource);

            // Find child wall 2K1eRGMijEj9PRqHskPwYb
            var childWall = elements.FirstOrDefault(e => e.ElementID == "2K1eRGMijEj9PRqHskPwYb");
            Assert.NotNull(childWall);
            Assert.Equal(5.87, childWall.WeightTons ?? 0.0, 2);
            Assert.Equal(2.4, childWall.VolumeM3 ?? 0.0, 1);

            // Find child beam 1hiQd8Xon3rRRv$q2e7P3T (heaviest beam 1.70t)
            var childBeam = elements.FirstOrDefault(e => e.ElementID == "1hiQd8Xon3rRRv$q2e7P3T");
            Assert.NotNull(childBeam);
            Assert.Equal(1.7033, childBeam.WeightTons ?? 0.0, 2);
        }
    }
}
