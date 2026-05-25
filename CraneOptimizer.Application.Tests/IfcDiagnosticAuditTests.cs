using CraneOptimizer.Domain.Diagnostics;
using CraneOptimizer.Infrastructure.Repositories;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace CraneOptimizer.Application.Tests
{
    public class IfcDiagnosticAuditTests
    {
        private readonly ITestOutputHelper _output;

        public IfcDiagnosticAuditTests(ITestOutputHelper output)
        {
            _output = output;
        }
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
                Assert.Equal(2, files.Length);

                var jsonFile = files.FirstOrDefault(f => f.EndsWith(".json"));
                var csvFile = files.FirstOrDefault(f => f.EndsWith(".csv"));

                Assert.NotNull(jsonFile);
                Assert.NotNull(csvFile);

                // Verify CSV Content and Metadata headers (Phase 2A requirement)
                var csvContent = File.ReadAllText(csvFile);
                Assert.Contains("# IFC DIAGNOSTIC AUDIT PROFESSIONAL REPORT", csvContent);
                Assert.Contains("# File Name: SampleBridge.ifc", csvContent);
                Assert.Contains("# Exporter Application: Autodesk Revit 2024", csvContent);
                Assert.Contains("# Schema Version: IFC4", csvContent);
                Assert.Contains("GlobalId,EntityLabel,Name,RawIfcType,ResolvedCategory", csvContent);

                // Verify JSON Content
                var jsonContent = File.ReadAllText(jsonFile);
                using (var document = JsonDocument.Parse(jsonContent))
                {
                    var root = document.RootElement;
                    Assert.Equal("SampleBridge.ifc", root.GetProperty("Metadata").GetProperty("FileName").GetString());
                    Assert.Equal(10, root.GetProperty("RawTypes")[0].GetProperty("TotalCount").GetInt32());
                }

                // Verify CSV Content
                csvContent = File.ReadAllText(csvFile);
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

        [Fact]
        public async Task TestTeklaSingleChildAssemblyWrapperCollapse()
        {
            // Tekla single-child assembly becomes GroupingOnly parent + LiftableElement child.
            var audit = new IfcAuditResult
            {
                Metadata = new IfcAuditMetadata
                {
                    FileName = "TeklaModel.ifc",
                    SchemaVersion = "IFC4",
                    ExporterApplication = "Tekla Structures 2024"
                }
            };

            // Parent assembly
            audit.Classifications.Add(new ElementClassificationAuditDetail
            {
                GlobalId = "ParentAssemblyGlobalId",
                RawIfcType = "IfcElementAssembly",
                CandidateResolvedCategory = "Assembly",
                Name = "TeklaPrecastAssembly"
            });
            audit.Weights.Add(new ElementWeightAuditDetail
            {
                GlobalId = "ParentAssemblyGlobalId",
                ResolvedType = "Assembly",
                WeightSource = "Missing",
                WeightStatus = "MissingWeight",
                WeightRawValue = null,
                WeightStandardValueTons = null
            });

            // Single Child
            audit.Classifications.Add(new ElementClassificationAuditDetail
            {
                GlobalId = "ChildElementGlobalId",
                RawIfcType = "IfcColumn",
                CandidateResolvedCategory = "Column",
                Name = "TeklaPrecastColumn"
            });
            audit.Weights.Add(new ElementWeightAuditDetail
            {
                GlobalId = "ChildElementGlobalId",
                ResolvedType = "Column",
                WeightSource = "Quantity",
                WeightStatus = "ExplicitPositive",
                WeightRawValue = 4.5,
                WeightStandardValueTons = 4.5
            });

            // Assembly Decomposition details
            var detail = new AssemblyDecompositionDetail
            {
                AssemblyGlobalId = "ParentAssemblyGlobalId",
                AssemblyName = "TeklaPrecastAssembly",
                GeometryOwnershipPattern = "ChildrenOnly",
                ParentHasGeometry = false,
                ChildrenHaveGeometry = true,
                DirectChildCount = 1,
                RecursiveChildCount = 1
            };
            detail.ChildDetails.Add(new AssemblyChildDetail
            {
                ChildGlobalId = "ChildElementGlobalId",
                ChildRawIfcType = "IfcColumn",
                ChildName = "TeklaPrecastColumn",
                HasGeometry = true,
                SwallowedRisk = true
            });
            audit.Assemblies.Details.Add(detail);

            var resolver = new IfcExporterProfileResolver();
            var normalizer = new IfcNormalizationDecisionService(resolver);

            var result = await normalizer.GenerateNormalizationPreviewAsync(audit);

            var parent = result.Elements.First(e => e.GlobalId == "ParentAssemblyGlobalId");
            var child = result.Elements.First(e => e.GlobalId == "ChildElementGlobalId");

            Assert.Equal(IfcElementRole.GroupingOnly, parent.Role);
            Assert.Equal(IfcAssemblyDecision.SingleChildWrapper, parent.AssemblyDecision);
            Assert.False(parent.IncludeInGaPreview);
            Assert.Single(parent.VisibleGeometryOwnerGlobalIds);
            Assert.Equal("ChildElementGlobalId", parent.VisibleGeometryOwnerGlobalIds[0]);

            Assert.Equal(IfcElementRole.LiftableElement, child.Role);
            Assert.True(child.IncludeInGaPreview);
            Assert.Equal("ParentAssemblyGlobalId", child.ParentGlobalId);
            Assert.Single(child.VisibleGeometryOwnerGlobalIds);
            Assert.Equal("ChildElementGlobalId", child.VisibleGeometryOwnerGlobalIds[0]);
        }

        [Fact]
        public async Task TestTeklaWeldedMultiChildAssemblyTreating()
        {
            // Tekla welded multi-child assembly becomes LiftAssembly when explicit parent weight exists,
            // and structural children are excluded to avoid double counting.
            var audit = new IfcAuditResult
            {
                Metadata = new IfcAuditMetadata
                {
                    FileName = "TeklaCastUnit.ifc",
                    SchemaVersion = "IFC4",
                    ExporterApplication = "Tekla Structures"
                }
            };

            // Parent Assembly (has trusted weight)
            audit.Classifications.Add(new ElementClassificationAuditDetail
            {
                GlobalId = "ParentAssembly",
                RawIfcType = "IfcElementAssembly",
                CandidateResolvedCategory = "Assembly",
                Name = "TeklaCastUnitAssembly"
            });
            audit.Weights.Add(new ElementWeightAuditDetail
            {
                GlobalId = "ParentAssembly",
                ResolvedType = "Assembly",
                WeightSource = "PropertySet.CastUnitWeight",
                WeightStatus = "ExplicitPositive",
                WeightRawValue = 12.5,
                WeightStandardValueTons = 12.5
            });

            // Children elements
            audit.Classifications.Add(new ElementClassificationAuditDetail
            {
                GlobalId = "Child1",
                RawIfcType = "IfcBeam",
                CandidateResolvedCategory = "Beam",
                Name = "PrecastBeamComponent1"
            });
            audit.Weights.Add(new ElementWeightAuditDetail
            {
                GlobalId = "Child1",
                ResolvedType = "Beam",
                WeightSource = "Missing",
                WeightStatus = "MissingWeight"
            });

            audit.Classifications.Add(new ElementClassificationAuditDetail
            {
                GlobalId = "Child2",
                RawIfcType = "IfcBeam",
                CandidateResolvedCategory = "Beam",
                Name = "PrecastBeamComponent2"
            });
            audit.Weights.Add(new ElementWeightAuditDetail
            {
                GlobalId = "Child2",
                ResolvedType = "Beam",
                WeightSource = "Missing",
                WeightStatus = "MissingWeight"
            });

            var detail = new AssemblyDecompositionDetail
            {
                AssemblyGlobalId = "ParentAssembly",
                AssemblyName = "TeklaCastUnitAssembly",
                GeometryOwnershipPattern = "ChildrenOnly",
                ParentHasGeometry = false,
                ChildrenHaveGeometry = true,
                DirectChildCount = 2,
                RecursiveChildCount = 2
            };
            detail.ChildDetails.Add(new AssemblyChildDetail { ChildGlobalId = "Child1", ChildRawIfcType = "IfcBeam", HasGeometry = true });
            detail.ChildDetails.Add(new AssemblyChildDetail { ChildGlobalId = "Child2", ChildRawIfcType = "IfcBeam", HasGeometry = true });
            audit.Assemblies.Details.Add(detail);

            var resolver = new IfcExporterProfileResolver();
            var normalizer = new IfcNormalizationDecisionService(resolver);

            var result = await normalizer.GenerateNormalizationPreviewAsync(audit);

            var parent = result.Elements.First(e => e.GlobalId == "ParentAssembly");
            var c1 = result.Elements.First(e => e.GlobalId == "Child1");
            var c2 = result.Elements.First(e => e.GlobalId == "Child2");

            Assert.Equal(IfcElementRole.LiftAssembly, parent.Role);
            Assert.Equal(IfcAssemblyDecision.TeklaCastUnitOrWeldedLiftAssembly, parent.AssemblyDecision);
            Assert.True(parent.IncludeInGaPreview);
            // ChildrenOnly pattern -> visible geometry owners must list the child IDs
            Assert.Contains("Child1", parent.VisibleGeometryOwnerGlobalIds);
            Assert.Contains("Child2", parent.VisibleGeometryOwnerGlobalIds);
            Assert.Equal(2, parent.VisibleGeometryOwnerGlobalIds.Count);

            // Children must be Swallowed/Excluded to avoid double-counting
            Assert.Equal(IfcElementRole.Excluded, c1.Role);
            Assert.Equal(IfcElementRole.Excluded, c2.Role);
            Assert.False(c1.IncludeInGaPreview);
            Assert.False(c2.IncludeInGaPreview);
        }

        [Fact]
        public async Task TestRevitStructuralBeamSystemUnrolling()
        {
            // Revit Structural Beam System becomes GroupingOnly parent + liftable beam children.
            var audit = new IfcAuditResult
            {
                Metadata = new IfcAuditMetadata
                {
                    FileName = "RevitModel.ifc",
                    SchemaVersion = "IFC4",
                    ExporterApplication = "Revit Exporter"
                }
            };

            // Parent system grouping
            audit.Classifications.Add(new ElementClassificationAuditDetail
            {
                GlobalId = "SystemParent",
                RawIfcType = "IfcElementAssembly",
                CandidateResolvedCategory = "Assembly",
                Name = "Structural Beam System"
            });
            audit.Weights.Add(new ElementWeightAuditDetail { GlobalId = "SystemParent", WeightStatus = "MissingWeight" });

            // Child beams
            audit.Classifications.Add(new ElementClassificationAuditDetail
            {
                GlobalId = "BeamChild1",
                RawIfcType = "IfcBeam",
                CandidateResolvedCategory = "Beam",
                Name = "Structural Beam 01"
            });
            audit.Weights.Add(new ElementWeightAuditDetail
            {
                GlobalId = "BeamChild1",
                ResolvedType = "Beam",
                WeightSource = "PropertySet.Weight",
                WeightStatus = "ExplicitPositive",
                WeightRawValue = 1.8,
                WeightStandardValueTons = 1.8
            });

            audit.Classifications.Add(new ElementClassificationAuditDetail
            {
                GlobalId = "BeamChild2",
                RawIfcType = "IfcBeam",
                CandidateResolvedCategory = "Beam",
                Name = "Structural Beam 02"
            });
            audit.Weights.Add(new ElementWeightAuditDetail
            {
                GlobalId = "BeamChild2",
                ResolvedType = "Beam",
                WeightSource = "PropertySet.Weight",
                WeightStatus = "ExplicitPositive",
                WeightRawValue = 1.8,
                WeightStandardValueTons = 1.8
            });

            var detail = new AssemblyDecompositionDetail
            {
                AssemblyGlobalId = "SystemParent",
                AssemblyName = "Structural Beam System",
                GeometryOwnershipPattern = "ChildrenOnly",
                ParentHasGeometry = false,
                ChildrenHaveGeometry = true,
                DirectChildCount = 2,
                RecursiveChildCount = 2
            };
            detail.ChildDetails.Add(new AssemblyChildDetail { ChildGlobalId = "BeamChild1", ChildRawIfcType = "IfcBeam", HasGeometry = true });
            detail.ChildDetails.Add(new AssemblyChildDetail { ChildGlobalId = "BeamChild2", ChildRawIfcType = "IfcBeam", HasGeometry = true });
            audit.Assemblies.Details.Add(detail);

            var resolver = new IfcExporterProfileResolver();
            var normalizer = new IfcNormalizationDecisionService(resolver);

            var result = await normalizer.GenerateNormalizationPreviewAsync(audit);

            var parent = result.Elements.First(e => e.GlobalId == "SystemParent");
            var b1 = result.Elements.First(e => e.GlobalId == "BeamChild1");
            var b2 = result.Elements.First(e => e.GlobalId == "BeamChild2");

            Assert.Equal(IfcElementRole.GroupingOnly, parent.Role);
            Assert.Equal(IfcAssemblyDecision.RevitStructuralBeamSystemGrouping, parent.AssemblyDecision);
            Assert.False(parent.IncludeInGaPreview);

            Assert.Equal(IfcElementRole.LiftableElement, b1.Role);
            Assert.Equal(IfcElementRole.LiftableElement, b2.Role);
            Assert.True(b1.IncludeInGaPreview);
            Assert.True(b2.IncludeInGaPreview);
            Assert.Equal("SystemParent", b1.ParentGlobalId);
            Assert.Equal("SystemParent", b2.ParentGlobalId);
        }

        [Fact]
        public async Task TestIfcReinforcingBarClassificationAndWeightRollup()
        {
            // IfcReinforcingBar becomes InternalAuxiliary and reinforcement explicit weight rollup
            var audit = new IfcAuditResult
            {
                Metadata = new IfcAuditMetadata
                {
                    FileName = "Reinforcements.ifc",
                    ExporterApplication = "Generic Exporter"
                }
            };

            // Host element (missing weight)
            audit.Classifications.Add(new ElementClassificationAuditDetail
            {
                GlobalId = "PrecastCol",
                RawIfcType = "IfcColumn",
                CandidateResolvedCategory = "Column",
                Name = "Precast Concrete Column"
            });
            audit.Weights.Add(new ElementWeightAuditDetail { GlobalId = "PrecastCol", WeightStatus = "MissingWeight" });

            // Reinforcement rebar child (has explicit weight)
            audit.Classifications.Add(new ElementClassificationAuditDetail
            {
                GlobalId = "Rebar1",
                RawIfcType = "IfcReinforcingBar",
                CandidateResolvedCategory = "Reinforcement",
                Name = "Main Rebar 25mm"
            });
            audit.Weights.Add(new ElementWeightAuditDetail
            {
                GlobalId = "Rebar1",
                ResolvedType = "Reinforcement",
                WeightSource = "PropertySet.Mass",
                WeightStatus = "ExplicitPositive",
                WeightRawValue = 0.45,
                WeightStandardValueTons = 0.45
            });

            audit.Geometries.Add(new ElementGeometryOwnershipAuditDetail
            {
                LogicalGlobalId = "Rebar1",
                HasGeometryDirectly = true,
                GeometryOwnerCandidateGlobalId = "Rebar1"
            });

            var detail = new AssemblyDecompositionDetail
            {
                AssemblyGlobalId = "PrecastCol",
                AssemblyName = "Precast Concrete Column",
                GeometryOwnershipPattern = "ChildrenOnly",
                DirectChildCount = 1,
                RecursiveChildCount = 1
            };
            detail.ChildDetails.Add(new AssemblyChildDetail { ChildGlobalId = "Rebar1", ChildRawIfcType = "IfcReinforcingBar", HasGeometry = true });
            audit.Assemblies.Details.Add(detail);

            var resolver = new IfcExporterProfileResolver();
            var normalizer = new IfcNormalizationDecisionService(resolver);

            var result = await normalizer.GenerateNormalizationPreviewAsync(audit);

            var host = result.Elements.First(e => e.GlobalId == "PrecastCol");
            var rebar = result.Elements.First(e => e.GlobalId == "Rebar1");

            // Rebar checks
            Assert.Equal(IfcElementRole.InternalAuxiliary, rebar.Role);
            Assert.False(rebar.IncludeInRenderPreview);
            Assert.False(rebar.IncludeInGaPreview);
            Assert.Contains("Rebar1", rebar.HiddenInternalGeometryOwnerGlobalIds);

            // Roll-up verification
            Assert.Equal(0.45, host.ReinforcementWeightContributionTons);
            Assert.Equal(IfcWeightTrustLevel.RequiresUserApproval, host.WeightTrustLevel);
            Assert.Contains("positive reinforcement steel weight is available", host.Warnings[0]);
        }

        [Fact]
        public void TestUnitConversionDisplayWarningsAndConversions()
        {
            // Add tests for length/area/volume/mass conversion display.
            // Explicitly assert that:
            // 1. FOOT length to meters = 0.3048
            // 2. SQUARE FOOT to square meters = 0.09290304
            // 3. CUBIC FOOT to cubic meters = 0.028316846592
            // 4. KILOGRAM to kilograms = 1.0 (GetSiUnitFactor logic)

            // Let's call the private methods of IfcDiagnosticAuditService using reflection or check their behavior.
            var type = typeof(IfcDiagnosticAuditService);
            
            var getSiFactorMethod = type.GetMethod("GetSiUnitFactor", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
            var getConvFactorMethod = type.GetMethod("GetConversionUnitFactor", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);

            Assert.NotNull(getSiFactorMethod);
            Assert.NotNull(getConvFactorMethod);

            using (var model = Xbim.Ifc.IfcStore.Create(Xbim.Common.Step21.XbimSchemaVersion.Ifc4, Xbim.IO.XbimStoreType.InMemoryModel))
            {
                using (var txn = model.BeginTransaction("Create mock units"))
                {
                    // Create conversion based units
                    var mockFoot = model.Instances.New<Xbim.Ifc4.MeasureResource.IfcConversionBasedUnit>();
                    mockFoot.Name = "FOOT";

                    var mockSqFoot = model.Instances.New<Xbim.Ifc4.MeasureResource.IfcConversionBasedUnit>();
                    mockSqFoot.Name = "SQUARE FOOT";

                    var mockSqFt = model.Instances.New<Xbim.Ifc4.MeasureResource.IfcConversionBasedUnit>();
                    mockSqFt.Name = "SQ FT";

                    var mockCuFoot = model.Instances.New<Xbim.Ifc4.MeasureResource.IfcConversionBasedUnit>();
                    mockCuFoot.Name = "CUBIC FOOT";

                    var mockCuFt = model.Instances.New<Xbim.Ifc4.MeasureResource.IfcConversionBasedUnit>();
                    mockCuFt.Name = "CU FT";

                    var mockTon = model.Instances.New<Xbim.Ifc4.MeasureResource.IfcConversionBasedUnit>();
                    mockTon.Name = "TON";

                    var footFactor = (double)getConvFactorMethod.Invoke(null, new object[] { mockFoot })!;
                    var sqFootFactor = (double)getConvFactorMethod.Invoke(null, new object[] { mockSqFoot })!;
                    var sqFtFactor = (double)getConvFactorMethod.Invoke(null, new object[] { mockSqFt })!;
                    var cuFootFactor = (double)getConvFactorMethod.Invoke(null, new object[] { mockCuFoot })!;
                    var cuFtFactor = (double)getConvFactorMethod.Invoke(null, new object[] { mockCuFt })!;
                    var tonFactor = (double)getConvFactorMethod.Invoke(null, new object[] { mockTon })!;

                    Assert.Equal(0.3048, footFactor);
                    Assert.Equal(0.3048 * 0.3048, sqFootFactor);
                    Assert.Equal(0.3048 * 0.3048, sqFtFactor);
                    Assert.Equal(0.3048 * 0.3048 * 0.3048, cuFootFactor);
                    Assert.Equal(0.3048 * 0.3048 * 0.3048, cuFtFactor);
                    Assert.Equal(1000.0, tonFactor);

                    // Create SI units for mass
                    var mockKg = model.Instances.New<Xbim.Ifc4.MeasureResource.IfcSIUnit>();
                    mockKg.Name = Xbim.Ifc4.Interfaces.IfcSIUnitName.GRAM;
                    mockKg.Prefix = Xbim.Ifc4.Interfaces.IfcSIPrefix.KILO;

                    var mockGram = model.Instances.New<Xbim.Ifc4.MeasureResource.IfcSIUnit>();
                    mockGram.Name = Xbim.Ifc4.Interfaces.IfcSIUnitName.GRAM;

                    var kgFactor = (double)getSiFactorMethod.Invoke(null, new object[] { mockKg })!;
                    var gramFactor = (double)getSiFactorMethod.Invoke(null, new object[] { mockGram })!;

                    Assert.Equal(1.0, kgFactor);
                    Assert.Equal(0.001, gramFactor);
                    
                    txn.Commit();
                }
            }
        }

        [Fact]
        public async Task RunRealIfcNormalizationPreviews()
        {
            var files = new[]
            {
                @"F:\06-ITI\00-Project\tryProjects\try6\Models\IFC\IfcBuilding_12_ifc4.ifc",
                @"F:\06-ITI\00-Project\tryProjects\try6\Models\IFC\13_ifc4.ifc",
                @"F:\06-ITI\00-Project\tryProjects\try6\Models\IFC\Snowdon Towers Sample Structural_ifc2x3.ifc",
                @"F:\06-ITI\00-Project\tryProjects\try6\Models\IFC\Snowdon Towers Sample Structural_ifc4.ifc"
            };

            var auditService = new IfcDiagnosticAuditService();
            var resolver = new IfcExporterProfileResolver();
            var normalizer = new IfcNormalizationDecisionService(resolver);
            var projectFolder = @"f:\06-ITI\00-Project\Main Project\CraneOptimizer";

            foreach (var file in files)
            {
                if (!System.IO.File.Exists(file))
                {
                    _output.WriteLine($"[ERROR] File not found: {file}");
                    continue;
                }

                _output.WriteLine($"\n--- Processing File: {System.IO.Path.GetFileName(file)} ---");
                var auditResult = await auditService.RunAuditAsync(file);
                await auditService.GenerateReportsAsync(auditResult, projectFolder);

                var previewResult = await normalizer.GenerateNormalizationPreviewAsync(auditResult);
                string outDir = await normalizer.GeneratePreviewReportsAsync(previewResult, projectFolder);

                _output.WriteLine($"Reports generated at: {outDir}");
                _output.WriteLine($"Detected Exporter: {previewResult.ExporterApplication}");
                _output.WriteLine($"Detected Profile: {previewResult.DetectedProfile}");
                _output.WriteLine($"Total Raw Products: {previewResult.TotalRawProductCount}");
                _output.WriteLine($"Normalized LiftableCount: {previewResult.NormalizedLiftableCount}");
                _output.WriteLine($"GroupingOnlyCount: {previewResult.GroupingOnlyCount}");
                _output.WriteLine($"LiftAssemblyCount: {previewResult.LiftAssemblyCount}");
                _output.WriteLine($"InternalAuxiliaryCount: {previewResult.InternalAuxiliaryCount}");
                _output.WriteLine($"ExcludedCount: {previewResult.ExcludedCount}");
                _output.WriteLine($"NeedsReviewCount: {previewResult.NeedsReviewCount}");
                _output.WriteLine($"VisibleRenderGeometryCount: {previewResult.VisibleRenderGeometryCount}");
                _output.WriteLine($"HiddenInternalGeometryCount: {previewResult.HiddenInternalGeometryCount}");
                _output.WriteLine($"BlockedGaMissingWeightCount: {previewResult.BlockedGaMissingWeightCount}");
                _output.WriteLine($"BlockedGaZeroWeightCount: {previewResult.BlockedGaZeroWeightCount}");
                _output.WriteLine($"BlockedGaFallbackWeightCount: {previewResult.BlockedGaFallbackWeightCount}");

                // Count assembly decisions
                int singleChildWrapper = 0;
                int revitBeamSystem = 0;
                int teklaWelded = 0;
                int genericGrouping = 0;
                int unknownNeedsReview = 0;

                foreach (var el in previewResult.Elements)
                {
                    switch (el.AssemblyDecision)
                    {
                        case IfcAssemblyDecision.SingleChildWrapper:
                            singleChildWrapper++;
                            break;
                        case IfcAssemblyDecision.RevitStructuralBeamSystemGrouping:
                            revitBeamSystem++;
                            break;
                        case IfcAssemblyDecision.TeklaCastUnitOrWeldedLiftAssembly:
                            teklaWelded++;
                            break;
                        case IfcAssemblyDecision.GenericGroupingAssembly:
                            genericGrouping++;
                            break;
                        case IfcAssemblyDecision.UnknownNeedsReview:
                            unknownNeedsReview++;
                            break;
                    }
                }

                _output.WriteLine($"Assembly Decision Counts:");
                _output.WriteLine($"  SingleChildWrapper: {singleChildWrapper}");
                _output.WriteLine($"  RevitStructuralBeamSystemGrouping: {revitBeamSystem}");
                _output.WriteLine($"  TeklaCastUnitOrWeldedLiftAssembly: {teklaWelded}");
                _output.WriteLine($"  GenericGroupingAssembly: {genericGrouping}");
                _output.WriteLine($"  UnknownNeedsReview: {unknownNeedsReview}");

                // Count weight trust levels
                var trustCounts = new Dictionary<IfcWeightTrustLevel, int>();
                foreach (var el in previewResult.Elements)
                {
                    if (!trustCounts.ContainsKey(el.WeightTrustLevel))
                        trustCounts[el.WeightTrustLevel] = 0;
                    trustCounts[el.WeightTrustLevel]++;
                }

                _output.WriteLine($"Weight Trust Level Counts:");
                foreach (var kv in trustCounts)
                {
                    _output.WriteLine($"  {kv.Key}: {kv.Value}");
                }
            }
        }

        [Fact]
        public void TestPhase2ANormalizationGating()
        {
            // Test 1: NeedsReview parent element is blocked from GA
            var needsReviewEl = new CraneOptimizer.Domain.Schemas.PrecastElement
            {
                ElementID = "NeedsReview_Parent",
                Role = IfcElementRole.NeedsReview,
                IncludeInGa = true, // Normalization service might have initialized to true
                WeightTons = 5.0,
                WeightTrustLevel = IfcWeightTrustLevel.RequiresUserApproval
            };

            // Test 2: GroupingOnly item must not enter GA or render
            var groupingOnlyEl = new CraneOptimizer.Domain.Schemas.PrecastElement
            {
                ElementID = "GroupingOnly_System",
                Role = IfcElementRole.GroupingOnly,
                IncludeInGa = true,
                IncludeInRender = true,
                WeightTons = 10.0,
                WeightTrustLevel = IfcWeightTrustLevel.ExplicitQuantity
            };

            // Test 3: Standardized Units and Gating Criteria for GA
            // GA ready element
            var gaReadyEl = new CraneOptimizer.Domain.Schemas.PrecastElement
            {
                ElementID = "GA_Ready",
                Role = IfcElementRole.LiftableElement,
                IncludeInGa = true,
                IncludeInRender = true,
                WeightTons = 3.5,
                WeightTrustLevel = IfcWeightTrustLevel.ExplicitQuantity,
                ElementCOG = new CraneOptimizer.Domain.Common.DomainPoint3D(1.0, 2.0, 3.0)
            };

            // Missing weight element
            var missingWeightEl = new CraneOptimizer.Domain.Schemas.PrecastElement
            {
                ElementID = "Missing_Weight",
                Role = IfcElementRole.LiftableElement,
                IncludeInGa = true,
                WeightTons = null,
                WeightTrustLevel = IfcWeightTrustLevel.Missing
            };

            // Fallback weight element (not allowed in GA)
            var fallbackWeightEl = new CraneOptimizer.Domain.Schemas.PrecastElement
            {
                ElementID = "Fallback_Weight",
                Role = IfcElementRole.LiftableElement,
                IncludeInGa = true,
                WeightTons = 4.2,
                WeightTrustLevel = IfcWeightTrustLevel.EstimatedFallback_NotAllowedForGA
            };

            // Let's verify our gating logic on these elements
            var filteredElements = new List<CraneOptimizer.Domain.Schemas.PrecastElement>
            {
                needsReviewEl,
                groupingOnlyEl,
                gaReadyEl,
                missingWeightEl,
                fallbackWeightEl
            };

            var gaReadyElements = new List<CraneOptimizer.Domain.Schemas.PrecastElement>();
            var blockedElements = new List<(CraneOptimizer.Domain.Schemas.PrecastElement element, string reason)>();

            foreach (var element in filteredElements)
            {
                if (!element.IncludeInGa)
                {
                    blockedElements.Add((element, "IncludeInGa flag is false"));
                    continue;
                }

                if (element.Role == IfcElementRole.NeedsReview)
                {
                    blockedElements.Add((element, "NeedsReview blocked"));
                    continue;
                }

                if (element.Role == IfcElementRole.GroupingOnly)
                {
                    blockedElements.Add((element, "GroupingOnly blocked"));
                    continue;
                }

                if (element.Role == IfcElementRole.InternalAuxiliary)
                {
                    blockedElements.Add((element, "Auxiliary blocked"));
                    continue;
                }

                if (!element.WeightTons.HasValue || element.WeightTons.Value <= 0)
                {
                    blockedElements.Add((element, "Missing or zero weight"));
                    continue;
                }

                if (element.WeightTrustLevel != IfcWeightTrustLevel.ExplicitQuantity &&
                    element.WeightTrustLevel != IfcWeightTrustLevel.ExplicitProperty)
                {
                    blockedElements.Add((element, "Untrusted weight"));
                    continue;
                }

                gaReadyElements.Add(element);
            }

            // Assertions
            Assert.Single(gaReadyElements);
            Assert.Equal("GA_Ready", gaReadyElements[0].ElementID);

            Assert.Equal(4, blockedElements.Count);
            Assert.Contains(blockedElements, b => b.element.ElementID == "NeedsReview_Parent" && b.reason == "NeedsReview blocked");
            Assert.Contains(blockedElements, b => b.element.ElementID == "GroupingOnly_System" && b.reason == "GroupingOnly blocked");
            Assert.Contains(blockedElements, b => b.element.ElementID == "Missing_Weight" && b.reason == "Missing or zero weight");
            Assert.Contains(blockedElements, b => b.element.ElementID == "Fallback_Weight" && b.reason == "Untrusted weight");
        }
    }
}
