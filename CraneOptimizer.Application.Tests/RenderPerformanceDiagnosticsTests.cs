using CraneOptimizer.Domain.Common;
using CraneOptimizer.Domain.Diagnostics;
using CraneOptimizer.Domain.Schemas;
using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Models;
using CraneOptimizer.UI.Core.Interfaces;
using CraneOptimizer.UI.Core.Services;
using CraneOptimizer.UI.Features.F2_IfcViewer;
using System.Threading;
using Xunit;

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
    }
}
