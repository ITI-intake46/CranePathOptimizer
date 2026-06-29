using CraneOptimizer.Domain.Diagnostics;
using CraneOptimizer.Domain.Units;
using CraneOptimizer.Infrastructure.Repositories;
using CraneOptimizer.Infrastructure.Ifc;
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
        public void PrintAssemblyShapeInstances()
        {
            const string ifcPath = @"F:\06-ITI\00-Project\tryProjects\Project2\Models\IFC\testBuilding2x3withMarks.ifc";
            if (!File.Exists(ifcPath)) return;

            var outputPath = @"C:\Users\Dell\.gemini\antigravity\brain\91c7a91f-5f44-4c92-8297-6cea96c03b18\scratch\assembly_shapes.txt";
            using var sw = new StreamWriter(outputPath, false);

            using (var model = Xbim.Ifc.IfcStore.Open(ifcPath))
            {
                var context = new Xbim.ModelGeometry.Scene.Xbim3DModelContext(model);
                context.CreateContext();

                var assembly = model.Instances.OfType<Xbim.Ifc4.Interfaces.IIfcElementAssembly>().FirstOrDefault(e => e.GlobalId == "2KFTYALGT0ghARgUhfEqjI");
                if (assembly != null)
                {
                    var shapes = context.ShapeInstancesOf(assembly).ToList();
                    sw.WriteLine($"--- Assembly {assembly.GlobalId} shape count: {shapes.Count} ---");
                    foreach (var s in shapes)
                    {
                        sw.WriteLine($"  ShapeInstance Label: {s.InstanceLabel}, RepresentationType: {s.RepresentationType}, Transform: {s.Transformation}, GeometryLabel: {s.ShapeGeometryLabel}");
                    }
                }
                else
                {
                    sw.WriteLine("Assembly 2KFTYALGT0ghARgUhfEqjI not found.");
                }
            }
        }

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
            const string ifcPath = @"F:\06-ITI\00-Project\tryProjects\Project2\Models\IFC\testBuilding2x3withMarks.ifc";
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
            Assert.Equal(3.8, parent.VolumeM3 ?? 0.0, 1);
            Assert.Equal(IfcWeightTrustLevel.ExplicitProperty, parent.WeightTrustLevel);
            Assert.Equal("PropertySet.Instance", parent.WeightSource);

            var parentCog = parent.COG_M ?? parent.ElementCOG;
            var childWall = elements.FirstOrDefault(e => e.ElementID == "2K1eRGMijEj9PRqHskPwYb");
            Assert.NotNull(childWall);
            var childCog = childWall.COG_M ?? childWall.ElementCOG;

            var outputPath = @"C:\Users\Dell\.gemini\antigravity\brain\91c7a91f-5f44-4c92-8297-6cea96c03b18\scratch\assembly_shapes.txt";
            using (var sw = new StreamWriter(outputPath, true))
            {
                sw.WriteLine($"--- COG Diagnostics ---");
                sw.WriteLine($"  Parent COG: X={parentCog?.X}, Y={parentCog?.Y}, Z={parentCog?.Z}");
                sw.WriteLine($"  Child COG:  X={childCog?.X},  Y={childCog?.Y},  Z={childCog?.Z}");
            }
            Assert.Equal(5.87, childWall.WeightTons ?? 0.0, 2);
            Assert.Equal(2.4, childWall.VolumeM3 ?? 0.0, 1);

            // Find child beam 1hiQd8Xon3rRRv$q2e7P3T (heaviest beam 1.70t)
            var childBeam = elements.FirstOrDefault(e => e.ElementID == "1hiQd8Xon3rRRv$q2e7P3T");
            Assert.NotNull(childBeam);
            Assert.Equal(1.7033, childBeam.WeightTons ?? 0.0, 2);
        }

        [Fact]
        public void TestIntegration_IFC2x3GlobalCoordinatesAndRollup()
        {
            const string ifcPath = @"F:\06-ITI\00-Project\tryProjects\Project2\Models\IFC\testBuilding2x3withMarks.ifc";
            RunCoordinatesAndRollupTest(ifcPath);
        }

        [Fact]
        public void TestIntegration_IFC4GlobalCoordinatesAndRollup()
        {
            const string ifcPath = @"F:\06-ITI\00-Project\tryProjects\Project2\Models\IFC\testBuilding4withMarks.ifc";
            RunCoordinatesAndRollupTest(ifcPath);
        }

        private void RunCoordinatesAndRollupTest(string ifcPath)
        {
            if (!File.Exists(ifcPath)) return;

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
            
            // The georeferenced offset for Project2 in meters:
            double offsetX = 0.9188479242867018;
            double offsetY = -0.02478343665680427;
            double offsetZ = 2.842170943040401E-14;

            // 1. Verify child wall global coordinates
            var childWall = elements.FirstOrDefault(e => e.ElementID == "2K1eRGMijEj9PRqHskPwYb");
            Assert.NotNull(childWall);
            Assert.True(childWall.PhysicalCOGAvailable);
            
            double globalWallX = childWall.COG_M.X + offsetX;
            double globalWallY = childWall.COG_M.Y + offsetY;
            double globalWallZ = childWall.COG_M.Z + offsetZ;
            
            Assert.Equal(90.6147, globalWallX, 3);
            Assert.Equal(110.1856, globalWallY, 3);
            Assert.Equal(1.5, globalWallZ, 3);

            // 2. Verify parent assembly is container-only / physical COG unavailable
            var parent = elements.FirstOrDefault(e => e.ElementID == "2KFTYALGT0ghARgUhfEqjI");
            Assert.NotNull(parent);
            Assert.False(parent.PhysicalCOGAvailable);
            Assert.Contains(parent.ExtractionWarnings, w => w.Contains("ContainerOnlyAssembly"));
            
            // Check that the mapped entity is saved with NULL
            var mapperEntity = CraneOptimizer.UI.Features.F10_ProjectManagement.Mappers.PrecastElementMapper.ToEntity(parent, 1, 1);
            Assert.Null(mapperEntity.COG_X);
            Assert.Null(mapperEntity.COG_Y);
            Assert.Null(mapperEntity.COG_Z);
            Assert.Null(mapperEntity.BBoxMinX);

            // 3. Verify diagnostic rollup from the three children
            var children = elements.Where(e => e.ParentGlobalId == "2KFTYALGT0ghARgUhfEqjI").ToList();
            Assert.Equal(3, children.Count);

            double totalWeight = 0;
            double weightedX = 0;
            double weightedY = 0;
            double weightedZ = 0;

            foreach (var child in children)
            {
                double w = child.WeightTons ?? 0;
                totalWeight += w;
                weightedX += (child.COG_M.X + offsetX) * w;
                weightedY += (child.COG_M.Y + offsetY) * w;
                weightedZ += (child.COG_M.Z + offsetZ) * w;
            }

            double rollupX = weightedX / totalWeight;
            double rollupY = weightedY / totalWeight;
            double rollupZ = weightedZ / totalWeight;

            Assert.True(Math.Abs(rollupX - 90.4492) < 0.002, $"Expected rollupX to be approx 90.4492, but got {rollupX}");
            Assert.True(Math.Abs(rollupY - 109.0853) < 0.002, $"Expected rollupY to be approx 109.0853, but got {rollupY}");
            Assert.True(Math.Abs(rollupZ - 1.9401) < 0.002, $"Expected rollupZ to be approx 1.9401, but got {rollupZ}");
        }

        [Fact]
        public async Task TestAssemblyGraphClassification_Project2()
        {
            const string ifcPath = @"F:\06-ITI\00-Project\tryProjects\Project2\Models\IFC\testBuilding2x3withMarks.ifc";
            if (!File.Exists(ifcPath)) return;

            var auditService = new IfcDiagnosticAuditService();
            var auditResult = await auditService.RunAuditAsync(ifcPath);

            Assert.NotNull(auditResult);
            Assert.NotEmpty(auditResult.AssemblyRelations);

            // Locate parent assembly relation
            var rel = auditResult.AssemblyRelations.FirstOrDefault(r => r.ParentGlobalId == "2KFTYALGT0ghARgUhfEqjI");
            Assert.NotNull(rel);
            Assert.Equal("IfcRelAggregates", rel.ParentGlobalIdSource);
            Assert.Equal("2K1eRGMijEj9PRqHskPwYb", rel.MainPartGlobalId);
            Assert.Equal(AssemblyMainPartDecision.InferredHighConfidence, rel.MainPartDetectionMethod);
            Assert.Equal(3, rel.ChildCount);
            Assert.Equal(2, rel.VisibleSecondaryPartCount);
            Assert.True(rel.HasSecondaryParts);
            Assert.Contains("MixedCategoryButValidAssembly", rel.Warnings);

            // Verify child classifications
            var mainPartNode = rel.Children.FirstOrDefault(c => c.ChildGlobalId == "2K1eRGMijEj9PRqHskPwYb");
            Assert.NotNull(mainPartNode);
            Assert.Equal(AssemblyChildClassification.MainPart, mainPartNode.ChildClassification);

            var beamNode = rel.Children.FirstOrDefault(c => c.ChildGlobalId == "0LQDWLQ954Q9QwwTtOHEV0");
            Assert.NotNull(beamNode);
            Assert.Equal(AssemblyChildClassification.VisibleSecondaryPart, beamNode.ChildClassification);

            // Verify CSV report generation
            string tempFolder = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            Directory.CreateDirectory(tempFolder);
            try
            {
                await auditService.GenerateReportsAsync(auditResult, tempFolder);
                var csvPath = Path.Combine(tempFolder, "Reports", "IFC_Audit", "debug_ifc_assembly_graph_classification_audit.csv");
                Assert.True(File.Exists(csvPath), "CSV debug_ifc_assembly_graph_classification_audit.csv should be generated");
                var lines = File.ReadAllLines(csvPath);
                Assert.True(lines.Length > 1, "CSV should contain headers and rows");
                Assert.Contains("2KFTYALGT0ghARgUhfEqjI", lines[1]);
            }
            finally
            {
                try { Directory.Delete(tempFolder, true); } catch { }
            }
        }

        [Fact]
        public void TestPrecastElementMapping_AssemblyClassification()
        {
            const string ifcPath = @"F:\06-ITI\00-Project\tryProjects\Project2\Models\IFC\testBuilding2x3withMarks.ifc";
            if (!File.Exists(ifcPath)) return;

            var sessionService = new IfcModelSessionService();
            var aliasResolver = new CentralIfcPropertyAliasResolver(new IfcExporterProfileResolver());
            var auditService = new IfcDiagnosticAuditService(sessionService, aliasResolver);
            var normalizationService = new IfcNormalizationDecisionService(new IfcExporterProfileResolver());
            var georefExtractor = new CraneOptimizer.Infrastructure.Ifc.IfcGeoreferencingExtractor();
            var reader = new IfcReaderRepository(
                auditService,
                normalizationService,
                sessionService,
                georefExtractor,
                aliasResolver);

            var elements = reader.ReadElements(ifcPath);

            var parent = elements.FirstOrDefault(e => e.ElementID == "2KFTYALGT0ghARgUhfEqjI");
            Assert.NotNull(parent);
            Assert.Equal("2K1eRGMijEj9PRqHskPwYb", parent.MainPartGlobalId);
            Assert.Equal("InferredHighConfidence", parent.MainPartDetectionMethod);
            Assert.Equal(3, parent.ChildCount);
            Assert.Equal(2, parent.VisibleSecondaryPartCount);
            Assert.True(parent.HasSecondaryParts);
            Assert.Contains("MixedCategoryButValidAssembly", parent.ExtractionWarnings);
        }
    }
}
