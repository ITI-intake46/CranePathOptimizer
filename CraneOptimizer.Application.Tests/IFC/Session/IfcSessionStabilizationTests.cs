using CraneOptimizer.Domain.Common;
using CraneOptimizer.Domain.Diagnostics;
using CraneOptimizer.Domain.Interfaces;
using CraneOptimizer.Domain.Schemas;
using CraneOptimizer.Domain.Schemas.Enums;
using CraneOptimizer.Infrastructure.Ifc;
using CraneOptimizer.Infrastructure.Repositories;
using CraneOptimizer.UI.Features.F2_IfcViewer;
using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Engines.OptimizationScope;
using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Settings;
using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Models;
using CraneOptimizer.Domain.Entities;
using Xunit;
using Xunit.Abstractions;
using Xbim.Ifc;
using Xbim.Common.Geometry;
using Xbim.ModelGeometry.Scene;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media.Media3D;
using CraneOptimizer.UI.Core.Interfaces;
using CraneOptimizer.UI.Core.Models;
using CraneOptimizer.UI.Features.F8_GAOptimizer.Services;
using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Interfaces;
using CraneOptimizer.Application.Optimization.GeneticAlgorithms.DTOs;
using CraneOptimizer.UI.Features.F10_ProjectManagement.Services;
using CraneOptimizer.UI.Features.F10_ProjectManagement.Models;
using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Services;
using CraneOptimizer.UI.Features.F0_CoordinateSystem;

namespace CraneOptimizer.Application.Tests
{
    public class IfcSessionStabilizationTests : IDisposable
    {
        private readonly ITestOutputHelper _output;

        public IfcSessionStabilizationTests(ITestOutputHelper output)
        {
            _output = output;
            ElementVisualBuilder.ClearForTesting();
        }

        public void Dispose()
        {
            ElementVisualBuilder.ClearForTesting();
        }

        // =========================================================================
        // Fakes / Stubs for pure unit testing
        // =========================================================================

        public class FakeIfcModelSession : IIfcModelSession
        {
            public Guid SessionId { get; set; } = Guid.NewGuid();
            public string IfcPath { get; set; } = "C:\\Fake\\Model.ifc";
            public string FileIdentity { get; set; } = "FakeModelIdentity";
            public IfcStore Model { get; }
            public Xbim3DModelContext GeometryContext { get; }
            public double ModelLengthFactor { get; set; } = 1.0;
            public double? ModelGroundElevation { get; set; } = 0.0;
            public int CreateContextCount { get; set; } = 1;
            public int ShapeInstanceCount { get; set; } = 0;
            public TimeSpan OpenDuration { get; set; } = TimeSpan.FromMilliseconds(50);
            public TimeSpan CreateContextDuration { get; set; } = TimeSpan.FromMilliseconds(100);
            public long MemoryBeforeLoadBytes { get; set; } = 1024 * 1024 * 10;
            public long MemoryAfterCreateContextBytes { get; set; } = 1024 * 1024 * 20;
            public bool IsDisposed { get; private set; }

            public FakeIfcModelSession()
            {
                Model = IfcStore.Create(Xbim.Common.Step21.XbimSchemaVersion.Ifc4, Xbim.IO.XbimStoreType.InMemoryModel);
                using (var txn = Model.BeginTransaction("Init"))
                {
                    var project = Model.Instances.New<Xbim.Ifc4.Kernel.IfcProject>();
                    project.Name = "Fake Project";
                    txn.Commit();
                }
                GeometryContext = new Xbim3DModelContext(Model);
                GeometryContext.CreateContext();
            }

            public void Dispose()
            {
                IsDisposed = true;
                Model.Close();
                Model.Dispose();
            }
        }

        public class FakeIfcModelSessionService : IIfcModelSessionService
        {
            public IIfcModelSession? Current { get; set; }

            public IIfcModelSession OpenSession(string ifcPath, CancellationToken cancellationToken = default)
            {
                Current?.Dispose();
                Current = new FakeIfcModelSession
                {
                    IfcPath = ifcPath,
                    FileIdentity = "Fake|" + ifcPath.ToUpperInvariant()
                };
                return Current;
            }

            public IIfcModelSession? GetActiveSession(string ifcPath)
            {
                if (Current == null || Current.IsDisposed)
                    return null;

                var identity = "Fake|" + ifcPath.ToUpperInvariant();
                return string.Equals(Current.FileIdentity, identity, StringComparison.OrdinalIgnoreCase)
                    ? Current
                    : null;
            }

            public void Clear()
            {
                Current?.Dispose();
                Current = null;
            }

            public void Dispose()
            {
                Clear();
            }
        }

        // =========================================================================
        // Tests
        // =========================================================================

        [Fact]
        public void TestRealSessionServiceLifecycle()
        {
            var tempPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".ifc");
            using (var model = IfcStore.Create(Xbim.Common.Step21.XbimSchemaVersion.Ifc4, Xbim.IO.XbimStoreType.InMemoryModel))
            {
                using (var txn = model.BeginTransaction("Init"))
                {
                    var project = model.Instances.New<Xbim.Ifc4.Kernel.IfcProject>();
                    project.Name = "Minimal Integration Project";
                    txn.Commit();
                }
                model.SaveAs(tempPath);
            }

            try
            {
                using (var service = new IfcModelSessionService())
                {
                    // 1. Open session
                    var session1 = service.OpenSession(tempPath);
                    Assert.NotNull(session1);
                    Assert.Same(session1, service.Current);
                    Assert.Equal(1, session1.CreateContextCount);
                    Assert.False(session1.IsDisposed);

                    // 2. Get active session should return the same active session
                    var retrieved = service.GetActiveSession(tempPath);
                    Assert.Same(session1, retrieved);

                    // 3. Opening a new path disposes the old session
                    var tempPath2 = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".ifc");
                    File.Copy(tempPath, tempPath2);
                    try
                    {
                        var session2 = service.OpenSession(tempPath2);
                        Assert.True(session1.IsDisposed);
                        Assert.False(session2.IsDisposed);
                        Assert.Same(session2, service.Current);
                    }
                    finally
                    {
                        if (File.Exists(tempPath2)) File.Delete(tempPath2);
                    }
                }
            }
            finally
            {
                if (File.Exists(tempPath)) File.Delete(tempPath);
            }
        }

        [Fact]
        public async Task TestAuditServiceReusesActiveSession()
        {
            var sessionService = new FakeIfcModelSessionService();
            var auditService = new IfcDiagnosticAuditService(sessionService);

            var path = "C:\\Models\\Test.ifc";
            var session = sessionService.OpenSession(path);

            // Call RunAuditAsync. If it reuses the active session, it will not attempt to read from disk.
            var result = await auditService.RunAuditAsync(path);

            Assert.NotNull(result);
            Assert.Empty(result.Classifications);
            Assert.False(session.IsDisposed);
        }

        [Fact]
        public void TestMeshCacheSameSessionSameQuality()
        {
            var sessionId = Guid.NewGuid();
            ElementVisualBuilder.EnsureCacheSessionForTesting(sessionId);
            byte[] shapeData = GetValidShapeDataFromInMemoryModel();
            double oneMeter = 1000.0;
            var quality = ElementVisualBuilder.MeshQuality.Medium;

            // 1. Initial request -> cache miss
            bool reused;
            var mesh1 = ElementVisualBuilder.GetOrCreateCachedMeshForTesting(sessionId, shapeData, oneMeter, quality, out reused);
            Assert.NotNull(mesh1);
            Assert.False(reused);

            var stats1 = ElementVisualBuilder.GetCacheStats();
            Assert.Equal(sessionId, stats1.SessionId);
            Assert.Equal(1, stats1.CacheCount);

            // 2. Second request with same session and quality -> cache hit
            var mesh2 = ElementVisualBuilder.GetOrCreateCachedMeshForTesting(sessionId, shapeData, oneMeter, quality, out reused);
            Assert.Same(mesh1, mesh2);
            Assert.True(reused);

            var stats2 = ElementVisualBuilder.GetCacheStats();
            Assert.Equal(1, stats2.CacheCount);
        }

        [Fact]
        public void TestMeshCacheSameSessionChangedQuality()
        {
            var sessionId = Guid.NewGuid();
            ElementVisualBuilder.EnsureCacheSessionForTesting(sessionId);
            byte[] shapeData = GetValidShapeDataFromInMemoryModel();
            double oneMeter = 1000.0;

            bool reused;
            var mesh1 = ElementVisualBuilder.GetOrCreateCachedMeshForTesting(sessionId, shapeData, oneMeter, ElementVisualBuilder.MeshQuality.Medium, out reused);
            Assert.NotNull(mesh1);
            Assert.False(reused);

            // Changing the quality should generate a separate cache entry / no reuse (separate key)
            var mesh2 = ElementVisualBuilder.GetOrCreateCachedMeshForTesting(sessionId, shapeData, oneMeter, ElementVisualBuilder.MeshQuality.Low, out reused);
            Assert.NotNull(mesh2);
            Assert.False(reused);

            var stats = ElementVisualBuilder.GetCacheStats();
            Assert.Equal(2, stats.CacheCount);
        }

        [Fact]
        public void TestMeshCacheNewSessionClearsCache()
        {
            var session1 = Guid.NewGuid();
            ElementVisualBuilder.EnsureCacheSessionForTesting(session1);
            byte[] shapeData = GetValidShapeDataFromInMemoryModel();
            double oneMeter = 1000.0;

            bool reused;
            ElementVisualBuilder.GetOrCreateCachedMeshForTesting(session1, shapeData, oneMeter, ElementVisualBuilder.MeshQuality.Medium, out reused);

            var stats1 = ElementVisualBuilder.GetCacheStats();
            Assert.Equal(1, stats1.CacheCount);
            Assert.Equal(session1, stats1.SessionId);

            // EnsureCacheSession with a new sessionId should clear the cache and update session ID
            var session2 = Guid.NewGuid();
            ElementVisualBuilder.EnsureCacheSessionForTesting(session2);

            var stats2 = ElementVisualBuilder.GetCacheStats();
            Assert.Equal(0, stats2.CacheCount);
            Assert.Equal(session2, stats2.SessionId);
        }

        [Fact]
        public void TestMeshCacheUnloadClearsCache()
        {
            var sessionId = Guid.NewGuid();
            ElementVisualBuilder.EnsureCacheSessionForTesting(sessionId);
            byte[] shapeData = GetValidShapeDataFromInMemoryModel();
            double oneMeter = 1000.0;

            bool reused;
            ElementVisualBuilder.GetOrCreateCachedMeshForTesting(sessionId, shapeData, oneMeter, ElementVisualBuilder.MeshQuality.Medium, out reused);

            var stats = ElementVisualBuilder.GetCacheStats();
            Assert.Equal(1, stats.CacheCount);

            // Unloading / manual clear
            ElementVisualBuilder.ClearMeshCache("Unloaded project");

            var statsAfter = ElementVisualBuilder.GetCacheStats();
            Assert.Equal(0, statsAfter.CacheCount);
            Assert.Null(statsAfter.SessionId);
        }

        private static byte[] GetValidShapeDataFromInMemoryModel()
        {
            using (var model = IfcStore.Create(Xbim.Common.Step21.XbimSchemaVersion.Ifc4, Xbim.IO.XbimStoreType.InMemoryModel))
            {
                using (var txn = model.BeginTransaction("Create Geometry"))
                {
                    var project = model.Instances.New<Xbim.Ifc4.Kernel.IfcProject>();
                    project.Name = "Test Project";
                    
                    var building = model.Instances.New<Xbim.Ifc4.ProductExtension.IfcBuilding>();
                    building.Name = "Test Building";
                    
                    var column = model.Instances.New<Xbim.Ifc4.SharedBldgElements.IfcColumn>();
                    column.GlobalId = Guid.NewGuid().ToString();
                    column.Name = "Test Column";
                    
                    var profile = model.Instances.New<Xbim.Ifc4.ProfileResource.IfcRectangleProfileDef>();
                    profile.ProfileType = Xbim.Ifc4.Interfaces.IfcProfileTypeEnum.AREA;
                    profile.XDim = 500;
                    profile.YDim = 500;
                    
                    var point = model.Instances.New<Xbim.Ifc4.GeometryResource.IfcCartesianPoint>();
                    point.SetXYZ(0, 0, 0);
                    
                    var axis = model.Instances.New<Xbim.Ifc4.GeometryResource.IfcAxis2Placement3D>();
                    axis.Location = point;
                    
                    var body = model.Instances.New<Xbim.Ifc4.GeometricModelResource.IfcExtrudedAreaSolid>();
                    body.SweptArea = profile;
                    body.Position = axis;
                    body.ExtrudedDirection = model.Instances.New<Xbim.Ifc4.GeometryResource.IfcDirection>();
                    body.ExtrudedDirection.SetXYZ(0, 0, 1);
                    body.Depth = 3000;
                    
                    var shapeRep = model.Instances.New<Xbim.Ifc4.RepresentationResource.IfcShapeRepresentation>();
                    shapeRep.ContextOfItems = model.Instances.New<Xbim.Ifc4.RepresentationResource.IfcGeometricRepresentationContext>();
                    shapeRep.RepresentationIdentifier = "Body";
                    shapeRep.RepresentationType = "SweptSolid";
                    shapeRep.Items.Add(body);
                    
                    var productRep = model.Instances.New<Xbim.Ifc4.RepresentationResource.IfcProductDefinitionShape>();
                    productRep.Representations.Add(shapeRep);
                    column.Representation = productRep;
                    
                    txn.Commit();
                }
                
                var context = new Xbim3DModelContext(model);
                context.CreateContext();
                
                var shapeInstance = context.ShapeInstances().FirstOrDefault();
                if (shapeInstance != null)
                {
                    var geometry = context.ShapeGeometry(shapeInstance);
                    if (geometry != null)
                    {
                        var data = (geometry as IXbimShapeGeometryData)?.ShapeData;
                        if (data != null && data.Length > 0)
                        {
                            return data;
                        }
                    }
                }
            }
            throw new InvalidOperationException("Failed to generate mock shape data.");
        }

        [Fact]
        public void TestIncludeInRenderAtBuilderLevel()
        {
            var builder = new ElementVisualBuilder();
            
            // 1. Element with IncludeInRender = false
            var element1 = new PrecastElement
            {
                ElementID = "Element1",
                IncludeInRender = false,
                ElementCOG = new DomainPoint3D(10, 10, 0)
            };

            var visual1 = builder.Build((element1, null, false)) as ModelVisual3D;
            Assert.NotNull(visual1);
            Assert.Empty(visual1.Children);
            Assert.Null(visual1.Content);

            // 2. BuildBatch with mixed items
            var element2 = new PrecastElement
            {
                ElementID = "Element2",
                IncludeInRender = true,
                ElementCOG = new DomainPoint3D(20, 20, 0),
                BBoxLength = 2, BBoxWidth = 2, BBoxHeight = 2
            };

            var batchVisual = builder.BuildBatch(new[]
            {
                (element1, (LiftStatus?)null, false),
                (element2, (LiftStatus?)null, false)
            }) as ModelVisual3D;

            Assert.NotNull(batchVisual);
            // element1 must be excluded, only element2 rendered as a fallback box
            Assert.Single(batchVisual.Children);
            var child = batchVisual.Children[0] as ModelVisual3D;
            Assert.NotNull(child);
            Assert.Same(element2, child.GetValue(System.Windows.FrameworkElement.TagProperty));
        }

        [Fact]
        public void TestIncludeInGaAtOptimizationInputBuilderLevel()
        {
            var builder = new OptimizationInputBuilder();
            
            var element1 = new PrecastElement
            {
                ElementID = "GA-Enabled",
                ElementType = "Beam",
                IncludeInGa = true,
                ElementCOG = new DomainPoint3D(0, 0, 1),
                ElementWT = 5.0
            };

            var element2 = new PrecastElement
            {
                ElementID = "GA-Blocked",
                ElementType = "Column",
                IncludeInGa = false,
                ElementCOG = new DomainPoint3D(2, 2, 1),
                ElementWT = 6.0
            };

            var crane = new Crane
            {
                Name = "Test Crane",
                Specifications = new List<CraneSpecification>
                {
                    new()
                    {
                        MaxRadius = 40,
                        MaxBoomLength = 50,
                        LoadChart = new List<TargetPoint> { new() { Radius = 10, BoomLength = 20, Capacity = 50 } }
                    }
                }
            };

            var zone = new GreenAreaZone
            {
                Name = "Allowed",
                Type = ZoneType.Allowed,
                Vertices = new List<DomainPoint3D>
                {
                    new(-10, -10, 0), new(10, -10, 0), new(10, 10, 0), new(-10, 10, 0)
                }
            };

            var result = builder.Build(
                new GASettings(),
                crane,
                new List<PrecastElement> { element1, element2 },
                new List<GreenAreaZone> { zone });

            // Assert that element2 (GA-Blocked) was completely excluded from GA scope elements
            Assert.Single(result.Elements);
            Assert.Equal("GA-Enabled", result.Elements[0].ElementID);
        }

        // =========================================================================
        // Phase 1 Regression Tests
        // =========================================================================

        private static string? CallGetBestMarkCandidate(Xbim.Ifc4.Interfaces.IIfcProduct product)
        {
            var extractorType = typeof(IfcReaderRepository).GetNestedType("IfcEngineeringExtractor", System.Reflection.BindingFlags.NonPublic);
            var method = extractorType?.GetMethod("GetBestMarkCandidate", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
            return method?.Invoke(null, new object[] { product }) as string;
        }

        [Fact]
        public void TestGetBestMarkCandidate_PreservesPropertySetMark()
        {
            using (var model = IfcStore.Create(Xbim.Common.Step21.XbimSchemaVersion.Ifc4, Xbim.IO.XbimStoreType.InMemoryModel))
            {
                using (var txn = model.BeginTransaction("Create Element"))
                {
                    var beam = model.Instances.New<Xbim.Ifc4.SharedBldgElements.IfcBeam>();
                    beam.GlobalId = Guid.NewGuid().ToString();
                    beam.Name = "RawProductName";

                    // Create property set Pset_Revit_Identity Data
                    var pset = model.Instances.New<Xbim.Ifc4.Kernel.IfcPropertySet>();
                    pset.Name = "Pset_Revit_Identity Data";

                    var markProp = model.Instances.New<Xbim.Ifc4.PropertyResource.IfcPropertySingleValue>();
                    markProp.Name = "Mark";
                    markProp.NominalValue = new Xbim.Ifc4.MeasureResource.IfcLabel("B1-AssemblyMark");
                    pset.HasProperties.Add(markProp);

                    var rel = model.Instances.New<Xbim.Ifc4.Kernel.IfcRelDefinesByProperties>();
                    rel.RelatedObjects.Add(beam);
                    rel.RelatingPropertyDefinition = pset;

                    txn.Commit();

                    // Act
                    var mark = CallGetBestMarkCandidate(beam);

                    // Assert
                    Assert.Equal("B1-AssemblyMark", mark);
                }
            }
        }

        [Fact]
        public void TestGetBestMarkCandidate_FallsBackToNameIfNoMark()
        {
            using (var model = IfcStore.Create(Xbim.Common.Step21.XbimSchemaVersion.Ifc4, Xbim.IO.XbimStoreType.InMemoryModel))
            {
                using (var txn = model.BeginTransaction("Create Element"))
                {
                    var beam = model.Instances.New<Xbim.Ifc4.SharedBldgElements.IfcBeam>();
                    beam.GlobalId = Guid.NewGuid().ToString();
                    beam.Name = "RawProductName";
                    txn.Commit();

                    // Act
                    var mark = CallGetBestMarkCandidate(beam);

                    // Assert
                    Assert.Equal("RawProductName", mark);
                }
            }
        }

        private static bool CallHasExplicitAssemblyMarkProperties(Xbim.Ifc4.Interfaces.IIfcProduct product)
        {
            var extractorType = typeof(IfcReaderRepository).GetNestedType("IfcEngineeringExtractor", System.Reflection.BindingFlags.NonPublic);
            var method = extractorType?.GetMethod("HasExplicitAssemblyMarkProperties", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
            return (bool)(method?.Invoke(null, new object[] { product }) ?? false);
        }

        [Fact]
        public void TestHasExplicitAssemblyMarkProperties_IgnoresNameFallback()
        {
            using (var model = IfcStore.Create(Xbim.Common.Step21.XbimSchemaVersion.Ifc4, Xbim.IO.XbimStoreType.InMemoryModel))
            {
                using (var txn = model.BeginTransaction("Create Assembly"))
                {
                    var assembly = model.Instances.New<Xbim.Ifc4.ProductExtension.IfcElementAssembly>();
                    assembly.GlobalId = Guid.NewGuid().ToString();
                    assembly.Name = "AssemblyProductName";
                    txn.Commit();

                    // Act
                    var hasExplicitMark = CallHasExplicitAssemblyMarkProperties(assembly);

                    // Assert: Should be false because assembly.Name is not an explicit assembly property
                    Assert.False(hasExplicitMark);
                }
            }
        }

        [Fact]
        public void TestHasExplicitAssemblyMarkProperties_RecognizesExplicitProperties()
        {
            using (var model = IfcStore.Create(Xbim.Common.Step21.XbimSchemaVersion.Ifc4, Xbim.IO.XbimStoreType.InMemoryModel))
            {
                using (var txn = model.BeginTransaction("Create Assembly"))
                {
                    var assembly = model.Instances.New<Xbim.Ifc4.ProductExtension.IfcElementAssembly>();
                    assembly.GlobalId = Guid.NewGuid().ToString();
                    assembly.Name = "AssemblyProductName";

                    // Create property set with "Assembly Mark"
                    var pset = model.Instances.New<Xbim.Ifc4.Kernel.IfcPropertySet>();
                    pset.Name = "Pset_Revit_Identity Data";

                    var markProp = model.Instances.New<Xbim.Ifc4.PropertyResource.IfcPropertySingleValue>();
                    markProp.Name = "Assembly Mark";
                    markProp.NominalValue = new Xbim.Ifc4.MeasureResource.IfcLabel("AM-101");
                    pset.HasProperties.Add(markProp);

                    var rel = model.Instances.New<Xbim.Ifc4.Kernel.IfcRelDefinesByProperties>();
                    rel.RelatedObjects.Add(assembly);
                    rel.RelatingPropertyDefinition = pset;

                    txn.Commit();

                    // Act
                    var hasExplicitMark = CallHasExplicitAssemblyMarkProperties(assembly);

                    // Assert: Should be true because we defined "Assembly Mark"
                    Assert.True(hasExplicitMark);
                }
            }
        }

        private CranePlacementService CreatePlacementService(List<PrecastElement> elements, MockPlacementEngine placementEngine)
        {
            var inputBuilder = new OptimizationInputBuilder();
            var orchestrator = new MockMultiStageGAOrchestrator();
            var scopeEngine = new MockScopeEngine();
            var sceneService = new MockSceneServiceForGating(elements);
            var coordinateService = new CoordinateService();
            var workspaceService = new MockProjectWorkspaceService();
            var historyLogger = new MockHistoryLogger();
            
            return new CranePlacementService(
                inputBuilder,
                placementEngine,
                orchestrator,
                scopeEngine,
                sceneService,
                coordinateService,
                workspaceService,
                null!, // ifcViewer
                historyLogger);
        }

        [Fact]
        public async Task TestGA_Gating_BlocksEstimatedWeight()
        {
            // 1. Element has EstimatedFallback_NotAllowedForGA and IncludeInGa = false
            var element = new PrecastElement
            {
                ElementID = "Element1",
                ElementType = "Beam",
                IncludeInGa = false,
                WeightTrustLevel = IfcWeightTrustLevel.EstimatedFallback_NotAllowedForGA,
                WeightTons = 5.0,
                ElementWT = 5.0,
                ElementCOG = new DomainPoint3D(10, 10, 2)
            };

            var placementEngine = new MockPlacementEngine();
            var service = CreatePlacementService(new List<PrecastElement> { element }, placementEngine);
            var crane = new Crane { Name = "Test Crane" };

            // Act & Assert
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => 
                service.RunOptimizationAsync(new GASettings(), crane));
            Assert.Contains("IncludeInGa flag is false", ex.Message);
        }

        [Fact]
        public async Task TestGA_Gating_BlocksEstimatedWeightEvenIfIncludeInGaIsTrue()
        {
            // 2. Element has EstimatedFallback_NotAllowedForGA and IncludeInGa = true
            var element = new PrecastElement
            {
                ElementID = "Element1",
                ElementType = "Beam",
                IncludeInGa = true,
                WeightTrustLevel = IfcWeightTrustLevel.EstimatedFallback_NotAllowedForGA,
                WeightTons = 5.0,
                ElementWT = 5.0,
                ElementCOG = new DomainPoint3D(10, 10, 2)
            };

            var placementEngine = new MockPlacementEngine();
            var service = CreatePlacementService(new List<PrecastElement> { element }, placementEngine);
            var crane = new Crane { Name = "Test Crane" };

            // Act & Assert
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => 
                service.RunOptimizationAsync(new GASettings(), crane));
            Assert.Contains("Untrusted weight source/trust level", ex.Message);
        }

        [Fact]
        public async Task TestGA_AssemblyGating_PreventsDoubleCounting()
        {
            // Parent Assembly is included and has trusted weight
            var parent = new PrecastElement
            {
                ElementID = "ParentAssembly",
                ElementType = "Assembly",
                Role = IfcElementRole.LiftAssembly,
                IncludeInGa = true,
                WeightTrustLevel = IfcWeightTrustLevel.ExplicitQuantity,
                WeightTons = 10.0,
                ElementWT = 10.0,
                ElementCOG = new DomainPoint3D(10, 10, 2)
            };

            // Child element is also included
            var child = new PrecastElement
            {
                ElementID = "ChildPart",
                ParentGlobalId = "ParentAssembly",
                ElementType = "Beam",
                Role = IfcElementRole.LiftableElement,
                IncludeInGa = true,
                WeightTrustLevel = IfcWeightTrustLevel.ExplicitQuantity,
                WeightTons = 5.0,
                ElementWT = 5.0,
                ElementCOG = new DomainPoint3D(10, 10, 2)
            };

            var placementEngine = new MockPlacementEngine();
            var service = CreatePlacementService(new List<PrecastElement> { parent, child }, placementEngine);
            
            var crane = new Crane
            {
                Name = "Test Crane",
                Specifications = new List<CraneSpecification>
                {
                    new()
                    {
                        MaxRadius = 40,
                        MaxBoomLength = 50,
                        LoadChart = new List<TargetPoint> { new() { Radius = 10, BoomLength = 20, Capacity = 50 } }
                    }
                }
            };

            // Act
            await service.RunOptimizationAsync(new GASettings { EnableMultiStageDiscovery = false }, crane);

            // Assert
            Assert.NotNull(placementEngine.LastInput);
            var gaElements = placementEngine.LastInput.Elements;
            
            // Should contain parent but exclude child part to prevent double-counting
            Assert.Contains(gaElements, e => e.ElementID == "ParentAssembly");
            Assert.DoesNotContain(gaElements, e => e.ElementID == "ChildPart");
        }

        // =========================================================================
        // Mocks for testing CranePlacementService
        // =========================================================================

        private class MockPlacementEngine : ICranePlacementEngine
        {
            public PlacementInput? LastInput { get; private set; }
            public PlacementResult RunOptimization(PlacementInput input, CancellationToken cancellationToken = default)
            {
                LastInput = input;
                return new PlacementResult();
            }
        }

        private class MockMultiStageGAOrchestrator : IMultiStageGAOrchestrator
        {
            public Task<MultiStageGAResult> RunAsync(PlacementInput project, Crane crane, GASettings baseSettings, double coverageTarget, CancellationToken cancellationToken)
            {
                var emptyResult = new PlacementResult();
                return Task.FromResult(new MultiStageGAResult(emptyResult, 1, new List<PlacementResult> { emptyResult }, "Completed"));
            }
        }

        private class MockScopeEngine : IOptimizationScopeEngine
        {
            public List<PrecastElement> FilterElements(List<PrecastElement> allElements, OptimizationScopeFilter filter)
            {
                return allElements; // No filtering
            }
        }

        private class MockSceneServiceForGating : ISceneService
        {
            private readonly List<PrecastElement> _elements;
            public MockSceneServiceForGating(List<PrecastElement> elements)
            {
                _elements = elements;
            }
            public List<PrecastElement> GetElements() => _elements;
            public void SetElements(List<PrecastElement> elements) { }
            public void ClearElements() { }
            public List<PrecastElement> GetSelectedElements() => _elements;
            public void SetSelectedElements(List<PrecastElement> elements) { }
            public void ClearSelectedElements() { }
            public event Action<List<PrecastElement>>? ElementsChanged;
            public event Action<List<PrecastElement>>? ElementsSelectionChanged;
            public List<GreenAreaZone> GetZones() => new()
            {
                new GreenAreaZone
                {
                    ZoneId = Guid.NewGuid(),
                    Name = "Test Zone",
                    Type = ZoneType.Allowed,
                    Vertices = new List<DomainPoint3D>
                    {
                        new(0, 0, 0),
                        new(100, 0, 0),
                        new(100, 100, 0),
                        new(0, 100, 0)
                    }
                }
            };
            public void AddZone(GreenAreaZone zone) { }
            public void RemoveZone(GreenAreaZone zone) { }
            public void RemoveSelectedZone() { }
            public void ClearZones() { }
            public void UpdateZone(GreenAreaZone zone) { }
            public GreenAreaZone? SelectedZone => null;
            public void SetSelectedZone(GreenAreaZone? zone) { }
            public void ClearZoneSelection() { }
            public event Action<GreenAreaZone>? ZoneAdded;
            public event Action<GreenAreaZone>? ZoneRemoved;
            public event Action<GreenAreaZone>? ZoneUpdated;
            public event Action? ZonesCleared;
            public event Action<GreenAreaZone?>? ZoneSelected;
            
            public List<SceneCrane> GetCranes() => new();
            public void AddCrane(SceneCrane crane) { }
            public void RemoveCrane(SceneCrane crane) { }
            public void ClearCranes() { }
            public void MoveCrane(SceneCrane crane, double x, double y, double z) { }
            public void RotateCrane(SceneCrane crane, double angle) { }
            public void UpdateCrane(SceneCrane crane) { }
            public SceneCrane? SelectedCrane => null;
            public void SetSelectedCrane(SceneCrane? crane) { }
            public void ClearCraneSelection() { }
            public event Action<SceneCrane>? CraneAdded;
            public event Action<SceneCrane>? CraneRemoved;
            public event Action? CranesCleared;
            public event Action<SceneCrane>? CraneMoved;
            public event Action<SceneCrane>? CraneRotated;
            public event Action<SceneCrane>? CraneUpdated;
            public event Action<SceneCrane?>? CraneSelected;
            
            public List<SceneDxf> GetDxfEntities() => new();
            public void AddDxfEntity(SceneDxf entity) { }
            public void RemoveDxfEntity(SceneDxf entity) { }
            public void RemoveSelectedDxfEntity() { }
            public void ClearDxfEntities() { }
            public void UpdateDxfEntity(SceneDxf entity) { }
            public SceneDxf? SelectedDxfEntity => null;
            public void SetSelectedDxfEntity(SceneDxf? entity) { }
            public void ClearDxfSelection() { }
            public event Action<SceneDxf>? DxfEntityAdded;
            public event Action<SceneDxf>? DxfEntityRemoved;
            public event Action<SceneDxf>? DxfEntityUpdated;
            public event Action? DxfEntitiesCleared;
            public event Action<SceneDxf?>? DxfEntitySelected;
            
            public object? GetSelectedObject() => null;
            public void SetSelectedObject(object? obj) { }
            public event Action<object?>? SelectionChanged;
        }

        private class MockProjectWorkspaceService : IProjectWorkspaceService
        {
            public ProjectSession? CurrentSession => null;
            public Task<ProjectSession> CreateProjectAsync(CreateProjectRequest r, CancellationToken c) => Task.FromResult(new ProjectSession());
            public Task<IReadOnlyList<ProjectCardModel>> GetRecentProjectsAsync(CancellationToken c) => Task.FromResult<IReadOnlyList<ProjectCardModel>>(new List<ProjectCardModel>());
            public Task<ProjectSession?> OpenProjectAsync(Guid id, CancellationToken c) => Task.FromResult<ProjectSession?>(null);
            public Task<ProjectSession> OpenProjectFolderAsync(string path, CancellationToken c) => Task.FromResult(new ProjectSession());
            public Task DeleteProjectAsync(Guid id, CancellationToken c) => Task.CompletedTask;
            public Task ToggleFavoriteAsync(Guid id, bool f, CancellationToken c) => Task.CompletedTask;
            public Task<IReadOnlyList<ProjectCardModel>> SearchProjectsAsync(string s, bool f, CancellationToken c) => Task.FromResult<IReadOnlyList<ProjectCardModel>>(new List<ProjectCardModel>());
        }

        private class MockHistoryLogger : IOptimizationHistoryLogger
        {
            public Task LogRunAsync(OptimizationRunEntry entry, string projectFolderPath) => Task.CompletedTask;
        }
    }
}
