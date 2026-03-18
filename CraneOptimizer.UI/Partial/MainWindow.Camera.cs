using HelixToolkit.Wpf;
using System;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Media3D;

namespace WPF_IFC_test1
{
    public partial class MainWindow
    {
        #region Camera Initialization

        // Tracks the camera whose Changed event is currently subscribed
        private Camera _subscribedCamera;

        private void InitializeCamera()
        {
            var camera = new PerspectiveCamera
            {
                Position = new Point3D(50, 50, 50),
                LookDirection = new Vector3D(-50, -50, -50),
                UpDirection = new Vector3D(0, 0, 1),
                FieldOfView = 45,
                NearPlaneDistance = 0.001,
                FarPlaneDistance = 1_000_000
            };
            IfcViewer.Camera = camera;
            _subscribedCamera = camera;
            camera.Changed += OnCameraChanged;
        }

        /// <summary>
        /// Called by ZoneModuleController.OnCameraReplaced when EnterPlanView /
        /// ReturnTo3DView swaps the camera object. Unsubscribes from the old camera
        /// and subscribes to the new one so measure-line and grid updates keep working.
        /// </summary>
        internal void RewireCamera(Camera newCam)
        {
            if (_subscribedCamera != null)
                _subscribedCamera.Changed -= OnCameraChanged;
            _subscribedCamera = newCam;
            newCam.Changed += OnCameraChanged;
            _measureTool?.UpdateScreenPositions();
        }

        #endregion

        #region Camera Changed Handler

        private bool _cameraChanging = false;

        private void OnCameraChanged(object sender, EventArgs e)
        {
            if (_cameraChanging) return;
            _cameraChanging = true;
            try
            {
                if (IfcViewer.Camera is PerspectiveCamera cam)
                {
                    double dist = cam.LookDirection.Length;
                    if (dist <= 0) return;

                    double near = Math.Max(0.0001, dist * 0.0001);
                    double far = Math.Max(dist * 2000, 1_000_000);
                    if (Math.Abs(cam.NearPlaneDistance - near) / near > 0.1) cam.NearPlaneDistance = near;
                    if (Math.Abs(cam.FarPlaneDistance - far) / far > 0.1) cam.FarPlaneDistance = far;

                    _zoneController?.UpdateGridForCamera(cam.Position, dist);
                    _measureTool?.UpdateScreenPositions();
                    return;
                }

                if (IfcViewer.Camera is OrthographicCamera ortho)
                {
                    double width = ortho.Width;
                    if (width <= 0) return;
                    _zoneController?.UpdateGridForOrthographicCamera(ortho.Position, width);
                    _measureTool?.UpdateScreenPositions();
                }
            }
            finally
            {
                _cameraChanging = false;
            }
        }

        #endregion

        #region Mouse Wheel Zoom

        private void IfcViewer_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            e.Handled = true;

            if (IfcViewer.Camera is OrthographicCamera ortho)
            {
                int ticks = e.Delta / 120;
                double factor = Math.Pow(0.88, ticks);
                double newWidth = Math.Clamp(ortho.Width * factor, 1.0, 3000.0);

                Point screenPt = e.GetPosition(IfcViewer);
                double relX = (screenPt.X / IfcViewer.ActualWidth) - 0.5;
                double relY = (screenPt.Y / IfcViewer.ActualHeight) - 0.5;
                double aspect = IfcViewer.ActualWidth / Math.Max(1, IfcViewer.ActualHeight);
                double deltaWidth = ortho.Width - newWidth;

                ortho.Position = new Point3D(
                    ortho.Position.X + relX * deltaWidth * aspect,
                    ortho.Position.Y - relY * deltaWidth,
                    ortho.Position.Z);
                ortho.Width = newWidth;
                return;
            }

            if (IfcViewer.Camera is not PerspectiveCamera cam) return;

            double dist = cam.LookDirection.Length;
            if (dist <= 0) return;

            int ticks3d = e.Delta / 120;
            double newDist = dist * Math.Pow(0.9, ticks3d);
            newDist = Math.Clamp(newDist, 0.5, 2000.0);

            double moveDelta = dist - newDist;
            Vector3D moveDir = GetMouseRayNormalized(e.GetPosition(IfcViewer), cam);

            cam.Position += moveDir * moveDelta;
            var lookNorm = cam.LookDirection;
            lookNorm.Normalize();
            cam.LookDirection = lookNorm * newDist;
        }

        private Vector3D GetMouseRayNormalized(Point screenPt, PerspectiveCamera cam)
        {
            try
            {
                if (Viewport3DHelper.Point2DtoPoint3D(
                        IfcViewer.Viewport, screenPt, out Point3D near, out Point3D far))
                {
                    var dir = far - near;
                    dir.Normalize();
                    return dir;
                }
            }
            catch { }

            var ld = cam.LookDirection;
            ld.Normalize();
            return ld;
        }

        #endregion

        #region Reset View

        private void ResetCamera_Click(object sender, RoutedEventArgs e) => ResetToFitAll();

        public void ResetToFitAll()
        {
            // Plan View — stay in plan view, just re-frame on model
            if (IfcViewer.Camera is OrthographicCamera ortho)
            {
                Rect3D box = ComputeSceneBoundingBox();
                double cx = box.X + box.SizeX / 2.0;
                double cy = box.Y + box.SizeY / 2.0;
                double cz = box.Z + box.SizeZ / 2.0;

                // Use the larger of X/Y span with 40% margin
                double span = Math.Max(box.SizeX, box.SizeY);
                if (span < 1.0) span = 100.0;

                double elevation = cz + Math.Max(span * 2, 500.0);
                ortho.Position = new Point3D(cx, cy, elevation);
                ortho.LookDirection = new Vector3D(0, 0, -1);
                ortho.UpDirection = new Vector3D(0, 1, 0);
                ortho.Width = span * 1.4;

                StatusText.Text = "✓ Fit All (Plan View)";
                return;
            }

            // 3D View
            if (IfcViewer.Camera is not PerspectiveCamera) return;

            Rect3D aabb = ComputeSceneBoundingBox();
            var center = new Point3D(
                aabb.X + aabb.SizeX / 2.0,
                aabb.Y + aabb.SizeY / 2.0,
                aabb.Z + aabb.SizeZ / 2.0);

            double diag = Math.Sqrt(
                aabb.SizeX * aabb.SizeX +
                aabb.SizeY * aabb.SizeY +
                aabb.SizeZ * aabb.SizeZ);
            if (diag < 1.0) diag = 100.0;

            // FIX: 1.8 multiplier (was 0.6 = camera inside building)
            double camDist = Math.Clamp(diag * 1.8, 10.0, 5000.0);

            var offset = new Vector3D(0, -camDist * 0.8, camDist * 0.6);
            offset.Normalize();
            offset *= camDist;
            var targetPos = center + offset;
            var targetLook = center - targetPos;
            var targetUp = new Vector3D(0, 0, 1);

            AnimateCameraTo(targetPos, targetLook, targetUp, fov: 50, durationMs: 300);
            StatusText.Text = "✓ Fit All";
        }

        public void ResetToPlanView()
        {
            Rect3D aabb = ComputeSceneBoundingBox();
            var center = new Point3D(
                aabb.X + aabb.SizeX / 2.0,
                aabb.Y + aabb.SizeY / 2.0,
                aabb.Z + aabb.SizeZ / 2.0);

            double diag = Math.Sqrt(
                aabb.SizeX * aabb.SizeX +
                aabb.SizeY * aabb.SizeY +
                aabb.SizeZ * aabb.SizeZ);
            if (diag < 1.0) diag = 100.0;

            var targetPos = new Point3D(center.X, center.Y, center.Z + diag * 0.9);
            var targetLook = new Vector3D(0, 0, -(diag * 0.9));
            var targetUp = new Vector3D(0, 1, 0);

            AnimateCameraTo(targetPos, targetLook, targetUp, fov: 45, durationMs: 300);
            StatusText.Text = "✓ Plan View";
        }

        #endregion

        #region Scene Bounding Box

        /// <summary>
        /// Fits the camera to only the loaded IFC model geometry, ignoring zones and cranes.
        /// Called after IFC load so that pre-existing large zones don't shrink the building view.
        /// </summary>
        public void FitToIFCModel()
        {
            double minX = double.MaxValue, minY = double.MaxValue, minZ = double.MaxValue;
            double maxX = double.MinValue, maxY = double.MinValue, maxZ = double.MinValue;
            bool hasAny = false;

            void Expand(double x, double y, double z)
            {
                if (x < minX) minX = x; if (x > maxX) maxX = x;
                if (y < minY) minY = y; if (y > maxY) maxY = y;
                if (z < minZ) minZ = z; if (z > maxZ) maxZ = z;
                hasAny = true;
            }

            foreach (var visual in _loadedIfcModels)
            {
                IEnumerable<Point3D> pts = null;
                if (visual.Content is Model3DGroup grp)
                    pts = grp.Children
                             .OfType<GeometryModel3D>()
                             .Where(g => g.Geometry is MeshGeometry3D)
                             .SelectMany(g => ((MeshGeometry3D)g.Geometry).Positions);
                else if (visual.Content is GeometryModel3D gm && gm.Geometry is MeshGeometry3D m)
                    pts = m.Positions;

                if (pts == null) continue;
                foreach (var pt in pts) Expand(pt.X, pt.Y, pt.Z);
            }

            if (!hasAny)
            {
                // No IFC geometry — fall back to full scene fit
                ResetToFitAll();
                return;
            }

            var center = new Point3D(
                (minX + maxX) / 2.0,
                (minY + maxY) / 2.0,
                (minZ + maxZ) / 2.0);

            double diag = Math.Sqrt(
                Math.Pow(maxX - minX, 2) +
                Math.Pow(maxY - minY, 2) +
                Math.Pow(maxZ - minZ, 2));
            if (diag < 1.0) diag = 50.0;

            double camDist = Math.Clamp(diag * 1.8, 10.0, 5000.0);
            var offset = new Vector3D(0, -camDist * 0.8, camDist * 0.6);
            offset.Normalize();
            offset *= camDist;
            var targetPos  = center + offset;
            var targetLook = center - targetPos;
            var targetUp   = new Vector3D(0, 0, 1);

            AnimateCameraTo(targetPos, targetLook, targetUp, fov: 50, durationMs: 400);
            StatusText.Text = $"✓ Fit to Model  ({_loadedIfcModels.Count} visuals)";
        }

        private Rect3D ComputeSceneBoundingBox()
        {
            double minX = double.MaxValue, minY = double.MaxValue, minZ = double.MaxValue;
            double maxX = double.MinValue, maxY = double.MinValue, maxZ = double.MinValue;
            bool hasAny = false;

            void Expand(double x, double y, double z)
            {
                if (x < minX) minX = x; if (x > maxX) maxX = x;
                if (y < minY) minY = y; if (y > maxY) maxY = y;
                if (z < minZ) minZ = z; if (z > maxZ) maxZ = z;
                hasAny = true;
            }

            foreach (var visual in _loadedIfcModels)
            {
                IEnumerable<Point3D> pts = null;
                if (visual.Content is Model3DGroup grp)
                    pts = grp.Children
                             .OfType<GeometryModel3D>()
                             .Where(g => g.Geometry is MeshGeometry3D)
                             .SelectMany(g => ((MeshGeometry3D)g.Geometry).Positions);
                else if (visual.Content is GeometryModel3D gm && gm.Geometry is MeshGeometry3D m)
                    pts = m.Positions;

                if (pts == null) continue;
                foreach (var pt in pts) Expand(pt.X, pt.Y, pt.Z);
            }

            if (_craneManager != null)
            {
                foreach (var crane in _craneManager.Cranes)
                {
                    var p = crane.Parameters;
                    double r = p.HookRadius;
                    double h = p.HookHeight;
                    Expand(p.PositionX - r, p.PositionY - r, p.PositionZ);
                    Expand(p.PositionX + r, p.PositionY + r, p.PositionZ + h);
                }
            }

            if (_zoneController?.Manager?.Zones != null)
            {
                foreach (var zone in _zoneController.Manager.Zones)
                    foreach (var v in zone.Vertices)
                        Expand(v.X, v.Y, v.Z);
            }

            if (!hasAny)
                return new Rect3D(-50, -50, 0, 100, 100, 1);

            double pad = Math.Max((maxX - minX) * 0.05, (maxY - minY) * 0.05);
            return new Rect3D(
                minX - pad, minY - pad, minZ,
                (maxX - minX) + pad * 2,
                (maxY - minY) + pad * 2,
                Math.Max(maxZ - minZ, 1));
        }

        #endregion

        #region Camera Animation

        private void AnimateCameraTo(
            Point3D targetPos, Vector3D targetLook, Vector3D targetUp,
            double fov = 45, int durationMs = 300)
        {
            if (IfcViewer.Camera is not PerspectiveCamera cam) return;

            _camAnimTimer?.Stop();
            _camAnimTimer = null;

            Point3D startPos = cam.Position;
            Vector3D startLook = cam.LookDirection;
            Vector3D startUp = cam.UpDirection;
            double startFov = cam.FieldOfView;
            double elapsed = 0;

            _camAnimTimer = new System.Windows.Threading.DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(16)
            };

            _camAnimTimer.Tick += (s, e) =>
            {
                elapsed += 16;
                double raw = Math.Min(elapsed / durationMs, 1.0);
                double ease = EaseInOut(raw);

                cam.Position = LerpPt(startPos, targetPos, ease);
                cam.LookDirection = LerpVec(startLook, targetLook, ease);
                cam.UpDirection = LerpVec(startUp, targetUp, ease);
                cam.FieldOfView = startFov + (fov - startFov) * ease;

                if (raw >= 1.0)
                {
                    _camAnimTimer.Stop();
                    _camAnimTimer = null;
                    SaveCurrentCameraState();
                }
            };

            _camAnimTimer.Start();
        }

        private static double EaseInOut(double t)
            => t < 0.5 ? 2 * t * t : 1 - Math.Pow(-2 * t + 2, 2) / 2;

        private static Point3D LerpPt(Point3D a, Point3D b, double t)
            => new(a.X + (b.X - a.X) * t, a.Y + (b.Y - a.Y) * t, a.Z + (b.Z - a.Z) * t);

        private static Vector3D LerpVec(Vector3D a, Vector3D b, double t)
            => new(a.X + (b.X - a.X) * t, a.Y + (b.Y - a.Y) * t, a.Z + (b.Z - a.Z) * t);

        #endregion

        #region Camera State Management

        private void SaveCurrentCameraState()
        {
            if (IfcViewer.Camera is PerspectiveCamera cam)
            {
                _savedCamPosition = cam.Position;
                _savedCamLookDir = cam.LookDirection;
                _savedCamUpDir = cam.UpDirection;
                _savedCamFov = cam.FieldOfView;
                _camSaved = true;
            }
        }

        private void EnterPlanView() => _zoneController?.EnterPlanView();
        private void ReturnTo3DView() => _zoneController?.ReturnTo3DView();

        private void ToggleCOGMarkers()
        {
            _cogMarkersVisible = !_cogMarkersVisible;
            foreach (var sphere in _cogVisuals.Values)
                sphere.Visible = _cogMarkersVisible;
            StatusText.Text = _cogMarkersVisible ? "COG markers ON" : "COG markers OFF";
        }

        #endregion
    }
}