using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Windows.Media.Media3D;
using CraneOptimizer.Domain.Common;
using CraneOptimizer.Domain.Schemas;
using CraneOptimizer.UI.Core.Interfaces;
using CraneOptimizer.UI.Core.Services;
using HelixToolkit.Wpf;
using Xunit;

namespace CraneOptimizer.Application.Tests
{
    public class CameraServiceTests
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

        private class MockRenderingService : IRenderingService
        {
            private HelixViewport3D? _viewport;

            public void Initialize(HelixViewport3D viewport)
            {
                _viewport = viewport;
            }

            public void Render<T>(IEnumerable<T> items, IVisualBuilder<T> builder, string layerName) { }
            public void Clear() { }
            public void ClearLayer(string layerKey) { }
            public void SetGridSize(double widthMeters, double lengthMeters) { }
            public void SetGridSize(double widthMeters, double lengthMeters, double centerXMeters, double centerYMeters) { }
            public HelixViewport3D? GetViewport() => _viewport;
            public void ZoomExtents() { }
        }

        [Fact]
        public void ZoomToTopView_AppliesOrthographicProjection_EnforcesVerticalLookAndHorizontalUp()
        {
            RunOnSta(() =>
            {
                // Arrange
                var viewport = new HelixViewport3D();
                var mockRenderingService = new MockRenderingService();
                mockRenderingService.Initialize(viewport);

                var cameraService = new CameraService(mockRenderingService);

                var elements = new List<PrecastElement>
                {
                    new PrecastElement
                    {
                        ElementID = "El-1",
                        ElementCOG = new DomainPoint3D(10, 20, 5),
                        BBoxLength = 2.0,
                        BBoxWidth = 2.0,
                        BBoxHeight = 4.0
                    }
                };

                // Act
                cameraService.ZoomToTopView(elements, animate: false);

                // Assert
                Assert.True(viewport.Orthographic);
                Assert.True(cameraService.IsTopPlanViewActive);

                var camera = viewport.Camera as OrthographicCamera;
                Assert.NotNull(camera);

                // LookDirection must be exactly vertical along Z axis (0, 0, -distance)
                Assert.Equal(0.0, camera!.LookDirection.X, 5);
                Assert.Equal(0.0, camera.LookDirection.Y, 5);
                Assert.True(camera.LookDirection.Z < 0.0);

                // UpDirection must be stable, horizontal and roll/tilt free: (0, 1, 0)
                Assert.Equal(0.0, camera.UpDirection.X, 5);
                Assert.Equal(1.0, camera.UpDirection.Y, 5);
                Assert.Equal(0.0, camera.UpDirection.Z, 5);

                // Position must be target bounds center + distance above
                Assert.Equal(10.0, camera.Position.X, 5);
                Assert.Equal(20.0, camera.Position.Y, 5);
                Assert.True(camera.Position.Z > 5.0);
            });
        }

        [Fact]
        public void StandardViews_PreservePerspectiveAndVerticalUp()
        {
            RunOnSta(() =>
            {
                // Arrange
                var viewport = new HelixViewport3D();
                var mockRenderingService = new MockRenderingService();
                mockRenderingService.Initialize(viewport);

                var cameraService = new CameraService(mockRenderingService);

                var elements = new List<PrecastElement>
                {
                    new PrecastElement
                    {
                        ElementID = "El-2",
                        ElementCOG = new DomainPoint3D(0, 0, 0),
                        BBoxLength = 10,
                        BBoxWidth = 10,
                        BBoxHeight = 10
                    }
                };

                // Act - Set to Top View first
                cameraService.ZoomToTopView(elements, animate: false);
                Assert.True(viewport.Orthographic);
                Assert.True(cameraService.IsTopPlanViewActive);

                // Act - Switch to Front View
                cameraService.ZoomToFrontView(elements, animate: false);

                // Assert Front View changes
                Assert.False(viewport.Orthographic);
                Assert.False(cameraService.IsTopPlanViewActive);

                var camera = viewport.Camera as PerspectiveCamera;
                Assert.NotNull(camera);

                // Front view UpDirection should be vertical Z-up (0, 0, 1)
                Assert.Equal(0.0, camera!.UpDirection.X, 5);
                Assert.Equal(0.0, camera.UpDirection.Y, 5);
                Assert.Equal(1.0, camera.UpDirection.Z, 5);
            });
        }

        [Fact]
        public void ZoomToElements_WhenTopViewActive_KeepsTopViewOrthographic()
        {
            RunOnSta(() =>
            {
                // Arrange
                var viewport = new HelixViewport3D();
                var mockRenderingService = new MockRenderingService();
                mockRenderingService.Initialize(viewport);

                var cameraService = new CameraService(mockRenderingService);

                var elements = new List<PrecastElement>
                {
                    new PrecastElement
                    {
                        ElementID = "El-3",
                        ElementCOG = new DomainPoint3D(5, 5, 5),
                        BBoxLength = 10,
                        BBoxWidth = 10,
                        BBoxHeight = 10
                    }
                };

                // Act - Set to Top View
                cameraService.ZoomToTopView(elements, animate: false);
                Assert.True(viewport.Orthographic);

                // Act - Call ZoomToElements (simulate FitToModel or ZoomExtents interaction)
                cameraService.ZoomToElements(elements);

                // Assert - View remains orthographic and perfectly vertical
                Assert.True(viewport.Orthographic);
                Assert.True(cameraService.IsTopPlanViewActive);

                var camera = viewport.Camera as OrthographicCamera;
                Assert.NotNull(camera);
                Assert.Equal(0.0, camera!.LookDirection.X, 5);
                Assert.Equal(0.0, camera.LookDirection.Y, 5);
                Assert.Equal(0.0, camera.UpDirection.Z, 5);
            });
        }
    }
}
