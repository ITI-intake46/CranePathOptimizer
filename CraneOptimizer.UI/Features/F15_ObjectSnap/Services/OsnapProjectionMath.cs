using CraneOptimizer.UI.Features.F15_ObjectSnap.Models;
using System;
using System.Windows;
using System.Windows.Media.Media3D;

namespace CraneOptimizer.UI.Features.F15_ObjectSnap.Services
{
    internal readonly record struct OsnapRay(Point3D Origin, Vector3D Direction);

    internal static class OsnapProjectionMath
    {
        public static bool TryCreateCursorRay(
            OsnapViewportSnapshot snapshot,
            out OsnapRay ray)
        {
            ray = default;
            if (!snapshot.IsValid ||
                !TryBuildCameraBasis(
                    snapshot.LookDirection,
                    snapshot.UpDirection,
                    out Vector3D right,
                    out Vector3D up,
                    out Vector3D forward))
            {
                return false;
            }

            double innerX = snapshot.CursorScreenPoint.X - snapshot.ScreenOffsetX;
            double innerY = snapshot.CursorScreenPoint.Y - snapshot.ScreenOffsetY;
            double normalizedX = (innerX / snapshot.ViewportWidth - 0.5) * 2.0;
            double normalizedY = (0.5 - innerY / snapshot.ViewportHeight) * 2.0;
            double aspect = snapshot.ViewportWidth / snapshot.ViewportHeight;
            if (!double.IsFinite(aspect) || aspect <= 0)
                return false;

            if (snapshot.CameraType == OsnapCameraType.Perspective)
            {
                double horizontalTangent =
                    Math.Tan(snapshot.FieldOfViewOrWidth * Math.PI / 360.0);
                if (!double.IsFinite(horizontalTangent) || horizontalTangent <= 0)
                    return false;

                double verticalTangent = horizontalTangent / aspect;
                Vector3D direction =
                    forward +
                    right * (normalizedX * horizontalTangent) +
                    up * (normalizedY * verticalTangent);
                if (!IsFinite(direction) ||
                    direction.LengthSquared < 1e-12)
                    return false;

                direction.Normalize();
                ray = new OsnapRay(snapshot.CameraPosition, direction);
                return true;
            }

            double halfWidth = snapshot.FieldOfViewOrWidth / 2.0;
            double halfHeight = halfWidth / aspect;
            Point3D origin =
                snapshot.CameraPosition +
                right * (normalizedX * halfWidth) +
                up * (normalizedY * halfHeight);
            if (!IsFinite(origin))
                return false;

            ray = new OsnapRay(origin, forward);
            return true;
        }

        public static bool TryProject(
            OsnapViewportSnapshot snapshot,
            Point3D world,
            out Point screen,
            out double depth)
        {
            screen = default;
            depth = default;
            if (!IsFinite(world) ||
                !snapshot.IsValid ||
                !TryBuildCameraBasis(
                    snapshot.LookDirection,
                    snapshot.UpDirection,
                    out Vector3D right,
                    out Vector3D up,
                    out Vector3D forward))
            {
                return false;
            }

            Vector3D relative = world - snapshot.CameraPosition;
            depth = Vector3D.DotProduct(relative, forward);
            if (!double.IsFinite(depth) || depth <= 1e-8)
                return false;

            double aspect = snapshot.ViewportWidth / snapshot.ViewportHeight;
            double normalizedX;
            double normalizedY;
            if (snapshot.CameraType == OsnapCameraType.Perspective)
            {
                double halfHorizontal =
                    depth * Math.Tan(snapshot.FieldOfViewOrWidth * Math.PI / 360.0);
                if (!double.IsFinite(halfHorizontal) || halfHorizontal <= 0)
                    return false;

                double halfVertical = halfHorizontal / aspect;
                normalizedX = Vector3D.DotProduct(relative, right) / halfHorizontal;
                normalizedY = Vector3D.DotProduct(relative, up) / halfVertical;
            }
            else
            {
                double halfHorizontal = snapshot.FieldOfViewOrWidth / 2.0;
                if (!double.IsFinite(halfHorizontal) || halfHorizontal <= 0)
                    return false;

                double halfVertical = halfHorizontal / aspect;
                normalizedX = Vector3D.DotProduct(relative, right) / halfHorizontal;
                normalizedY = Vector3D.DotProduct(relative, up) / halfVertical;
            }

            screen = new Point(
                snapshot.ScreenOffsetX +
                    snapshot.ViewportWidth * (0.5 + normalizedX * 0.5),
                snapshot.ScreenOffsetY +
                    snapshot.ViewportHeight * (0.5 - normalizedY * 0.5));
            return double.IsFinite(screen.X) && double.IsFinite(screen.Y);
        }

        public static double GetViewDepth(
            OsnapViewportSnapshot snapshot,
            Point3D world)
        {
            Vector3D forward = snapshot.LookDirection;
            if (forward.LengthSquared < 1e-12)
                return double.NaN;

            forward.Normalize();
            return Vector3D.DotProduct(world - snapshot.CameraPosition, forward);
        }

        public static double WorldRadiusAtDepth(
            OsnapViewportSnapshot snapshot,
            double depth,
            double radiusPixels)
        {
            if (!double.IsFinite(depth) ||
                !double.IsFinite(radiusPixels) ||
                radiusPixels < 0 ||
                !double.IsFinite(snapshot.ViewportWidth) ||
                snapshot.ViewportWidth <= 0 ||
                !double.IsFinite(snapshot.FieldOfViewOrWidth) ||
                snapshot.FieldOfViewOrWidth <= 0)
            {
                return double.NaN;
            }

            if (snapshot.CameraType == OsnapCameraType.Orthographic)
            {
                return radiusPixels *
                       snapshot.FieldOfViewOrWidth /
                       Math.Max(1.0, snapshot.ViewportWidth);
            }

            double safeDepth = Math.Max(depth, 1e-6);
            double visibleWidth =
                2.0 *
                safeDepth *
                Math.Tan(snapshot.FieldOfViewOrWidth * Math.PI / 360.0);
            return radiusPixels * visibleWidth / Math.Max(1.0, snapshot.ViewportWidth);
        }

        public static double PixelDistanceSquared(Point first, Point second)
        {
            double dx = first.X - second.X;
            double dy = first.Y - second.Y;
            return dx * dx + dy * dy;
        }

        public static double DistanceToScreenSegment(
            Point point,
            Point start,
            Point end)
        {
            double dx = end.X - start.X;
            double dy = end.Y - start.Y;
            double lengthSquared = dx * dx + dy * dy;
            if (lengthSquared < 1e-12)
                return Math.Sqrt(PixelDistanceSquared(point, start));

            double parameter = Math.Clamp(
                ((point.X - start.X) * dx + (point.Y - start.Y) * dy) /
                lengthSquared,
                0.0,
                1.0);
            var closest = new Point(
                start.X + parameter * dx,
                start.Y + parameter * dy);
            return Math.Sqrt(PixelDistanceSquared(point, closest));
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
            if (forward.LengthSquared < 1e-12 ||
                upDirection.LengthSquared < 1e-12)
            {
                return false;
            }

            forward.Normalize();
            Vector3D requestedUp = upDirection;
            requestedUp.Normalize();
            up = requestedUp -
                 forward * Vector3D.DotProduct(requestedUp, forward);
            if (up.LengthSquared < 1e-10)
                up = ChooseFallbackUp(forward);

            if (up.LengthSquared < 1e-12)
                return false;

            up.Normalize();
            right = Vector3D.CrossProduct(forward, up);
            if (right.LengthSquared < 1e-12)
                return false;

            right.Normalize();
            up = Vector3D.CrossProduct(right, forward);
            up.Normalize();
            return true;
        }

        private static Vector3D ChooseFallbackUp(Vector3D forward)
        {
            Vector3D axis = Math.Abs(forward.Z) < 0.8
                ? new Vector3D(0, 0, 1)
                : new Vector3D(0, 1, 0);
            Vector3D result =
                axis - forward * Vector3D.DotProduct(axis, forward);
            if (result.LengthSquared > 1e-12)
                result.Normalize();
            return result;
        }

        private static bool IsFinite(Point3D point) =>
            double.IsFinite(point.X) &&
            double.IsFinite(point.Y) &&
            double.IsFinite(point.Z);

        private static bool IsFinite(Vector3D vector) =>
            double.IsFinite(vector.X) &&
            double.IsFinite(vector.Y) &&
            double.IsFinite(vector.Z);
    }
}
