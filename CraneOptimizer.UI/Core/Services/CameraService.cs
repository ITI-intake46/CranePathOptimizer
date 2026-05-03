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
        private const double DefaultGridViewSize = 50_000d;

        public CameraService(IRenderingService renderingService)
        {
            _renderingService = renderingService;
        }

        public void ResetCamera()
        {
            ResetToDefaultView();
        }

        public void ResetToDefaultView()
        {
            var viewport = _renderingService.GetViewport();
            if (viewport == null)
                return;

            SetCameraView(
                center: new Point3D(0, 0, 0),
                sceneSize: DefaultGridViewSize,
                elevationFactor: 0.82,
                distanceFactor: 1.32);
            UpdateClipping(null);
        }

        public void ZoomExtents()
        {
            var viewport = _renderingService.GetViewport();
            if (viewport == null)
                return;

            viewport.UpdateLayout();
            viewport.ZoomExtents();
        }

        public void ZoomToElements(IReadOnlyList<PrecastElement> elements)
        {
            if (elements.Count == 0)
            {
                ResetToDefaultView();
                return;
            }

            var (center, size) = CalculateBounds(elements);
            SetCameraView(center, size, elevationFactor: 0.72, distanceFactor: 1.55);
            UpdateClipping(elements);
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

            var (center, size) = CalculateBounds(source);
            var radius = Math.Max(1000d, size * 0.5);

            var cameraDistance = (camera.Position - center).Length;
            camera.NearPlaneDistance = Math.Max(MinNearPlane, Math.Min(radius * 0.02, cameraDistance * 0.1));
            camera.FarPlaneDistance = Math.Max(camera.NearPlaneDistance * 1000, (cameraDistance + radius) * FarPlanePaddingFactor);
        }

        private void SetCameraView(Point3D center, double sceneSize, double elevationFactor, double distanceFactor)
        {
            var viewport = _renderingService.GetViewport();
            if (viewport == null)
                return;

            var distance = Math.Max(25_000d, sceneSize * distanceFactor);
            var elevation = Math.Max(12_000d, sceneSize * elevationFactor);
            var position = new Point3D(
                center.X + distance,
                center.Y - distance,
                center.Z + elevation);

            var camera = viewport.Camera as PerspectiveCamera;
            if (camera == null)
            {
                camera = new PerspectiveCamera();
                viewport.Camera = camera;
            }

            camera.Position = position;
            camera.LookDirection = center - position;
            camera.UpDirection = new Vector3D(0, 0, 1);
            camera.FieldOfView = 45;
            viewport.UpdateLayout();
        }

        private static (Point3D center, double size) CalculateBounds(IReadOnlyList<PrecastElement> source)
        {
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
            var size = Math.Max(1_000d, Math.Sqrt(dx * dx + dy * dy + dz * dz));
            return (center, size);
        }
    }
}
