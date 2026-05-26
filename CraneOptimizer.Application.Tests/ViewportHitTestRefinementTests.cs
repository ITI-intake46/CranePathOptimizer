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
    }
}
