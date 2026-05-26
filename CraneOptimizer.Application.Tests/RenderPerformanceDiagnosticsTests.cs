using CraneOptimizer.Domain.Common;
using CraneOptimizer.Domain.Diagnostics;
using CraneOptimizer.Domain.Schemas;
using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Models;
using CraneOptimizer.UI.Core.Interfaces;
using CraneOptimizer.UI.Core.Services;
using CraneOptimizer.UI.Features.F2_IfcViewer;
using CraneOptimizer.Infrastructure.Ifc;
using CraneOptimizer.Infrastructure.Repositories;
using System.Threading;
using Xunit;

[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace CraneOptimizer.Application.Tests
{
    /// <summary>
    /// Unit tests for Phase 2.0: Render Performance Metrics &amp; Baseline.
    /// Validates diagnostics DTO, store behavior, builder counters, and counter resets.
    /// </summary>
    public class RenderPerformanceDiagnosticsTests
    {
        /// <summary>
        /// Runs an action on an STA thread, required for WPF object creation in tests.
        /// </summary>
        private static void RunOnSta(Action action)
        {
            Exception? caught = null;
            var thread = new Thread(() =>
            {
                try { action(); }
                catch (Exception ex) { caught = ex; }
            });
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            thread.Join();
            if (caught != null)
                throw new AggregateException("STA thread threw an exception", caught);
        }

        #region RenderPerformanceStats DTO tests

        [Fact]
        public void Stats_DefaultValues_AllZero()
        {
            var stats = new RenderPerformanceStats();
            Assert.Equal(0, stats.TotalElements);
            Assert.Equal(0, stats.VisibleElements);
            Assert.Equal(0, stats.IncludeInRenderSkippedCount);
            Assert.Equal(0, stats.ShapeInstanceCount);
            Assert.Equal(0, stats.MeshesCreatedCount);
            Assert.Equal(0, stats.MeshCacheHitCount);
            Assert.Equal(0, stats.MeshCacheMissCount);
            Assert.Equal(0, stats.GeometryModel3DCount);
            Assert.Equal(0, stats.ModelVisual3DCount);
            Assert.Equal(0, stats.MaterialCreatedCount);
            Assert.Equal(0, stats.FallbackBoxCount);
            Assert.Equal(0, stats.RealMeshElementCount);
            Assert.Equal(0.0, stats.RenderDurationMs);
            Assert.Equal(0.0, stats.MemoryBeforeRenderMB);
            Assert.Equal(0.0, stats.MemoryAfterRenderMB);
            Assert.Null(stats.SessionId);
            Assert.Null(stats.FileName);
            Assert.Null(stats.RenderQuality);
        }

        [Fact]
        public void Stats_CanSetAllProperties()
        {
            var id = Guid.NewGuid();
            var ts = DateTime.UtcNow;
            var stats = new RenderPerformanceStats
            {
                SessionId = id,
                FileName = "test.ifc",
                RenderQuality = "Medium",
                TotalElements = 100,
                VisibleElements = 80,
                IncludeInRenderSkippedCount = 20,
                ShapeInstanceCount = 300,
                MeshesCreatedCount = 50,
                MeshCacheHitCount = 250,
                MeshCacheMissCount = 50,
                GeometryModel3DCount = 300,
                ModelVisual3DCount = 80,
                MaterialCreatedCount = 300,
                FallbackBoxCount = 0,
                RealMeshElementCount = 80,
                RenderDurationMs = 1234.5,
                ReRenderDurationMs = 1234.5,
                MemoryBeforeRenderMB = 400.0,
                MemoryAfterRenderMB = 500.0,
                TimestampUtc = ts
            };

            Assert.Equal(id, stats.SessionId);
            Assert.Equal("test.ifc", stats.FileName);
            Assert.Equal("Medium", stats.RenderQuality);
            Assert.Equal(100, stats.TotalElements);
            Assert.Equal(80, stats.VisibleElements);
            Assert.Equal(20, stats.IncludeInRenderSkippedCount);
            Assert.Equal(300, stats.ShapeInstanceCount);
            Assert.Equal(50, stats.MeshesCreatedCount);
            Assert.Equal(250, stats.MeshCacheHitCount);
            Assert.Equal(300, stats.GeometryModel3DCount);
            Assert.Equal(80, stats.ModelVisual3DCount);
            Assert.Equal(0, stats.FallbackBoxCount);
            Assert.Equal(1234.5, stats.RenderDurationMs);
            Assert.Equal(400.0, stats.MemoryBeforeRenderMB);
            Assert.Equal(500.0, stats.MemoryAfterRenderMB);
            Assert.Equal(ts, stats.TimestampUtc);
        }

        #endregion

        #region RenderDiagnosticsStore tests

        [Fact]
        public void Store_Initially_ReturnsNullLastAndEmptyHistory()
        {
            var store = new RenderDiagnosticsStore();
            Assert.Null(store.GetLastRenderStats());
            Assert.Empty(store.GetRenderStatsHistory());
        }

        [Fact]
        public void Store_AddStats_UpdatesLastAndHistory()
        {
            var store = new RenderDiagnosticsStore();
            var stats = new RenderPerformanceStats { TotalElements = 10, RenderQuality = "Low" };

            store.AddStats(stats);

            Assert.Same(stats, store.GetLastRenderStats());
            Assert.Single(store.GetRenderStatsHistory());
        }

        [Fact]
        public void Store_AddMultipleStats_LastIsAlwaysMostRecent()
        {
            var store = new RenderDiagnosticsStore();
            var s1 = new RenderPerformanceStats { TotalElements = 1 };
            var s2 = new RenderPerformanceStats { TotalElements = 2 };
            var s3 = new RenderPerformanceStats { TotalElements = 3 };

            store.AddStats(s1);
            store.AddStats(s2);
            store.AddStats(s3);

            Assert.Same(s3, store.GetLastRenderStats());
            Assert.Equal(3, store.GetRenderStatsHistory().Count);
        }

        [Fact]
        public void Store_HistoryCappedAt100()
        {
            var store = new RenderDiagnosticsStore();

            for (int i = 0; i < 120; i++)
                store.AddStats(new RenderPerformanceStats { TotalElements = i });

            var history = store.GetRenderStatsHistory();
            Assert.Equal(100, history.Count);

            // Oldest entries should have been removed; first item should be index 20
            Assert.Equal(20, history[0].TotalElements);
            Assert.Equal(119, history[^1].TotalElements);
        }

        [Fact]
        public void Store_Clear_ResetsLastAndHistory()
        {
            var store = new RenderDiagnosticsStore();
            store.AddStats(new RenderPerformanceStats { TotalElements = 5 });
            store.AddStats(new RenderPerformanceStats { TotalElements = 10 });

            store.Clear();

            Assert.Null(store.GetLastRenderStats());
            Assert.Empty(store.GetRenderStatsHistory());
        }

        [Fact]
        public void Store_AddNull_DoesNotThrow()
        {
            var store = new RenderDiagnosticsStore();
            store.AddStats(null!);

            Assert.Null(store.GetLastRenderStats());
            Assert.Empty(store.GetRenderStatsHistory());
        }

        [Fact]
        public void Store_ThreadSafety_ConcurrentAdds()
        {
            var store = new RenderDiagnosticsStore();
            var tasks = new List<Task>();

            for (int i = 0; i < 10; i++)
            {
                var idx = i;
                tasks.Add(Task.Run(() =>
                {
                    for (int j = 0; j < 50; j++)
                        store.AddStats(new RenderPerformanceStats { TotalElements = idx * 50 + j });
                }));
            }

            Task.WaitAll(tasks.ToArray());

            // 10 threads × 50 adds = 500 total, capped at 100
            Assert.Equal(100, store.GetRenderStatsHistory().Count);
            Assert.NotNull(store.GetLastRenderStats());
        }

        [Fact]
        public void Store_GetRenderStatsHistory_ReflectsLiveState()
        {
            var store = new RenderDiagnosticsStore();
            store.AddStats(new RenderPerformanceStats { TotalElements = 1 });
            Assert.Single(store.GetRenderStatsHistory());

            store.AddStats(new RenderPerformanceStats { TotalElements = 2 });
            Assert.Equal(2, store.GetRenderStatsHistory().Count);
        }

        #endregion

        #region ElementVisualBuilder diagnostics counter tests

        private static PrecastElement CreateTestElement(
            string id = "E1",
            bool includeInRender = true,
            bool includeInGa = true)
        {
            return new PrecastElement
            {
                ElementID = id,
                IncludeInRender = includeInRender,
                IncludeInGa = includeInGa,
                ElementCOG = new DomainPoint3D(0, 0, 0),
                BBoxLength = 2.0,
                BBoxWidth = 1.0,
                BBoxHeight = 0.5,
                ElementLength = 2.0,
                ElementWidth = 1.0,
                ElementHeight = 0.5,
                ElementWT = 1000,
                ElementType = "Wall",
                ElementMaterial = "Concrete"
            };
        }

        [Fact]
        public void Builder_ResetCounters_AllZero()
        {
            var builder = new ElementVisualBuilder();
            builder.ResetCounters();

            Assert.Equal(0, builder.DiagTotalElements);
            Assert.Equal(0, builder.DiagVisibleElements);
            Assert.Equal(0, builder.DiagIncludeInRenderSkipped);
            Assert.Equal(0, builder.DiagShapeInstances);
            Assert.Equal(0, builder.DiagMeshesCreated);
            Assert.Equal(0, builder.DiagMeshCacheHits);
            Assert.Equal(0, builder.DiagMeshCacheMisses);
            Assert.Equal(0, builder.DiagGeometryModel3DCount);
            Assert.Equal(0, builder.DiagModelVisual3DCount);
            Assert.Equal(0, builder.DiagMaterialCreated);
            Assert.Equal(0, builder.DiagFallbackBoxCount);
            Assert.Equal(0, builder.DiagRealMeshElements);
        }

        [Fact]
        public void Builder_Build_VisibleElement_IncrementsCounters()
        {
            RunOnSta(() =>
            {
                var builder = new ElementVisualBuilder();
                builder.ResetCounters();
                var element = CreateTestElement();

                builder.Build((element, null, false));

                Assert.Equal(1, builder.DiagTotalElements);
                Assert.Equal(1, builder.DiagVisibleElements);
                Assert.Equal(0, builder.DiagIncludeInRenderSkipped);
                Assert.Equal(1, builder.DiagFallbackBoxCount);
                Assert.Equal(1, builder.DiagGeometryModel3DCount);
                Assert.Equal(1, builder.DiagModelVisual3DCount);
                Assert.Equal(1, builder.DiagMaterialCreated);
            });
        }

        [Fact]
        public void Builder_Build_HiddenElement_IncrementsSkipCount()
        {
            RunOnSta(() =>
            {
                var builder = new ElementVisualBuilder();
                builder.ResetCounters();
                var element = CreateTestElement(includeInRender: false);

                builder.Build((element, null, false));

                Assert.Equal(1, builder.DiagTotalElements);
                Assert.Equal(0, builder.DiagVisibleElements);
                Assert.Equal(1, builder.DiagIncludeInRenderSkipped);
                Assert.Equal(0, builder.DiagFallbackBoxCount);
                Assert.Equal(0, builder.DiagGeometryModel3DCount);
            });
        }

        [Fact]
        public void Builder_BuildBatch_MixedElements_CountsCorrectly()
        {
            RunOnSta(() =>
            {
                var builder = new ElementVisualBuilder();
                builder.ResetCounters();

                var items = new List<(PrecastElement element, LiftStatus? status, bool isSelected)>
                {
                    (CreateTestElement("E1"), null, false),
                    (CreateTestElement("E2"), null, false),
                    (CreateTestElement("E3", includeInRender: false), null, false),
                    (CreateTestElement("E4"), LiftStatus.Liftable, false),
                    (CreateTestElement("E5", includeInRender: false), null, false),
                };

                builder.BuildBatch(items);

                Assert.Equal(5, builder.DiagTotalElements);
                Assert.Equal(3, builder.DiagVisibleElements);
                Assert.Equal(2, builder.DiagIncludeInRenderSkipped);
                // Without IFC reader, falls back to boxes for visible elements
                Assert.Equal(3, builder.DiagFallbackBoxCount);
                Assert.Equal(3, builder.DiagGeometryModel3DCount);
                Assert.Equal(3, builder.DiagModelVisual3DCount);
                Assert.Equal(3, builder.DiagMaterialCreated);
            });
        }

        [Fact]
        public void Builder_BuildBatch_AllHidden_NoFallbackBoxes()
        {
            RunOnSta(() =>
            {
                var builder = new ElementVisualBuilder();
                builder.ResetCounters();

                var items = new List<(PrecastElement element, LiftStatus? status, bool isSelected)>
                {
                    (CreateTestElement("E1", includeInRender: false), null, false),
                    (CreateTestElement("E2", includeInRender: false), null, false),
                };

                builder.BuildBatch(items);

                Assert.Equal(2, builder.DiagTotalElements);
                Assert.Equal(0, builder.DiagVisibleElements);
                Assert.Equal(2, builder.DiagIncludeInRenderSkipped);
                Assert.Equal(0, builder.DiagFallbackBoxCount);
            });
        }

        [Fact]
        public void Builder_CountersResetBetweenCycles()
        {
            RunOnSta(() =>
            {
                var builder = new ElementVisualBuilder();

                // First cycle
                builder.ResetCounters();
                builder.Build((CreateTestElement("E1"), null, false));
                Assert.Equal(1, builder.DiagTotalElements);
                Assert.Equal(1, builder.DiagFallbackBoxCount);

                // Second cycle — reset should clear
                builder.ResetCounters();
                Assert.Equal(0, builder.DiagTotalElements);
                Assert.Equal(0, builder.DiagFallbackBoxCount);

                builder.Build((CreateTestElement("E2"), null, false));
                Assert.Equal(1, builder.DiagTotalElements);
                Assert.Equal(1, builder.DiagFallbackBoxCount);
            });
        }

        [Fact]
        public void Builder_BuildBatch_LowQuality_CountsGeometryModels()
        {
            RunOnSta(() =>
            {
                var previousQuality = ElementVisualBuilder.CurrentQuality;
                try
                {
                    ElementVisualBuilder.CurrentQuality = ElementVisualBuilder.MeshQuality.Low;
                    var builder = new ElementVisualBuilder();
                    builder.ResetCounters();

                    var items = new List<(PrecastElement element, LiftStatus? status, bool isSelected)>
                    {
                        (CreateTestElement("E1"), null, false),
                        (CreateTestElement("E2"), LiftStatus.Liftable, false),
                        (CreateTestElement("E3"), null, true),
                    };

                    builder.BuildBatch(items);

                    Assert.Equal(3, builder.DiagTotalElements);
                    Assert.Equal(3, builder.DiagVisibleElements);
                    Assert.Equal(0, builder.DiagIncludeInRenderSkipped);
                    // Low quality uses BuildLargeModelBatch, which creates GeometryModel3Ds
                    Assert.True(builder.DiagGeometryModel3DCount > 0);
                    Assert.Equal(1, builder.DiagModelVisual3DCount); // batch root
                    Assert.Equal(0, builder.DiagFallbackBoxCount);
                }
                finally
                {
                    ElementVisualBuilder.CurrentQuality = previousQuality;
                }
            });
        }

        [Fact]
        public void Builder_Build_MultipleCalls_Accumulates()
        {
            RunOnSta(() =>
            {
                var builder = new ElementVisualBuilder();
                builder.ResetCounters();

                builder.Build((CreateTestElement("E1"), null, false));
                builder.Build((CreateTestElement("E2"), null, false));
                builder.Build((CreateTestElement("E3", includeInRender: false), null, false));

                Assert.Equal(3, builder.DiagTotalElements);
                Assert.Equal(2, builder.DiagVisibleElements);
                Assert.Equal(1, builder.DiagIncludeInRenderSkipped);
                Assert.Equal(2, builder.DiagFallbackBoxCount);
            });
        }

        #endregion

        #region Integration: Store + Stats flow

        [Fact]
        public void Integration_StatsFlowThroughStore()
        {
            var store = new RenderDiagnosticsStore();

            // Simulate what SceneRenderingManager does after a render
            var stats = new RenderPerformanceStats
            {
                RenderQuality = "Medium",
                TotalElements = 50,
                VisibleElements = 45,
                IncludeInRenderSkippedCount = 5,
                FallbackBoxCount = 45,
                RenderDurationMs = 200.5,
                MemoryBeforeRenderMB = 300.0,
                MemoryAfterRenderMB = 350.0,
                TimestampUtc = DateTime.UtcNow
            };

            store.AddStats(stats);

            var last = store.GetLastRenderStats();
            Assert.NotNull(last);
            Assert.Equal("Medium", last!.RenderQuality);
            Assert.Equal(50, last.TotalElements);
            Assert.Equal(45, last.VisibleElements);
            Assert.Equal(5, last.IncludeInRenderSkippedCount);
            Assert.Equal(200.5, last.RenderDurationMs);
        }

        [Fact]
        public void Integration_MultipleRenders_QualityChanges_RecordedInHistory()
        {
            var store = new RenderDiagnosticsStore();

            store.AddStats(new RenderPerformanceStats { RenderQuality = "Low", TotalElements = 100 });
            store.AddStats(new RenderPerformanceStats { RenderQuality = "Medium", TotalElements = 100 });
            store.AddStats(new RenderPerformanceStats { RenderQuality = "High", TotalElements = 100 });

            var history = store.GetRenderStatsHistory();
            Assert.Equal(3, history.Count);
            Assert.Equal("Low", history[0].RenderQuality);
            Assert.Equal("Medium", history[1].RenderQuality);
            Assert.Equal("High", history[2].RenderQuality);
        }

        [Fact]
        public void Integration_ClearBetweenSessions_ResetsHistory()
        {
            var store = new RenderDiagnosticsStore();
            store.AddStats(new RenderPerformanceStats { TotalElements = 10 });
            store.AddStats(new RenderPerformanceStats { TotalElements = 20 });
            Assert.Equal(2, store.GetRenderStatsHistory().Count);

            // New session — clear
            store.Clear();
            Assert.Empty(store.GetRenderStatsHistory());

            store.AddStats(new RenderPerformanceStats { TotalElements = 30 });
            Assert.Single(store.GetRenderStatsHistory());
            Assert.Equal(30, store.GetLastRenderStats()!.TotalElements);
        }

        #endregion

        #region Phase 2.2A Batching tests

        [Fact]
        public void Builder_Batching_WhenDisabled_RestoresOldBehavior()
        {
            RunOnSta(() =>
            {
                var smallIfcPath = @"F:\06-ITI\00-Project\tryProjects\try6\Models\IFC\teklaModelTestIFC_10.ifc";
                if (!System.IO.File.Exists(smallIfcPath)) return;

                var sessionService = new IfcModelSessionService();
                var auditService = new IfcDiagnosticAuditService(sessionService);
                var exporterProfileResolver = new IfcExporterProfileResolver();
                var normalizationService = new IfcNormalizationDecisionService(exporterProfileResolver);
                var readerRepository = new IfcReaderRepository(auditService, normalizationService, sessionService);

                var elements = readerRepository.ReadElements(smallIfcPath);
                var origin = new DomainPoint3D(0, 0, 0);
                var placementOffset = new DomainPoint3D(0, 0, 0);

                var previousQuality = ElementVisualBuilder.CurrentQuality;
                var previousFlag = ElementVisualBuilder.UseBatchedRealMeshRendering;
                try
                {
                    ElementVisualBuilder.CurrentQuality = ElementVisualBuilder.MeshQuality.Medium;
                    
                    // 1. Render with batching disabled
                    ElementVisualBuilder.UseBatchedRealMeshRendering = false;
                    var builderDisabled = new ElementVisualBuilder(readerRepository, origin, placementOffset);
                    builderDisabled.ResetCounters();
                    var visualDisabled = builderDisabled.BuildBatch(elements.Select(e => (e, (LiftStatus?)null, false)));

                    Assert.Equal(0, builderDisabled.DiagBatchedGroups);
                    Assert.Equal(0, builderDisabled.DiagHitTestBoxes);
                    Assert.Equal(0, builderDisabled.DiagHitTestGeometryModel3DCount);
                    Assert.Equal(builderDisabled.DiagGeometryModel3DCount, builderDisabled.DiagRenderGeometryModel3DCount);
                }
                finally
                {
                    ElementVisualBuilder.CurrentQuality = previousQuality;
                    ElementVisualBuilder.UseBatchedRealMeshRendering = previousFlag;
                    readerRepository.ClearData();
                }
            });
        }

        [Fact]
        public void Builder_Batching_WhenEnabled_BatchesUnselectedAndRendersHitBoxes()
        {
            RunOnSta(() =>
            {
                var smallIfcPath = @"F:\06-ITI\00-Project\tryProjects\try6\Models\IFC\teklaModelTestIFC_10.ifc";
                if (!System.IO.File.Exists(smallIfcPath)) return;

                var sessionService = new IfcModelSessionService();
                var auditService = new IfcDiagnosticAuditService(sessionService);
                var exporterProfileResolver = new IfcExporterProfileResolver();
                var normalizationService = new IfcNormalizationDecisionService(exporterProfileResolver);
                var readerRepository = new IfcReaderRepository(auditService, normalizationService, sessionService);

                var elements = readerRepository.ReadElements(smallIfcPath);
                var origin = new DomainPoint3D(0, 0, 0);
                var placementOffset = new DomainPoint3D(0, 0, 0);

                var previousQuality = ElementVisualBuilder.CurrentQuality;
                var previousFlag = ElementVisualBuilder.UseBatchedRealMeshRendering;
                try
                {
                    ElementVisualBuilder.CurrentQuality = ElementVisualBuilder.MeshQuality.Medium;
                    
                    // 1. Render with batching ENABLED (all unselected)
                    ElementVisualBuilder.UseBatchedRealMeshRendering = true;
                    var builderEnabled = new ElementVisualBuilder(readerRepository, origin, placementOffset);
                    builderEnabled.ResetCounters();
                    
                    // Select none
                    var items = elements.Select(e => (element: e, status: (LiftStatus?)null, isSelected: false)).ToList();
                    var visualEnabled = builderEnabled.BuildBatch(items);

                    // Verify batching results
                    Assert.True(builderEnabled.DiagBatchedGroups > 0);
                    Assert.True(builderEnabled.DiagRenderGeometryModel3DCount < items.Count); // Should be significantly fewer than per-element count
                    Assert.Equal(builderEnabled.DiagHitTestBoxes, builderEnabled.DiagHitTestGeometryModel3DCount);
                    Assert.Equal(builderEnabled.DiagRenderGeometryModel3DCount + builderEnabled.DiagHitTestGeometryModel3DCount, builderEnabled.DiagGeometryModel3DCount);

                    // Hit boxes should be created per visible element
                    var visibleCount = items.Count(i => i.element.IncludeInRender);
                    Assert.Equal(visibleCount, builderEnabled.DiagHitTestBoxes);

                    // 2. Verify that selection works (one selected element is excluded from batching)
                    var itemsWithSelection = elements.Select((e, idx) => (element: e, status: (LiftStatus?)null, isSelected: idx == 0)).ToList();
                    var builderSelection = new ElementVisualBuilder(readerRepository, origin, placementOffset);
                    builderSelection.ResetCounters();
                    var visualSelection = builderSelection.BuildBatch(itemsWithSelection);

                    // Selected element is rendered independently, so hit box is only created for unselected elements (visibleCount - 1)
                    Assert.Equal(visibleCount - 1, builderSelection.DiagHitTestBoxes);
                }
                finally
                {
                    ElementVisualBuilder.CurrentQuality = previousQuality;
                    ElementVisualBuilder.UseBatchedRealMeshRendering = previousFlag;
                    readerRepository.ClearData();
                }
            });
        }

        [Fact]
        public void Builder_Batching_IncludeInRenderFalse_ExcludedFromBatchAndHitBoxes()
        {
            RunOnSta(() =>
            {
                var smallIfcPath = @"F:\06-ITI\00-Project\tryProjects\try6\Models\IFC\teklaModelTestIFC_10.ifc";
                if (!System.IO.File.Exists(smallIfcPath)) return;

                var sessionService = new IfcModelSessionService();
                var auditService = new IfcDiagnosticAuditService(sessionService);
                var exporterProfileResolver = new IfcExporterProfileResolver();
                var normalizationService = new IfcNormalizationDecisionService(exporterProfileResolver);
                var readerRepository = new IfcReaderRepository(auditService, normalizationService, sessionService);

                var elements = readerRepository.ReadElements(smallIfcPath);
                var origin = new DomainPoint3D(0, 0, 0);
                var placementOffset = new DomainPoint3D(0, 0, 0);

                var previousQuality = ElementVisualBuilder.CurrentQuality;
                var previousFlag = ElementVisualBuilder.UseBatchedRealMeshRendering;
                try
                {
                    ElementVisualBuilder.CurrentQuality = ElementVisualBuilder.MeshQuality.Medium;
                    ElementVisualBuilder.UseBatchedRealMeshRendering = true;

                    // Set one element IncludeInRender = false
                    var modifiedElements = elements.ToList();
                    if (modifiedElements.Count > 0)
                    {
                        modifiedElements[0] = new PrecastElement
                        {
                            ElementID = modifiedElements[0].ElementID,
                            IncludeInRender = false,
                            ElementCOG = modifiedElements[0].ElementCOG
                        };
                    }

                    var builder = new ElementVisualBuilder(readerRepository, origin, placementOffset);
                    builder.ResetCounters();
                    var items = modifiedElements.Select(e => (element: e, status: (LiftStatus?)null, isSelected: false)).ToList();
                    builder.BuildBatch(items);

                    // Skipped count should be 1
                    Assert.Equal(1, builder.DiagIncludeInRenderSkipped);
                    
                    // Hit boxes should be created only for visible elements
                    var visibleCount = items.Count(i => i.element.IncludeInRender);
                    Assert.Equal(visibleCount, builder.DiagHitTestBoxes);
                }
                finally
                {
                    ElementVisualBuilder.CurrentQuality = previousQuality;
                    ElementVisualBuilder.UseBatchedRealMeshRendering = previousFlag;
                    readerRepository.ClearData();
                }
            });
        }
        
        [Fact]
        public void Builder_Batching_CacheHitsStillWork()
        {
            RunOnSta(() =>
            {
                var smallIfcPath = @"F:\06-ITI\00-Project\tryProjects\try6\Models\IFC\teklaModelTestIFC_10.ifc";
                if (!System.IO.File.Exists(smallIfcPath)) return;

                var sessionService = new IfcModelSessionService();
                var auditService = new IfcDiagnosticAuditService(sessionService);
                var exporterProfileResolver = new IfcExporterProfileResolver();
                var normalizationService = new IfcNormalizationDecisionService(exporterProfileResolver);
                var readerRepository = new IfcReaderRepository(auditService, normalizationService, sessionService);

                var elements = readerRepository.ReadElements(smallIfcPath);
                var origin = new DomainPoint3D(0, 0, 0);
                var placementOffset = new DomainPoint3D(0, 0, 0);

                var previousQuality = ElementVisualBuilder.CurrentQuality;
                var previousFlag = ElementVisualBuilder.UseBatchedRealMeshRendering;
                try
                {
                    ElementVisualBuilder.CurrentQuality = ElementVisualBuilder.MeshQuality.Medium;
                    ElementVisualBuilder.UseBatchedRealMeshRendering = true;

                    // Clear cache first
                    ElementVisualBuilder.ClearMeshCache("test-warm");

                    // Render once to populate cache
                    var builder1 = new ElementVisualBuilder(readerRepository, origin, placementOffset);
                    builder1.ResetCounters();
                    builder1.BuildBatch(elements.Select(e => (e, (LiftStatus?)null, false)));

                    var misses1 = builder1.DiagMeshCacheMisses;
                    var hits1 = builder1.DiagMeshCacheHits;

                    // Render second time (warm cache)
                    var builder2 = new ElementVisualBuilder(readerRepository, origin, placementOffset);
                    builder2.ResetCounters();
                    builder2.BuildBatch(elements.Select(e => (e, (LiftStatus?)null, false)));

                    var misses2 = builder2.DiagMeshCacheMisses;
                    var hits2 = builder2.DiagMeshCacheHits;

                    // Misses in the second run should be 0 because all meshes are warm in the cache!
                    Assert.Equal(0, misses2);
                    Assert.True(hits2 > 0);
                }
                finally
                {
                    ElementVisualBuilder.CurrentQuality = previousQuality;
                    ElementVisualBuilder.UseBatchedRealMeshRendering = previousFlag;
                    readerRepository.ClearData();
                }
            });
        }

        #endregion
    }
}
