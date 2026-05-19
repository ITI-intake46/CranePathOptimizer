using HelixToolkit.Wpf;
using System;
using System.Windows;
using System.Windows.Media.Media3D;

namespace CraneOptimizer.UI.Features.F15_ObjectSnap.Services
{
    internal static class OsnapProjection
    {
        public static bool TryProject(HelixViewport3D viewport, Point3D world, out Point screen)
        {
            screen = default;
            if (viewport == null || viewport.Viewport == null || !IsFinite(world))
                return false;

            try
            {
                var inner = Viewport3DHelper.Point3DtoPoint2D(viewport.Viewport, world);
                if (IsFinite(inner))
                {
                    var helixPt = viewport.Viewport.TranslatePoint(inner, viewport);
                    if (IsFinite(helixPt))
                    {
                        screen = helixPt;
                        return true;
                    }
                }
            }
            catch
            {
                // Fall through to camera math. WPF projection can reject large
                // transformed DXF coordinates when the camera clip planes are tight.
            }

            return TryProjectFromCamera(viewport, world, out screen);
        }

        public static bool TryProjectFast(HelixViewport3D viewport, Point3D world, out Point screen)
            => viewport != null && viewport.Viewport != null && IsFinite(world) &&
               TryProjectFromCamera(viewport, world, out screen);

        public static bool TryGetCameraDepth(HelixViewport3D viewport, Point3D world, out double depth)
        {
            depth = default;
            if (viewport == null || !IsFinite(world))
                return false;

            ProjectionCamera? camera = viewport.Camera switch
            {
                PerspectiveCamera perspective => perspective,
                OrthographicCamera orthographic => orthographic,
                _ => null
            };

            if (camera == null ||
                !TryBuildCameraBasis(camera.LookDirection, camera.UpDirection, out _, out _, out var forward))
            {
                return false;
            }

            depth = Vector3D.DotProduct(world - camera.Position, forward);
            return double.IsFinite(depth);
        }

        public static bool TryUnprojectZ0(HelixViewport3D viewport, Point screenPt, out Point3D worldPt)
        {
            worldPt = default;
            try
            {
                var innerPt = viewport.TranslatePoint(screenPt, viewport.Viewport);
                if (!Viewport3DHelper.Point2DtoPoint3D(
                        viewport.Viewport, innerPt, out Point3D near, out Point3D far))
                    return false;

                var dir = far - near;
                if (Math.Abs(dir.Z) < 1e-9)
                    return false;

                double t = -near.Z / dir.Z;
                if (t < 0)
                    return false;

                worldPt = near + t * dir;
                return IsFinite(worldPt);
            }
            catch
            {
                return false;
            }
        }

        private static bool TryProjectFromCamera(HelixViewport3D viewport, Point3D world, out Point screen)
        {
            screen = default;

            double width = viewport.Viewport.ActualWidth > 0 ? viewport.Viewport.ActualWidth : viewport.ActualWidth;
            double height = viewport.Viewport.ActualHeight > 0 ? viewport.Viewport.ActualHeight : viewport.ActualHeight;
            if (width <= 0 || height <= 0)
                return false;

            double aspect = width / height;
            if (!double.IsFinite(aspect) || aspect <= 0)
                return false;

            Point inner;
            switch (viewport.Camera)
            {
                case PerspectiveCamera camera:
                    if (!TryProjectPerspective(camera, world, width, height, aspect, out inner))
                        return false;
                    break;

                case OrthographicCamera camera:
                    if (!TryProjectOrthographic(camera, world, width, height, aspect, out inner))
                        return false;
                    break;

                default:
                    return false;
            }

            if (!IsFinite(inner))
                return false;

            try
            {
                screen = viewport.Viewport.TranslatePoint(inner, viewport);
            }
            catch
            {
                screen = inner;
            }

            return IsFinite(screen);
        }

        private static bool TryProjectPerspective(
            PerspectiveCamera camera,
            Point3D world,
            double width,
            double height,
            double aspect,
            out Point inner)
        {
            inner = default;
            if (!TryBuildCameraBasis(camera.LookDirection, camera.UpDirection, out var right, out var up, out var forward))
                return false;

            var v = world - camera.Position;
            double z = Vector3D.DotProduct(v, forward);
            if (z <= 1e-6)
                return false;

            double halfHorizontal = z * Math.Tan(camera.FieldOfView * Math.PI / 360.0);
            if (!double.IsFinite(halfHorizontal) || halfHorizontal <= 0)
                return false;

            double halfVertical = halfHorizontal / aspect;
            double x = Vector3D.DotProduct(v, right) / halfHorizontal;
            double y = Vector3D.DotProduct(v, up) / halfVertical;

            inner = new Point(width * (0.5 + x * 0.5), height * (0.5 - y * 0.5));
            return true;
        }

        private static bool TryProjectOrthographic(
            OrthographicCamera camera,
            Point3D world,
            double width,
            double height,
            double aspect,
            out Point inner)
        {
            inner = default;
            if (!TryBuildCameraBasis(camera.LookDirection, camera.UpDirection, out var right, out var up, out _))
                return false;

            double halfHorizontal = camera.Width / 2.0;
            if (!double.IsFinite(halfHorizontal) || halfHorizontal <= 0)
                return false;

            double halfVertical = halfHorizontal / aspect;
            var v = world - camera.Position;
            double x = Vector3D.DotProduct(v, right) / halfHorizontal;
            double y = Vector3D.DotProduct(v, up) / halfVertical;

            inner = new Point(width * (0.5 + x * 0.5), height * (0.5 - y * 0.5));
            return true;
        }

        private static bool TryBuildCameraBasis(
            Vector3D lookDirection,
            Vector3D upDirection,
            out Vector3D right,
            out Vector3D up,
            out Vector3D forward)
        {
            right = default;
            up = default;
            forward = lookDirection;

            if (forward.LengthSquared < 1e-12 || upDirection.LengthSquared < 1e-12)
                return false;

            forward.Normalize();
            var requestedUp = upDirection;
            if (requestedUp.LengthSquared < 1e-12)
                requestedUp = ChooseFallbackUp(forward);
            else
                requestedUp.Normalize();

            up = RemoveForwardComponent(requestedUp, forward);
            if (up.LengthSquared < 1e-10)
                up = ChooseFallbackUp(forward);

            if (up.LengthSquared < 1e-12)
                return false;

            up.Normalize();

            right = Vector3D.CrossProduct(forward, up);
            if (right.LengthSquared < 1e-12)
            {
                up = ChooseFallbackUp(forward);
                right = Vector3D.CrossProduct(forward, up);
            }

            if (right.LengthSquared < 1e-12)
                return false;

            right.Normalize();
            up = Vector3D.CrossProduct(right, forward);
            if (up.LengthSquared < 1e-12)
                return false;

            up.Normalize();
            return true;
        }

        private static Vector3D RemoveForwardComponent(Vector3D vector, Vector3D forward)
        {
            double dot = Vector3D.DotProduct(vector, forward);
            return new Vector3D(
                vector.X - forward.X * dot,
                vector.Y - forward.Y * dot,
                vector.Z - forward.Z * dot);
        }

        private static Vector3D ChooseFallbackUp(Vector3D forward)
        {
            Vector3D[] axes =
            {
                new(0, 0, 1),
                new(0, 1, 0),
                new(1, 0, 0)
            };

            Vector3D best = default;
            double bestLength = -1.0;
            foreach (var axis in axes)
            {
                var candidate = RemoveForwardComponent(axis, forward);
                double length = candidate.LengthSquared;
                if (length <= bestLength)
                    continue;

                best = candidate;
                bestLength = length;
            }

            if (best.LengthSquared > 1e-12)
                best.Normalize();

            return best;
        }

        private static bool IsFinite(Point3D point)
            => double.IsFinite(point.X) && double.IsFinite(point.Y) && double.IsFinite(point.Z);

        private static bool IsFinite(Point point)
            => double.IsFinite(point.X) && double.IsFinite(point.Y);
    }
}
