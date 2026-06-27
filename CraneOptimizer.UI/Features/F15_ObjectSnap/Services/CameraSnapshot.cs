using HelixToolkit.Wpf;
using System;
using System.Windows.Media.Media3D;

namespace CraneOptimizer.UI.Features.F15_ObjectSnap.Services
{
    internal sealed class CameraSnapshot
    {
        public int GeometryVersion { get; }
        public char ProjectionType { get; } // 'P' for Perspective, 'O' for Orthographic, 'U' for Unknown
        public double ViewportWidth { get; }
        public double ViewportHeight { get; }
        public Point3D Position { get; }
        public Vector3D LookDirection { get; }
        public Vector3D UpDirection { get; }
        public double FovOrWidth { get; }

        public CameraSnapshot(HelixViewport3D viewport, int geometryVersion)
        {
            GeometryVersion = geometryVersion;
            
            // Viewport size actuals
            double width = viewport.Viewport.ActualWidth > 0 ? viewport.Viewport.ActualWidth : viewport.ActualWidth;
            double height = viewport.Viewport.ActualHeight > 0 ? viewport.Viewport.ActualHeight : viewport.ActualHeight;
            ViewportWidth = width;
            ViewportHeight = height;

            if (viewport.Camera is PerspectiveCamera pCam)
            {
                ProjectionType = 'P';
                Position = pCam.Position;
                LookDirection = pCam.LookDirection;
                UpDirection = pCam.UpDirection;
                FovOrWidth = pCam.FieldOfView;
            }
            else if (viewport.Camera is OrthographicCamera oCam)
            {
                ProjectionType = 'O';
                Position = oCam.Position;
                LookDirection = oCam.LookDirection;
                UpDirection = oCam.UpDirection;
                FovOrWidth = oCam.Width;
            }
            else
            {
                ProjectionType = 'U';
                Position = new Point3D();
                LookDirection = new Vector3D(0, 0, -1);
                UpDirection = new Vector3D(0, 1, 0);
                FovOrWidth = 0;
            }
        }

        public static bool IsThresholdExceeded(CameraSnapshot current, CameraSnapshot? cached)
        {
            if (cached == null)
                return true;

            // 1. GeometryVersion change
            if (current.GeometryVersion != cached.GeometryVersion)
                return true;

            // 2. ProjectionType change
            if (current.ProjectionType != cached.ProjectionType)
                return true;

            // 3. Viewport width/height changed by 1.0 pixel or more
            if (Math.Abs(current.ViewportWidth - cached.ViewportWidth) >= 1.0 ||
                Math.Abs(current.ViewportHeight - cached.ViewportHeight) >= 1.0)
            {
                return true;
            }

            // 4. FOV or Orthographic Width changed by 0.01 units or more
            if (Math.Abs(current.FovOrWidth - cached.FovOrWidth) >= 0.01)
            {
                return true;
            }

            // 5. Position movement translation changed by 0.20 model units or more
            if ((current.Position - cached.Position).Length >= 0.20)
            {
                return true;
            }

            // 6. LookDirection angular change of 1.0 degree or more
            if (IsAngleExceeded(current.LookDirection, cached.LookDirection, 1.0))
            {
                return true;
            }

            // 7. UpDirection angular change of 1.0 degree or more
            if (IsAngleExceeded(current.UpDirection, cached.UpDirection, 1.0))
            {
                return true;
            }

            return false;
        }

        private static bool IsAngleExceeded(Vector3D v1, Vector3D v2, double thresholdDegrees)
        {
            double len1 = v1.Length;
            double len2 = v2.Length;
            if (len1 < 1e-9 || len2 < 1e-9)
                return true; // Zero-length vector, treat as changed/exceeded

            double dot = Vector3D.DotProduct(v1, v2) / (len1 * len2);
            dot = Math.Clamp(dot, -1.0, 1.0);

            double angleRad = Math.Acos(dot);
            double angleDeg = angleRad * (180.0 / Math.PI);

            return angleDeg >= thresholdDegrees;
        }
    }
}
