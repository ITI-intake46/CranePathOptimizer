using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Media.Animation;
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
        private const double MaxNearPlane = 1_000d;
        private const double FarPlanePaddingFactor = 4.0;
        private const double MinFarPlane = 2_000_000d;
        private const double DefaultGridViewSize = 50_000d;
        private static readonly Duration CameraAnimationDuration = new(TimeSpan.FromMilliseconds(420));

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

        public void ZoomToFrontView(IReadOnlyList<PrecastElement> elements, bool animate = true)
        {
            var hasElements = elements.Count > 0;
            var (center, size) = hasElements
                ? CalculateBounds(elements)
                : (new Point3D(0, 0, 0), DefaultGridViewSize);

            SetFrontCameraView(center, size, animate);
            UpdateClipping(hasElements ? elements : null);
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
                camera.FarPlaneDistance = MinFarPlane;
                return;
            }

            var (center, size) = CalculateBounds(source);
            var radius = Math.Max(1000d, size * 0.5);

            var cameraDistance = (camera.Position - center).Length;
            var nearPlane = Math.Min(MaxNearPlane, Math.Min(radius * 0.005, cameraDistance * 0.01));
            camera.NearPlaneDistance = Math.Max(MinNearPlane, nearPlane);
            camera.FarPlaneDistance = Math.Max(
                MinFarPlane,
                Math.Max(camera.NearPlaneDistance * 10_000d, (cameraDistance + radius) * FarPlanePaddingFactor));
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

        private void SetFrontCameraView(Point3D center, double sceneSize, bool animate)
        {
            var viewport = _renderingService.GetViewport();
            if (viewport == null)
                return;

            var distance = Math.Max(25_000d, sceneSize * 1.55);
            var elevation = Math.Max(12_000d, sceneSize * 0.55);
            var position = new Point3D(
                center.X,
                center.Y - distance,
                center.Z + elevation);
            var lookDirection = center - position;
            var upDirection = new Vector3D(0, 0, 1);

            var camera = viewport.Camera as PerspectiveCamera;
            if (camera == null)
            {
                camera = new PerspectiveCamera();
                viewport.Camera = camera;
            }

            camera.FieldOfView = 45;
            if (animate)
                AnimateCamera(camera, position, lookDirection, upDirection);
            else
                ApplyCamera(camera, position, lookDirection, upDirection);

            viewport.UpdateLayout();
        }

        private static void AnimateCamera(
            PerspectiveCamera camera,
            Point3D position,
            Vector3D lookDirection,
            Vector3D upDirection)
        {
            var startPosition = camera.Position;
            var startLookDirection = camera.LookDirection;
            var startUpDirection = camera.UpDirection;
            var easing = new CubicEase { EasingMode = EasingMode.EaseInOut };
            var remainingAnimations = 3;

            camera.BeginAnimation(ProjectionCamera.PositionProperty, null);
            camera.BeginAnimation(ProjectionCamera.LookDirectionProperty, null);
            camera.BeginAnimation(ProjectionCamera.UpDirectionProperty, null);

            camera.Position = position;
            camera.LookDirection = lookDirection;
            camera.UpDirection = upDirection;

            void ReleaseAnimation(object? sender, EventArgs e)
            {
                remainingAnimations--;
                if (remainingAnimations > 0)
                    return;

                camera.BeginAnimation(ProjectionCamera.PositionProperty, null);
                camera.BeginAnimation(ProjectionCamera.LookDirectionProperty, null);
                camera.BeginAnimation(ProjectionCamera.UpDirectionProperty, null);
                camera.Position = position;
                camera.LookDirection = lookDirection;
                camera.UpDirection = upDirection;
            }

            var positionAnimation = new Point3DAnimation
            {
                From = startPosition,
                To = position,
                Duration = CameraAnimationDuration,
                EasingFunction = easing,
                FillBehavior = FillBehavior.Stop
            };
            var lookAnimation = new Vector3DAnimation
            {
                From = startLookDirection,
                To = lookDirection,
                Duration = CameraAnimationDuration,
                EasingFunction = easing,
                FillBehavior = FillBehavior.Stop
            };
            var upAnimation = new Vector3DAnimation
            {
                From = startUpDirection,
                To = upDirection,
                Duration = CameraAnimationDuration,
                EasingFunction = easing,
                FillBehavior = FillBehavior.Stop
            };

            positionAnimation.Completed += ReleaseAnimation;
            lookAnimation.Completed += ReleaseAnimation;
            upAnimation.Completed += ReleaseAnimation;

            camera.BeginAnimation(ProjectionCamera.PositionProperty, positionAnimation);
            camera.BeginAnimation(ProjectionCamera.LookDirectionProperty, lookAnimation);
            camera.BeginAnimation(ProjectionCamera.UpDirectionProperty, upAnimation);
        }

        private static void ApplyCamera(
            PerspectiveCamera camera,
            Point3D position,
            Vector3D lookDirection,
            Vector3D upDirection)
        {
            camera.BeginAnimation(ProjectionCamera.PositionProperty, null);
            camera.BeginAnimation(ProjectionCamera.LookDirectionProperty, null);
            camera.BeginAnimation(ProjectionCamera.UpDirectionProperty, null);
            camera.Position = position;
            camera.LookDirection = lookDirection;
            camera.UpDirection = upDirection;
        }

        private static (Point3D center, double size) CalculateBounds(IReadOnlyList<PrecastElement> source)
        {
            var minX = double.MaxValue;
            var minY = double.MaxValue;
            var minZ = double.MaxValue;
            var maxX = double.MinValue;
            var maxY = double.MinValue;
            var maxZ = double.MinValue;

            foreach (var element in source)
            {
                var cog = element.ElementCOG;
                if (!IsFinite(cog.X) || !IsFinite(cog.Y) || !IsFinite(cog.Z))
                    continue;

                var halfLength = GetHalfExtent(element.BBoxLength);
                var halfWidth = GetHalfExtent(element.BBoxWidth);
                var halfHeight = GetHalfExtent(element.BBoxHeight);

                minX = Math.Min(minX, cog.X - halfLength);
                maxX = Math.Max(maxX, cog.X + halfLength);
                minY = Math.Min(minY, cog.Y - halfWidth);
                maxY = Math.Max(maxY, cog.Y + halfWidth);
                minZ = Math.Min(minZ, cog.Z - halfHeight);
                maxZ = Math.Max(maxZ, cog.Z + halfHeight);
            }

            if (minX == double.MaxValue)
                return (new Point3D(0, 0, 0), DefaultGridViewSize);

            var center = new Point3D((minX + maxX) * 0.5, (minY + maxY) * 0.5, (minZ + maxZ) * 0.5);
            var dx = maxX - minX;
            var dy = maxY - minY;
            var dz = maxZ - minZ;
            var size = Math.Max(1_000d, Math.Sqrt(dx * dx + dy * dy + dz * dz));
            return (center, size);
        }

        private static double GetHalfExtent(double? value)
        {
            if (!value.HasValue || !IsFinite(value.Value) || value.Value <= 0)
                return 0d;

            return value.Value * 0.5;
        }

        private static bool IsFinite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
    }
}
