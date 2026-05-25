using CraneOptimizer.Domain.Diagnostics;
using CraneOptimizer.Infrastructure.Repositories;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Xunit;

namespace CraneOptimizer.Application.Tests
{
    public class IfcDiagnosticAuditTests
    {
        [Fact]
        public void TestIfcAuditResultDtoInitialization()
        {
            // Verify assembly parent-child DTO structure holds elements as designed
            var audit = new IfcAuditResult
            {
                Metadata = new IfcAuditMetadata
                {
                    FileName = "TestModel.ifc",
                    SchemaVersion = "IFC4",
                    ExporterApplication = "Tekla Structures 2023"
                },
                Assemblies = new AssemblyDecompositionAudit
                {
                    TotalAssemblyCount = 1,
                    AssembliesWithChildren = 1,
                    TotalDirectChildCount = 2,
                    TotalRecursiveChildCount = 2,
                    MaxDecompositionDepth = 1,
                    DoubleCountingRisksCount = 0,
                    ChildrenSwallowedCount = 2,
                    ChildRawIfcTypes = new List<string> { "IfcColumn", "IfcPlate" }
                }
            };

            var detail = new AssemblyDecompositionDetail
            {
                AssemblyGlobalId = "AssemblyParentGlobalId_01",
                AssemblyName = "PrecastColAssembly_01",
                DirectChildCount = 2,
                RecursiveChildCount = 2,
                NestingDepth = 1,
                ParentHasGeometry = false,
                ChildrenHaveGeometry = true,
                GeometryOwnershipPattern = "ChildrenOnly"
            };

            detail.ChildDetails.Add(new AssemblyChildDetail
            {
                ChildGlobalId = "ChildColumnGlobalId_01",
                ChildRawIfcType = "IfcColumn",
                ChildName = "C1",
                HasGeometry = true,
                DoubleCountingRisk = false,
                SwallowedRisk = true
            });

            detail.ChildDetails.Add(new AssemblyChildDetail
            {
                ChildGlobalId = "ChildPlateGlobalId_01",
                ChildRawIfcType = "IfcPlate",
                ChildName = "P1",
                HasGeometry = true,
                DoubleCountingRisk = false,
                SwallowedRisk = true
            });

            audit.Assemblies.Details.Add(detail);

            Assert.Equal("TestModel.ifc", audit.Metadata.FileName);
            Assert.Equal("IFC4", audit.Metadata.SchemaVersion);
            Assert.Equal("Tekla Structures 2023", audit.Metadata.ExporterApplication);
            Assert.Equal(1, audit.Assemblies.TotalAssemblyCount);
            Assert.Single(audit.Assemblies.Details);
            Assert.Equal(2, audit.Assemblies.Details[0].ChildDetails.Count);
            Assert.True(audit.Assemblies.Details[0].ChildDetails.All(c => c.SwallowedRisk));
        }

        [Fact]
        public void TestWeightAuditClassificationZeroWeight()
        {
            var detail = new ElementWeightAuditDetail
            {
                GlobalId = "ZeroWeightGlobalId_01",
                ResolvedType = "Column",
                WeightRawValue = 0.0,
                WeightStandardValueTons = 0.0,
                WeightSource = "PropertySet.Instance",
                WeightStatus = "ExplicitZeroFromIFC",
                Warning = "The IFC explicitly provides zero weight. This may be valid or an exporter/data issue. The application must not silently replace it without user approval."
            };

            Assert.Equal("ExplicitZeroFromIFC", detail.WeightStatus);
            Assert.Equal(0.0, detail.WeightStandardValueTons);
            Assert.Contains("explicitly provides zero weight", detail.Warning);
        }

        [Fact]
        public void TestWeightAuditClassificationMissingWeight()
        {
            var detail = new ElementWeightAuditDetail
            {
                GlobalId = "MissingWeightGlobalId_01",
                ResolvedType = "Beam",
                WeightRawValue = null,
                WeightStandardValueTons = null,
                WeightSource = "Missing",
                WeightStatus = "MissingWeight",
                Warning = "No explicit weight found in IFC. Weight calculation is outside current project scope unless user provides material/density rules."
            };

            Assert.Null(detail.WeightRawValue);
            Assert.Null(detail.WeightStandardValueTons);
            Assert.Equal("MissingWeight", detail.WeightStatus);
            Assert.Contains("No explicit weight found in IFC", detail.Warning);
        }

        [Fact]
        public void TestWeightAuditClassificationPositiveWeight()
        {
            var detail = new ElementWeightAuditDetail
            {
                GlobalId = "PositiveWeightGlobalId_01",
                ResolvedType = "Slab",
                WeightRawValue = 4500.0,
                WeightStandardValueTons = 4.5,
                WeightSource = "IfcQuantityWeight.Instance",
                WeightStatus = "ExplicitPositive"
            };

            Assert.Equal(4500.0, detail.WeightRawValue);
            Assert.Equal(4.5, detail.WeightStandardValueTons);
            Assert.Equal("ExplicitPositive", detail.WeightStatus);
        }

        [Fact]
        public void TestWeightAuditEstimatedFallback()
        {
            var detail = new ElementWeightAuditDetail
            {
                GlobalId = "EstimatedWeightGlobalId_01",
                ResolvedType = "Slab",
                WeightRawValue = 3750.0, // e.g. volume 1.5 * 2500
                WeightStandardValueTons = 3.75,
                WeightSource = "ExistingDensityFallback",
                WeightStatus = "EstimatedByFallback",
                Warning = "Current pipeline estimated weight using density fallback. Verify with user because this is an engineering assumption."
            };

            Assert.Equal(3750.0, detail.WeightRawValue);
            Assert.Equal(3.75, detail.WeightStandardValueTons);
            Assert.Equal("EstimatedByFallback", detail.WeightStatus);
            Assert.Contains("density fallback", detail.Warning);
        }

        [Fact]
        public async Task TestReportWritersGenerateValidFiles()
        {
            // Set up small mock DTO data
            var audit = new IfcAuditResult
            {
                Metadata = new IfcAuditMetadata
                {
                    FileName = "SampleBridge.ifc",
                    SchemaVersion = "IFC4",
                    ExporterApplication = "Autodesk Revit 2024"
                }
            };

            audit.RawTypes.Add(new RawIfcTypeSummary
            {
                RawIfcClass = "IfcColumn",
                TotalCount = 10,
                CountWithGlobalId = 10,
                CountWithName = 10,
                CountWithShapeInstances = 10
            });

            audit.Classifications.Add(new ElementClassificationAuditDetail
            {
                GlobalId = "ColGlobalId_1",
                EntityLabel = 101,
                Name = "C-1",
                RawIfcType = "IfcColumn",
                ExpressTypeName = "IfcColumn",
                CandidateResolvedCategory = "Column",
                CandidateReason = "Direct mapping"
            });

            audit.Weights.Add(new ElementWeightAuditDetail
            {
                GlobalId = "ColGlobalId_1",
                ResolvedType = "Column",
                WeightRawValue = 3500.0,
                WeightStandardValueTons = 3.5,
                WeightSource = "IfcQuantityWeight.Instance",
                WeightStatus = "ExplicitPositive"
            });

            audit.VolumesMaterials.Add(new ElementVolumeMaterialAuditDetail
            {
                GlobalId = "ColGlobalId_1",
                VolumeSource = "Qto_ColumnBaseQuantities",
                VolumeRawValue = 1.4,
                VolumeStandardValueM3 = 1.4,
                MaterialSource = "Simple",
                MaterialName = "Concrete"
            });

            audit.Geometries.Add(new ElementGeometryOwnershipAuditDetail
            {
                LogicalGlobalId = "ColGlobalId_1",
                ShapeCount = 1,
                HasGeometryDirectly = true,
                GeometryOwnerCandidateGlobalId = "ColGlobalId_1",
                RenderRisk = "OK"
            });

            var tempPath = Path.Combine(Path.GetTempPath(), "IfcAuditTest_" + Guid.NewGuid());
            Directory.CreateDirectory(tempPath);

            try
            {
                var auditService = new IfcDiagnosticAuditService();
                string outputDir = await auditService.GenerateReportsAsync(audit, tempPath);

                Assert.True(Directory.Exists(outputDir));

                var files = Directory.GetFiles(outputDir);
                Assert.Equal(3, files.Length);

                var mdFile = files.FirstOrDefault(f => f.EndsWith(".md"));
                var jsonFile = files.FirstOrDefault(f => f.EndsWith(".json"));
                var csvFile = files.FirstOrDefault(f => f.EndsWith(".csv"));

                Assert.NotNull(mdFile);
                Assert.NotNull(jsonFile);
                Assert.NotNull(csvFile);

                // Verify MD Content
                var mdContent = File.ReadAllText(mdFile);
                Assert.Contains("SampleBridge.ifc", mdContent);
                Assert.Contains("Autodesk Revit 2024", mdContent);
                Assert.Contains("IfcColumn", mdContent);

                // Verify JSON Content
                var jsonContent = File.ReadAllText(jsonFile);
                using (var document = JsonDocument.Parse(jsonContent))
                {
                    var root = document.RootElement;
                    Assert.Equal("SampleBridge.ifc", root.GetProperty("Metadata").GetProperty("FileName").GetString());
                    Assert.Equal(10, root.GetProperty("RawTypes")[0].GetProperty("TotalCount").GetInt32());
                }

                // Verify CSV Content
                var csvContent = File.ReadAllText(csvFile);
                Assert.Contains("ColGlobalId_1", csvContent);
                Assert.Contains("IfcColumn", csvContent);
                Assert.Contains("Concrete", csvContent);
            }
            finally
            {
                if (Directory.Exists(tempPath))
                {
                    Directory.Delete(tempPath, true);
                }
            }
        }
    }
}
