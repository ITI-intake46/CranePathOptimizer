using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Media.Media3D;
using CraneOptimizer.Domain.Schemas;
using CraneOptimizer.UI.Core.Interfaces;
using HelixToolkit.Wpf;

namespace CraneOptimizer.UI.Core.Services
{
    public class CameraService : ICameraService
    {
        private readonly IRenderingService _renderingService;

        private const double MinNearPlane = 1d;
        private const double FarPlanePaddingFactor = 1.4;

        public CameraService(IRenderingService renderingService)
        {
            _renderingService = renderingService;
        }

        public void ResetCamera()
        {
            var viewport = _renderingService.GetViewport();
            viewport?.CameraController?.ResetCamera();
        }

        public void ZoomExtents()
        {
            var viewport = _renderingService.GetViewport();
            if (viewport == null)
                return;

            viewport.UpdateLayout();
            viewport.ZoomExtents();
        }

        public void UpdateClipping(IReadOnlyList<PrecastElement>? elements)
        {
            var viewport = _renderingService.GetViewport();
            var camera = viewport?.Camera as PerspectiveCamera;
            
            if (camera == null)
                return;

            var source = elements;
            if (source == null || source.Count == 0)
            {
                camera.NearPlaneDistance = MinNearPlane;
                camera.FarPlaneDistance = 2_000_000;
                return;
            }

            var minX = source.Min(e => e.ElementCOG.X - ((e.BBoxLength ?? 0d) / 2d));
            var maxX = source.Max(e => e.ElementCOG.X + ((e.BBoxLength ?? 0d) / 2d));
            var minY = source.Min(e => e.ElementCOG.Y - ((e.BBoxWidth ?? 0d) / 2d));
            var maxY = source.Max(e => e.ElementCOG.Y + ((e.BBoxWidth ?? 0d) / 2d));
            var minZ = source.Min(e => e.ElementCOG.Z - ((e.BBoxHeight ?? 0d) / 2d));
            var maxZ = source.Max(e => e.ElementCOG.Z + ((e.BBoxHeight ?? 0d) / 2d));

            var center = new Point3D((minX + maxX) * 0.5, (minY + maxY) * 0.5, (minZ + maxZ) * 0.5);
            var dx = maxX - minX;
            var dy = maxY - minY;
            var dz = maxZ - minZ;
            var radius = Math.Max(1000d, Math.Sqrt(dx * dx + dy * dy + dz * dz) * 0.5);

            var cameraDistance = (camera.Position - center).Length;
            camera.NearPlaneDistance = Math.Max(MinNearPlane, Math.Min(radius * 0.02, cameraDistance * 0.1));
            camera.FarPlaneDistance = Math.Max(camera.NearPlaneDistance * 1000, (cameraDistance + radius) * FarPlanePaddingFactor);
        }
    }
}
