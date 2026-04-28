using CraneOptimizer.Domain.Common;
using CraneOptimizer.Domain.Schemas;
using CraneOptimizer.UI.Core.Interfaces;
using CraneOptimizer.UI.Features.F3_CraneSetup.ViewModels;
using CraneOptimizer.UI.Features.F7_Zones;
using CraneOptimizer.UI.Shell.ViewModels;
using HelixToolkit.Wpf;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Media3D;

namespace CraneOptimizer.UI.Shell.Views
{
    public partial class MainWindow : Window
    {
        private readonly IRenderingService _renderingService;
        private readonly ISceneService _sceneService;
        private CraneSetupViewModel? _craneSetup;
        // ← NEW

        public MainWindow(
            MainViewModel vm,
            IRenderingService renderingService,
            ISceneService sceneService,
            CraneSetupViewModel craneSetup)
        {
            InitializeComponent();

            _renderingService = renderingService;
            _sceneService = sceneService;
            _craneSetup = craneSetup;                // ← NEW


            DataContext = vm;

            _renderingService.Initialize(IfcViewer);

            IfcViewer.MouseLeftButtonDown += Viewport_MouseDown;
            IfcViewer.MouseMove += Viewport_MouseMove;
            IfcViewer.MouseLeftButtonUp += Viewport_MouseUp;

            #region DXF
            // Wire the DXF uploader to the same Helix viewport.
            // Mirrors the IRenderingService pattern — must be done after InitializeComponent.
            vm.Dxf.Initialize(IfcViewer);
            #endregion

            #region Zones Interaction
            _sceneService.ZoneAdded += OnZoneAdded;
            _sceneService.ZoneSelected += OnZoneSelected;
            _sceneService.ZoneRemoved += OnZoneChanged;
            _sceneService.ZoneUpdated += OnZoneChanged;
            _sceneService.ZonesCleared += OnZonesCleared;
            #endregion
        }

        private MainViewModel? VM => DataContext as MainViewModel;

        #region Zones Interaction

        private void OnZoneAdded(GreenAreaZone zone) => RenderZones();
        private void OnZoneSelected(GreenAreaZone? zone) => RenderZones();
        private void OnZoneChanged(GreenAreaZone zone) => RenderZones();

        private void OnZonesCleared()
        {
            RenderZones();
        }

        private void RenderZones()
        {
            _renderingService.Render(
                _sceneService.GetZones(),
                new ZoneVisualBuilder(_sceneService),
                "Zones"
            );
        }

        private void HandleZoneSelection(MouseButtonEventArgs e)
        {
            var viewport = IfcViewer;
            var hits = viewport.Viewport.FindHits(e.GetPosition(viewport));

            if (hits == null || hits.Count == 0)
            {
                _sceneService.SetSelectedZone(null);
                return;
            }

            foreach (var hit in hits)
            {
                var visual = hit.Visual;

                if (visual is ModelVisual3D mv &&
                    mv.GetValue(FrameworkElement.TagProperty) is GreenAreaZone zone)
                {
                    _sceneService.SetSelectedZone(zone);
                    return;
                }
            }

            _sceneService.SetSelectedZone(null);
        }

        #endregion

        protected override void OnClosed(EventArgs e)
        {
            #region Zones Interaction
            _sceneService.ZoneAdded -= OnZoneAdded;
            _sceneService.ZoneSelected -= OnZoneSelected;
            _sceneService.ZoneRemoved -= OnZoneChanged;
            _sceneService.ZoneUpdated -= OnZoneChanged;
            _sceneService.ZonesCleared -= OnZonesCleared;
            #endregion

            base.OnClosed(e);
        }

        #region Viewport Interaction

        private void IfcViewer_MouseMove(object sender, MouseEventArgs e)
        {
            var viewport = sender as HelixViewport3D;
            if (viewport == null) return;

            var vm = VM?.Drawing;

            if (vm == null || !vm.IsDrawing) return;

            var screen = e.GetPosition(viewport);
            if (!TryGetWorldPoint(viewport, screen, out var world))
                return;

            vm.SetHoverPoint(new DomainPoint3D(world.X, world.Y, world.Z));
        }

        private void IfcViewer_MouseDown(object sender, MouseButtonEventArgs e)
        {
            var viewport = sender as HelixViewport3D;
            if (viewport == null) return;

            if (VM?.IfcViewer.IsPlacementMode == true)
            {
                if (e.ChangedButton != MouseButton.Left)
                    return;

                var placementScreen = e.GetPosition(viewport);
                if (!TryGetPlacementPoint(viewport, placementScreen, out var placementPoint))
                    return;

                if (VM.IfcViewer.PlacePendingIfc(placementPoint))
                {
                    viewport.UpdateLayout();
                    viewport.ZoomExtents();
                    e.Handled = true;
                }

                return;
            }

            if (VM?.IsPickingOrigin == true)
            {
                if (e.ChangedButton != MouseButton.Left)
                    return;

                var originScreen = e.GetPosition(viewport);
                if (!TryGetWorldPoint(viewport, originScreen, out var origin))
                    return;

                VM.SetOrigin(new DomainPoint3D(origin.X, origin.Y, origin.Z));
                viewport.ZoomExtents();
                e.Handled = true;
                return;
            }

            var vm = VM?.Drawing;

            if (vm != null && !vm.IsDrawing)
            {
                HandleZoneSelection(e);
                return;
            }

            if (vm == null) return;

            if (e.ChangedButton == MouseButton.Right)
            {
                vm.Finish();
                e.Handled = true;
                return;
            }

            if (e.ChangedButton != MouseButton.Left)
                return;

            var screen = e.GetPosition(viewport);
            if (!TryGetWorldPoint(viewport, screen, out var world))
                return;

            vm.AddPoint(new DomainPoint3D(world.X, world.Y, world.Z));
            e.Handled = true;
        }

        #endregion

        #region Window Chrome + UI Events (required by XAML)

        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            DragMove();
        }

        private void MinimizeWindow_Click(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState.Minimized;
        }

        private void MaximizeWindow_Click(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState == WindowState.Maximized
                ? WindowState.Normal
                : WindowState.Maximized;
        }

        private void CloseWindow_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void BackToWelcome_Click(object sender, RoutedEventArgs e) { }
        private void SaveProject_Click(object sender, RoutedEventArgs e) { }
        private void UndoBtn_Click(object sender, RoutedEventArgs e) { }
        private void RedoBtn_Click(object sender, RoutedEventArgs e) { }
        private void ClearHistory_Click(object sender, RoutedEventArgs e) { }
        private void BtnDimensionTool_Click(object sender, RoutedEventArgs e) { }
        private void ElementItem_Click(object sender, MouseButtonEventArgs e) { }

        private void ResetCamera_Click(object sender, RoutedEventArgs e)
        {
            IfcViewer?.CameraController?.ResetCamera();
        }

        private void IfcViewer_PreviewMouseWheel(object sender, MouseWheelEventArgs e) { }

        private void RenderQualityDropBtn_Click(object sender, RoutedEventArgs e)
        {
            RenderQualityPopup.IsOpen = true;
        }

        private void RenderQuality_Changed(object sender, RoutedEventArgs e) { }

        private void OsnapDropBtn_Click(object sender, RoutedEventArgs e)
        {
            OsnapPopup.IsOpen = true;
        }

        private void StatusOsnapMaster_Click(object sender, RoutedEventArgs e) { }
        private void StatusOsnap_Click(object sender, RoutedEventArgs e) { }

        private void ShowCommandPanel_Click(object sender, RoutedEventArgs e) { }
        private void ShowLogsPanel_Click(object sender, RoutedEventArgs e) { }

        #endregion

        #region Helpers

        #region Dragging the Crane Visual   
        private Point3D GetWorldPoint(HelixViewport3D viewport, System.Windows.Point screenPt)
        private bool TryGetWorldPoint(
            HelixViewport3D viewport,
            System.Windows.Point screenPt,
            out Point3D worldPoint)
        {
            worldPoint = new Point3D();

            try
            {
                var hit = viewport.FindNearestPoint(screenPt);
                if (hit.HasValue)
                {
                    worldPoint = hit.Value;
                    return true;
                }
            }
            catch { }

            try
            {
                bool ok = Viewport3DHelper.Point2DtoPoint3D(
                    viewport.Viewport,
                    screenPt,
                    out Point3D near,
                    out Point3D far);

                if (ok)
                {
                    var dir = far - near;

                    if (System.Math.Abs(dir.Z) > 1e-9)
                    {
                        double t = -near.Z / dir.Z;
                        if (t > 0)
                        {
                            worldPoint = near + t * dir;
                            return true;
                        }
                    }
                }
            }
            catch { }

            return false;
        }

        private bool TryGetPlacementPoint(
            HelixViewport3D viewport,
            System.Windows.Point screenPt,
            out Point3D worldPoint)
        {
            worldPoint = new Point3D();

            try
            {
                var hit = viewport.Viewport.FindHits(screenPt).FirstOrDefault();
                if (hit != null)
                    return TryGetWorldPoint(viewport, screenPt, out worldPoint);
            }
            catch { }

            return TryGetWorldPoint(viewport, screenPt, out worldPoint);
        }
        private void Viewport_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (_craneSetup?.SelectedCrane == null) return;

            // HelixTool's FindHits() performs a 3D ray-cast from the mouse position
            // and returns all 3D objects under the cursor.
            var mousePos = e.GetPosition(IfcViewer);
            var hits = Viewport3DHelper.FindHits(IfcViewer.Viewport, mousePos);

            // Check if any hit belongs to the crane layer
            if (hits != null && hits.Count > 0)
            {
                // The crane visual was clicked → begin drag
                _craneSetup.BeginDrag();
                IfcViewer.CaptureMouse(); // keep receiving events even if mouse leaves viewport
                e.Handled = true;         // prevent HelixTool's default camera rotation
            }
        }
        private void Viewport_MouseMove(object sender, MouseEventArgs e)
        {
            if (_craneSetup == null || !_craneSetup.IsDraggingActive) return;

            var worldPoint = ScreenToGroundPlane(e.GetPosition(IfcViewer));
            if (worldPoint == null) return;

            // Convert WPF Point3D to your domain type
            var domainPoint = new DomainPoint3D(worldPoint.Value.X, worldPoint.Value.Y, 0);

            // Update drag (includes zone constraint check)
            var zones = VM?.Scene.GetZones() ?? new List<GreenAreaZone>();
            _craneSetup.UpdateDrag(domainPoint, zones);

            // Show live coordinates in status bar
            VM?.SetCoordinates(domainPoint.X, domainPoint.Y);
            e.Handled = true;
        }

        // ─────────────────────────────────────────────────────────────────────
        // MOUSE UP — finish drag
        // ─────────────────────────────────────────────────────────────────────
        private void Viewport_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (_craneSetup == null || !_craneSetup.IsDraggingActive) return;

            var zones = VM?.Scene.GetZones() ?? new List<GreenAreaZone>();
            _craneSetup.EndDrag(zones);

            IfcViewer.ReleaseMouseCapture();
            e.Handled = true;
        }

        // ─────────────────────────────────────────────────────────────────────
        // COORDINATE CONVERSION: Screen pixel → 3D ground plane point
        // ─────────────────────────────────────────────────────────────────────
        /// <summary>
        /// Converts a 2D mouse position (screen pixels) into a 3D point on the
        /// ground plane (Z = 0) using ray-plane intersection.
        ///
        /// How it works:
        ///   1. HelixTool shoots a ray from the camera through the mouse pixel.
        ///   2. We find where that ray intersects the Z=0 horizontal plane.
        ///   3. That intersection is the "world position" under the cursor.
        /// </summary>
        private Point3D? ScreenToGroundPlane(System.Windows.Point mousePos)
        {
            // Point2DtoRay3D: HelixTool converts screen position → 3D ray
            var ray = IfcViewer.Viewport.Point2DtoRay3D(mousePos);
            if (ray == null) return null;

            // PlaneIntersection: find where the ray hits Z=0 plane
            // Parameters: a point on the plane (origin), the plane normal (Z-up)
            return ray.PlaneIntersection(
                new Point3D(0, 0, 0),
                new Vector3D(0, 0, 1)
            );
        }
        #endregion
        #endregion
 

    }
}
