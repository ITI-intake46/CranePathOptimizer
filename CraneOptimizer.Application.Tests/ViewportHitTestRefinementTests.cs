using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Media.Media3D;
using CraneOptimizer.Domain.Common;
using CraneOptimizer.Domain.Entities;
using CraneOptimizer.Domain.Interfaces;
using CraneOptimizer.UI.Core.Services;
using CraneOptimizer.UI.Features.F2_IfcViewer;
using CraneOptimizer.UI.Features.F15_ObjectSnap.Models;
using CraneOptimizer.UI.Features.F15_ObjectSnap.Services;
using HelixToolkit.Wpf;
using Xunit;
using CraneOptimizer.Domain.Schemas;
using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Models;

namespace CraneOptimizer.Application.Tests
{
    public class ViewportHitTestRefinementTests
    {
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

        [Fact]
        public void Test_RayTriangleIntersection_ValidHits()
        {
            RunOnSta(() =>
            {
                var method = typeof(ViewportHitTestService).GetMethod("RayTriangleIntersect",
                    System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
                
                Assert.NotNull(method);

                // Setup a triangle on XY plane
                var v0 = new Point3D(0, 0, 0);
                var v1 = new Point3D(1, 0, 0);
                var v2 = new Point3D(0, 1, 0);

                // Ray starting from above and pointing straight down
                var rayOrigin = new Point3D(0.25, 0.25, 5.0);
                var rayDir = new Vector3D(0, 0, -1);

                object[] args = new object[] { rayOrigin, rayDir, v0, v1, v2, 0.0 };
                bool hit = (bool)method.Invoke(null, args)!;
                double t = (double)args[5];

                Assert.True(hit);
                Assert.Equal(5.0, t, 3);

                // Ray pointing away
                rayDir = new Vector3D(0, 0, 1);
                args = new object[] { rayOrigin, rayDir, v0, v1, v2, 0.0 };
                hit = (bool)method.Invoke(null, args)!;
                Assert.False(hit);

                // Ray missing the triangle
                rayOrigin = new Point3D(2.0, 2.0, 5.0);
                rayDir = new Vector3D(0, 0, -1);
                args = new object[] { rayOrigin, rayDir, v0, v1, v2, 0.0 };
                hit = (bool)method.Invoke(null, args)!;
                Assert.False(hit);
            });
        }

        [Fact]
        public void Test_ObjectSnapDefaultOffAndManualToggle()
        {
            RunOnSta(() =>
            {
                var settings = new OsnapSettings();
                // Default value must be false
                Assert.False(settings.MasterEnabled);

                var service = new OsnapService(settings);
                bool eventFired = false;
                bool? lastEnabledState = null;

                service.MasterEnabledChanged += (enabled) =>
                {
                    eventFired = true;
                    lastEnabledState = enabled;
                };

                // Enable snap manually
                service.SetMasterEnabled(true);
                Assert.True(settings.MasterEnabled);
                Assert.True(eventFired);
                Assert.True(lastEnabledState);

                // Toggle back off
                eventFired = false;
                service.SetMasterEnabled(false);
                Assert.False(settings.MasterEnabled);
                Assert.True(eventFired);
                Assert.False(lastEnabledState);
            });
        }

        [Fact]
        public void Test_OsnapSettingsService_ForcesDisabledOnStartup()
        {
            var settingsService = new OsnapSettingsService();
            var settings = settingsService.Load();
            
            // Should always be disabled on startup/load
            Assert.False(settings.MasterEnabled);
        }

        [Fact]
        public void Test_MeshDerivedAabb_MatchesTransformedVertices()
        {
            RunOnSta(() =>
            {
                var el = new PrecastElement
                {
                    ElementID = "EL_TEST_01",
                    ElementCOG = new DomainPoint3D(10, 20, 30),
                    BBoxLength = 2,
                    BBoxWidth = 4,
                    BBoxHeight = 6,
                    IncludeInRender = true
                };

                var allItems = new List<(PrecastElement element, LiftStatus? status, bool isSelected)>
                {
                    (el, null, false)
                };

                // Create a mock mesh with some vertices
                var mesh = new MeshGeometry3D();
                mesh.Positions.Add(new Point3D(-1, -1, -1));
                mesh.Positions.Add(new Point3D(1, 1, 1));

                // Transform the mesh vertices
                var transform = new TranslateTransform3D(5, 5, 5); // translates vertices to (4,4,4) and (6,6,6)

                var elementMeshes = new Dictionary<string, List<(MeshGeometry3D Mesh, Matrix3D Transform)>>
                {
                    { "EL_TEST_01", new List<(MeshGeometry3D, Matrix3D)> { (mesh, transform.Value) } }
                };

                // Invoke PopulateSpatialHitTestRegistry via Reflection
                var method = typeof(ElementVisualBuilder).GetMethod("PopulateSpatialHitTestRegistry",
                    System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
                
                Assert.NotNull(method);
                
                // origin = (0,0,0)
                method.Invoke(null, new object[] { allItems, Guid.NewGuid(), new DomainPoint3D(0, 0, 0), elementMeshes });

                // Get spatial hit test elements
                var spatialElements = ElementVisualBuilder.GetSpatialHitTestElements();
                var registered = spatialElements.FirstOrDefault(x => x.ElementId == "EL_TEST_01");

                Assert.NotNull(registered);
                Assert.True(registered.UsedMeshDerivedBounds);
                
                // Expected min/max should be derived from vertices (translated by +5)
                // min = (-1+5, -1+5, -1+5) = (4, 4, 4)
                // max = (1+5, 1+5, 1+5) = (6, 6, 6)
                Assert.Equal(4.0, registered.Min.X, 3);
                Assert.Equal(4.0, registered.Min.Y, 3);
                Assert.Equal(4.0, registered.Min.Z, 3);
                Assert.Equal(6.0, registered.Max.X, 3);
                Assert.Equal(6.0, registered.Max.Y, 3);
                Assert.Equal(6.0, registered.Max.Z, 3);
            });
        }

        [Fact]
        public void Test_CogBasedAabbMismatch_CorrectedByMeshDerived()
        {
            RunOnSta(() =>
            {
                var el = new PrecastElement
                {
                    ElementID = "EL_TEST_02",
                    ElementCOG = new DomainPoint3D(100, 200, 300), // COG center is at (100,200,300)
                    BBoxLength = 1,
                    BBoxWidth = 1,
                    BBoxHeight = 1,
                    IncludeInRender = true
                };

                var allItems = new List<(PrecastElement element, LiftStatus? status, bool isSelected)>
                {
                    (el, null, false)
                };

                // Real mesh vertices are at (0, 0, 0)
                var mesh = new MeshGeometry3D();
                mesh.Positions.Add(new Point3D(-0.5, -0.5, -0.5));
                mesh.Positions.Add(new Point3D(0.5, 0.5, 0.5));

                var elementMeshes = new Dictionary<string, List<(MeshGeometry3D Mesh, Matrix3D Transform)>>
                {
                    { "EL_TEST_02", new List<(MeshGeometry3D, Matrix3D)> { (mesh, Matrix3D.Identity) } }
                };

                var method = typeof(ElementVisualBuilder).GetMethod("PopulateSpatialHitTestRegistry",
                    System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
                
                Assert.NotNull(method);
                method.Invoke(null, new object[] { allItems, Guid.NewGuid(), new DomainPoint3D(0, 0, 0), elementMeshes });

                var registered = ElementVisualBuilder.GetSpatialHitTestElements().FirstOrDefault(x => x.ElementId == "EL_TEST_02");

                Assert.NotNull(registered);
                Assert.True(registered.UsedMeshDerivedBounds);
                
                // Mismatch corrected (Mesh center = (0,0,0), COG center = (100,200,300))
                Assert.Equal(0.0, (registered.Min.X + registered.Max.X)/2.0, 3);
                Assert.Equal(100.0, (registered.CogBasedMin.X + registered.CogBasedMax.X)/2.0, 3);
            });
        }

        [Fact]
        public void Test_IncludeInRenderFalse_IsVisibleFalse()
        {
            RunOnSta(() =>
            {
                var el = new PrecastElement
                {
                    ElementID = "EL_TEST_03",
                    ElementCOG = new DomainPoint3D(0, 0, 0),
                    BBoxLength = 1,
                    BBoxWidth = 1,
                    BBoxHeight = 1,
                    IncludeInRender = false // Excluded from render
                };

                var allItems = new List<(PrecastElement element, LiftStatus? status, bool isSelected)>
                {
                    (el, null, false)
                };

                var method = typeof(ElementVisualBuilder).GetMethod("PopulateSpatialHitTestRegistry",
                    System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
                
                Assert.NotNull(method);
                method.Invoke(null, new object[] { allItems, Guid.NewGuid(), new DomainPoint3D(0, 0, 0), null });

                var registered = ElementVisualBuilder.GetSpatialHitTestElements().FirstOrDefault(x => x.ElementId == "EL_TEST_03");

                Assert.NotNull(registered);
                Assert.False(registered.IsVisible); // Must be invisible for spatial hit testing
            });
        }

        [Fact]
        public void Test_FallbackScoring_PrefersSmallerVolume()
        {
            RunOnSta(() =>
            {
                var smallEl = new PrecastElement
                {
                    ElementID = "SMALL_EL",
                    ElementCOG = new DomainPoint3D(0, 0, 0),
                    BBoxLength = 1,
                    BBoxWidth = 1,
                    BBoxHeight = 1,
                    IncludeInRender = true
                };

                var largeEl = new PrecastElement
                {
                    ElementID = "LARGE_EL",
                    ElementCOG = new DomainPoint3D(0, 0, 0),
                    BBoxLength = 10,
                    BBoxWidth = 10,
                    BBoxHeight = 10,
                    IncludeInRender = true
                };

                var smallCandidate = new ElementVisualBuilder.SpatialHitTestElement
                {
                    ElementId = "SMALL_EL",
                    Element = smallEl,
                    Min = new Point3D(-0.5, -0.5, -0.5),
                    Max = new Point3D(0.5, 0.5, 0.5)
                };

                var largeCandidate = new ElementVisualBuilder.SpatialHitTestElement
                {
                    ElementId = "LARGE_EL",
                    Element = largeEl,
                    Min = new Point3D(-5, -5, -5),
                    Max = new Point3D(5, 5, 5)
                };

                var method = typeof(ViewportHitTestService).GetMethod("ComputeFallbackScore",
                    System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
                
                Assert.NotNull(method);

                var rayOrigin = new Point3D(0, 0, 5);
                var rayDir = new Vector3D(0, 0, -1);
                double tNear = 4.5;

                // Invoke scoring
                double smallScore = (double)method.Invoke(null, new object[] { rayOrigin, rayDir, tNear, smallCandidate })!;
                double largeScore = (double)method.Invoke(null, new object[] { rayOrigin, rayDir, tNear, largeCandidate })!;

                // Lower score is better. Smaller volume must have lower score.
                Assert.True(smallScore < largeScore, $"Small score ({smallScore}) should be less than large score ({largeScore}) due to volume penalty.");
            });
        }

        [Fact]
        public void Test_OverlappingSlabColumn_PrefersColumnWhenMeshHit()
        {
            RunOnSta(() =>
            {
                // Triangle on column top (Z = 2.0)
                var colV0 = new Point3D(-0.5, -0.5, 2.0);
                var colV1 = new Point3D(0.5, -0.5, 2.0);
                var colV2 = new Point3D(0.0, 0.5, 2.0);

                // Triangle on slab top (Z = 1.75)
                var slabV0 = new Point3D(-5.0, -5.0, 1.75);
                var slabV1 = new Point3D(5.0, -5.0, 1.75);
                var slabV2 = new Point3D(0.0, 5.0, 1.75);

                var rayOrigin = new Point3D(0.0, 0.0, 5.0);
                var rayDir = new Vector3D(0, 0, -1);

                var method = typeof(ViewportHitTestService).GetMethod("RayTriangleIntersect",
                    System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
                Assert.NotNull(method);

                // Test intersection on column
                object[] colArgs = new object[] { rayOrigin, rayDir, colV0, colV1, colV2, 0.0 };
                bool colHit = (bool)method.Invoke(null, colArgs)!;
                double colT = (double)colArgs[5];

                // Test intersection on slab
                object[] slabArgs = new object[] { rayOrigin, rayDir, slabV0, slabV1, slabV2, 0.0 };
                bool slabHit = (bool)method.Invoke(null, slabArgs)!;
                double slabT = (double)slabArgs[5];

                Assert.True(colHit);
                Assert.True(slabHit);
                
                // Column is hit first (closer to ray origin Z = 5.0)
                Assert.Equal(3.0, colT, 3);
                Assert.Equal(3.25, slabT, 3);
                Assert.True(colT < slabT, "Column triangle hit distance should be smaller than slab hit distance.");
            });
        }
    }
}
