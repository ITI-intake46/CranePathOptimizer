using HelixToolkit.Wpf;
using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Media3D;
using WPF_IFC_test1.Models;

namespace WPF_IFC_test1.Utilities
{
    public class CraneDragHandler
    {
        private readonly HelixViewport3D _viewer;
        private readonly CraneManager _craneManager;
        private bool _isDragging;
        private bool _isRotating;
        private Point _lastMousePosition;

        // Cooldown: ignore drag attempts for a short time after a crane is added
        private DateTime _lastAddTime = DateTime.MinValue;
        private static readonly TimeSpan AddCooldown = TimeSpan.FromMilliseconds(600);

        // Small hit radius around the crane BASE — not the full reach
        private const double HitRadius = 8.0;

        public event Action<string> StatusChanged;

        public CraneDragHandler(HelixViewport3D viewer, CraneManager craneManager)
        {
            _viewer = viewer;
            _craneManager = craneManager;

            if (_viewer != null)
            {
                _viewer.MouseLeftButtonDown += OnMouseLeftButtonDown;
                _viewer.MouseMove += OnMouseMove;
                _viewer.MouseLeftButtonUp += OnMouseLeftButtonUp;
                _viewer.MouseRightButtonDown += OnMouseRightButtonDown;
                _viewer.MouseRightButtonUp += OnMouseRightButtonUp;
            }
        }

        /// <summary>
        /// Call this immediately after programmatically adding a crane so the
        /// drag handler ignores the next half-second of mouse events.
        /// </summary>
        public void NotifyCraneAdded()
        {
            _lastAddTime = DateTime.Now;
            _isDragging = false;
            _isRotating = false;
            Mouse.Capture(null);
        }

        private bool InCooldown => (DateTime.Now - _lastAddTime) < AddCooldown;

        private CraneParametersBase ActiveParams => _craneManager?.SelectedCrane?.Parameters;

        // ── Left button down → start drag ────────────────────────────────────

        private void OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (_craneManager == null || _craneManager.Cranes.Count == 0) return;
            if (InCooldown) return;

            var position = e.GetPosition(_viewer);
            var groundPoint = GetGroundPlaneIntersection(position);

            if (groundPoint.HasValue)
            {
                var clickedCrane = FindCraneAtPosition(groundPoint.Value);

                if (clickedCrane != null)
                {
                    _craneManager.SelectedCrane = clickedCrane;
                    _isDragging = true;
                    _lastMousePosition = position;
                    Mouse.Capture(_viewer);
                    StatusChanged?.Invoke($"🏗️ Dragging {clickedCrane.Name}...");
                    e.Handled = true;
                }
            }
        }

        // ── Mouse move → update position / rotation ───────────────────────────

        private void OnMouseMove(object sender, MouseEventArgs e)
        {
            if (ActiveParams == null) return;

            var position = e.GetPosition(_viewer);

            if (_isDragging && e.LeftButton == MouseButtonState.Pressed)
            {
                var groundPoint = GetGroundPlaneIntersection(position);
                if (groundPoint.HasValue)
                {
                    ActiveParams.PositionX = groundPoint.Value.X;
                    ActiveParams.PositionY = groundPoint.Value.Y;
                    StatusChanged?.Invoke(
                        $"🏗️ {_craneManager.SelectedCrane.Name}: " +
                        $"({groundPoint.Value.X:F1}, {groundPoint.Value.Y:F1})");
                }
                e.Handled = true;
            }
            else if (_isRotating && e.RightButton == MouseButtonState.Pressed)
            {
                double deltaX = position.X - _lastMousePosition.X;
                ActiveParams.SlewAngle += deltaX * 0.5;
                _lastMousePosition = position;
                StatusChanged?.Invoke(
                    $"🔄 {_craneManager.SelectedCrane.Name} Slew: {ActiveParams.SlewAngle:F1}°");
                e.Handled = true;
            }
            else
            {
                // Button was released outside the window — cancel any stuck drag
                if (_isDragging || _isRotating)
                    CancelDrag();
            }
        }

        // ── Left button up → stop drag ────────────────────────────────────────

        private void OnMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (_isDragging)
            {
                _isDragging = false;
                Mouse.Capture(null);

                if (_craneManager?.SelectedCrane != null)
                {
                    var p = ActiveParams;
                    StatusChanged?.Invoke(
                        $"✓ {_craneManager.SelectedCrane.Name} placed at " +
                        $"({p.PositionX:F1}, {p.PositionY:F1})");
                }
            }
        }

        // ── Right button down → start rotate ─────────────────────────────────

        private void OnMouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (_craneManager == null || _craneManager.Cranes.Count == 0) return;
            if (InCooldown) return;

            var position = e.GetPosition(_viewer);
            var groundPoint = GetGroundPlaneIntersection(position);

            if (groundPoint.HasValue)
            {
                var clickedCrane = FindCraneAtPosition(groundPoint.Value);

                if (clickedCrane != null)
                {
                    _craneManager.SelectedCrane = clickedCrane;
                    _isRotating = true;
                    _lastMousePosition = position;
                    Mouse.Capture(_viewer);
                    StatusChanged?.Invoke($"🔄 Rotating {clickedCrane.Name}...");
                    e.Handled = true;
                }
            }
        }

        // ── Right button up → stop rotate ────────────────────────────────────

        private void OnMouseRightButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (_isRotating)
            {
                _isRotating = false;
                Mouse.Capture(null);

                if (_craneManager?.SelectedCrane != null)
                    StatusChanged?.Invoke(
                        $"✓ {_craneManager.SelectedCrane.Name} Slew: {ActiveParams.SlewAngle:F1}°");
            }
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        private void CancelDrag()
        {
            _isDragging = false;
            _isRotating = false;
            Mouse.Capture(null);
        }

        /// <summary>
        /// Finds the crane whose BASE is closest to the click point,
        /// within a small fixed HitRadius (not the full swing radius).
        /// </summary>
        private CraneInstance FindCraneAtPosition(Point3D point)
        {
            CraneInstance closest = null;
            double minDistance = double.MaxValue;

            foreach (var crane in _craneManager.Cranes)
            {
                double dx = crane.Parameters.PositionX - point.X;
                double dy = crane.Parameters.PositionY - point.Y;
                double distance = Math.Sqrt(dx * dx + dy * dy);

                if (distance <= HitRadius && distance < minDistance)
                {
                    minDistance = distance;
                    closest = crane;
                }
            }

            return closest;
        }

        private Point3D? GetGroundPlaneIntersection(Point mousePosition)
        {
            try
            {
                if (_viewer.Camera is not ProjectionCamera camera) return null;

                double viewportWidth = _viewer.ActualWidth;
                double viewportHeight = _viewer.ActualHeight;
                if (viewportWidth <= 0 || viewportHeight <= 0) return null;

                double ndcX = (2.0 * mousePosition.X / viewportWidth) - 1.0;
                double ndcY = 1.0 - (2.0 * mousePosition.Y / viewportHeight);

                Point3D cameraPosition = camera.Position;
                Vector3D lookDirection = camera.LookDirection;
                Vector3D upDirection = camera.UpDirection;

                Vector3D rightDirection = Vector3D.CrossProduct(lookDirection, upDirection);
                rightDirection.Normalize();

                upDirection = Vector3D.CrossProduct(rightDirection, lookDirection);
                upDirection.Normalize();
                lookDirection.Normalize();

                double fovFactor = 1.0;
                if (camera is PerspectiveCamera perspCamera)
                    fovFactor = Math.Tan(perspCamera.FieldOfView * Math.PI / 360.0);

                double aspectRatio = viewportWidth / viewportHeight;

                Vector3D rayDirection = lookDirection
                    + rightDirection * ndcX * fovFactor * aspectRatio
                    + upDirection * ndcY * fovFactor;
                rayDirection.Normalize();

                if (Math.Abs(rayDirection.Z) < 1e-10) return null;

                double t = -cameraPosition.Z / rayDirection.Z;
                if (t < 0) return null;

                return new Point3D(
                    cameraPosition.X + t * rayDirection.X,
                    cameraPosition.Y + t * rayDirection.Y,
                    0);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"Error in GetGroundPlaneIntersection: {ex.Message}");
                return null;
            }
        }

        public void Detach()
        {
            try
            {
                if (_viewer != null)
                {
                    _viewer.MouseLeftButtonDown -= OnMouseLeftButtonDown;
                    _viewer.MouseMove -= OnMouseMove;
                    _viewer.MouseLeftButtonUp -= OnMouseLeftButtonUp;
                    _viewer.MouseRightButtonDown -= OnMouseRightButtonDown;
                    _viewer.MouseRightButtonUp -= OnMouseRightButtonUp;
                }
                CancelDrag();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in Detach: {ex.Message}");
            }
        }
    }
}