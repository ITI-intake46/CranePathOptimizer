using CraneOptimizer.Domain.Common;
using CraneOptimizer.Domain.Schemas;
using CraneOptimizer.UI.Core.Interfaces;
using CraneOptimizer.UI.Core.Models;
using CraneOptimizer.UI.Features.F3_CraneSetup.ViewModels;
using CraneOptimizer.UI.Features.F7_Zones;
using CraneOptimizer.UI.Shell.ViewModels;
using HelixToolkit.Wpf;
using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media.Media3D;

namespace CraneOptimizer.UI.Shell.Views
{
    public partial class MainWindow : Window
    {
        private const double DefaultGridSizeMeters = 50d;
        private const double GridPaddingFactor = 1.2d;
        private const double GridRoundStepMeters = 10d;

        private readonly IRenderingService _renderingService;
        private readonly ISceneService _sceneService;
        private CraneSetupViewModel? _craneSetup;
        private bool _manualGridSize;
        private double _gridCenterXMillimeters;
        private double _gridCenterYMillimeters;

        //private readonly OsnapPanelViewModel _osnapVm;
        //private readonly IOsnapService _osnapService;
        //private readonly OsnapGeometryBridge _osnapBridge;
        //private OsnapIndicator? _osnapIndicator;
        // ← NEW

        public MainWindow(
            MainViewModel vm,
            IRenderingService renderingService,
            ISceneService sceneService,
//            OsnapPanelViewModel osnapVm,
//IOsnapService osnapService,
//OsnapGeometryBridge osnapBridge,
            CraneSetupViewModel craneSetup)
        {
            InitializeComponent();

            _renderingService = renderingService;
            _sceneService = sceneService;
            _craneSetup = craneSetup;                // ← NEW

            //_osnapVm = osnapVm;
            //_osnapService = osnapService;
            //_osnapBridge = osnapBridge;   // activates scene-event subscriptions

            DataContext = vm;

            //// Give the embedded OsnapStatusPanel its own ViewModel
            //OsnapPanel.DataContext = osnapVm;

            //// Create the AutoCAD-style snap indicator on the overlay canvas
            //_osnapIndicator = new OsnapIndicator(OsnapOverlayCanvas);

            _renderingService.Initialize(IfcViewer);

            //_osnapBridge.AttachViewport(IfcViewer);

            // Wire the DXF uploader to the same Helix viewport.
            // Mirrors the IRenderingService pattern — must be done after InitializeComponent.
            vm.Dxf.Initialize(IfcViewer);

            //// After DXF loads, push snap points to the bridge so snapping works on DXF geometry
            //vm.Dxf.DxfLoaded += uploader => _osnapBridge.RefreshDxf(uploader);

            _sceneService.ZoneAdded += OnZoneAdded;
            _sceneService.ZoneSelected += OnZoneSelected;
            _sceneService.ZoneRemoved += OnZoneChanged;
            _sceneService.ZoneUpdated += OnZoneChanged;
            _sceneService.ZonesCleared += OnZonesCleared;

            _sceneService.ElementsChanged += OnElementsChanged;
            _sceneService.CraneSelected += OnCraneSelected;

            SetGridText(DefaultGridSizeMeters, DefaultGridSizeMeters);
        }

        private MainViewModel? VM => DataContext as MainViewModel;

        private void OnZoneAdded(GreenAreaZone zone) => RenderZones();
        private void OnZoneSelected(GreenAreaZone? zone) => RenderZones();
        private void OnZoneChanged(GreenAreaZone zone) => RenderZones();

        private void OnZonesCleared() => RenderZones();

        private void OnElementsChanged(List<PrecastElement> elements)
        {
            if (_manualGridSize)
                return;

            var (widthMeters, lengthMeters, centerXMillimeters, centerYMillimeters) = CalculateGridSizeMeters(elements);
            _gridCenterXMillimeters = centerXMillimeters;
            _gridCenterYMillimeters = centerYMillimeters;
            SetGridText(widthMeters, lengthMeters);
            _renderingService.SetGridSize(
                widthMeters * 1000.0,
                lengthMeters * 1000.0,
                _gridCenterXMillimeters,
                _gridCenterYMillimeters);
        }

        private void RenderZones()
        {
            _renderingService.Render(
                _sceneService.GetZones(),
                new ZoneVisualBuilder(_sceneService),
                "Zones"
            );
        }

        private void OnCraneSelected(SceneCrane? crane)
        {
            // Placeholder for future UI highlight/inspector sync.
        }

        private void HandleZoneSelection(MouseButtonEventArgs e)
        {
            var viewport = IfcViewer;
            var hits = viewport.Viewport.FindHits(e.GetPosition(viewport));

            if (hits == null || hits.Count == 0)
            {
                _sceneService.SetSelectedZone(null);
                _sceneService.SetSelectedCrane(null);
                return;
            }

            foreach (var hit in hits)
            {
                var visual = hit.Visual;

                if (visual is ModelVisual3D mv)
                {
                    var tag = mv.GetValue(FrameworkElement.TagProperty);

                    if (tag is GreenAreaZone zone)
                    {
                        _sceneService.SetSelectedZone(zone);
                        return;
                    }

                    if (tag is SceneCrane crane)
                    {
                        _sceneService.SetSelectedCrane(crane);
                        return;
                    }
                }
            }

            _sceneService.SetSelectedZone(null);
        }

        protected override void OnClosed(EventArgs e)
        {
            _sceneService.ZoneAdded -= OnZoneAdded;
            _sceneService.ZoneSelected -= OnZoneSelected;
            _sceneService.ZoneRemoved -= OnZoneChanged;
            _sceneService.ZoneUpdated -= OnZoneChanged;
            _sceneService.ZonesCleared -= OnZonesCleared;
            _sceneService.ElementsChanged -= OnElementsChanged;
            _sceneService.CraneSelected -= OnCraneSelected;

            base.OnClosed(e);
        }

        private void IfcViewer_MouseMove(object sender, MouseEventArgs e)
        {
            var viewport = sender as HelixViewport3D;
            if (viewport == null) return;
            //if (sender is not HelixViewport3D viewport) return;

            //// Get position relative to the HelixViewport3D — this matches
            //// the OsnapOverlayCanvas coordinate space (sibling in same Grid cell)
            //// so snap markers are positioned correctly.
            var screen = e.GetPosition(viewport);

            //// ── OSnap indicator: pass screen cursor directly (screen-space engine) ──
            //if (_osnapIndicator != null)
            //{
            //    var result = _osnapService.Snap(screen, viewport);
            //    _osnapIndicator.Update(result);
            //}

            //// ── Drawing hover point ───
            //var screen = e.GetPosition(viewport);
            if (!TryGetWorldPoint(viewport, screen, out var world))
                return;

            if (VM?.IsDraggingCrane == true && e.LeftButton == MouseButtonState.Pressed)
            {
                VM.DragCrane(new DomainPoint3D(world.X, world.Y, world.Z));
                e.Handled = true;
                return;
            }

            var vm = VM?.Drawing;
            if (vm == null || !vm.IsDrawing) return;

            vm.SetHoverPoint(new DomainPoint3D(world.X, world.Y, world.Z));
        }

        private void MainWindow_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (VM?.IfcViewer.IsPlacementMode == true)
                return;

            VM?.Drawing.HandleKeyDown(e);
        }

        private void IfcViewer_MouseDown(object sender, MouseButtonEventArgs e)
        {
            var viewport = sender as HelixViewport3D;
            if (viewport == null) return;

            if (e.ChangedButton == MouseButton.Left)
            {
                var pickPos = e.GetPosition(viewport);
                if (TryGetWorldPoint(viewport, pickPos, out var pickWorld)
                    && TryGetHitCrane(viewport, pickPos, out var hitCrane)
                    && VM != null)
                {
                    VM.SelectedSceneCrane = hitCrane;
                    VM.StartDrag(new DomainPoint3D(pickWorld.X, pickWorld.Y, pickWorld.Z));
                    e.Handled = true;
                    return;
                }
            }

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

            if (VM?.CranePlacement.IsPlacing == true)
            {
                if (e.ChangedButton != MouseButton.Left)
                    return;

                var cranePlacementScreen = e.GetPosition(viewport);
                if (!TryGetWorldPoint(viewport, cranePlacementScreen, out var cranePoint))
                    return;

                var point = new DomainPoint3D(cranePoint.X, cranePoint.Y, cranePoint.Z);
                VM.CranePlacement.Place(point);
                VM.PlaceSelectedCrane(point);
                e.Handled = true;
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
                if (!vm.IsMeasuring)
                    return;
            }

            if (vm == null) return;

            if (e.ChangedButton == MouseButton.Right)
            {
                vm.HandleRightClick();
                e.Handled = true;
                return;
            }

            if (e.ChangedButton != MouseButton.Left)
                return;

            var screen = e.GetPosition(viewport);
            if (!TryGetWorldPoint(viewport, screen, out var world))
                return;

            if (e.ClickCount > 1)
            {
                vm.HandleDoubleClick();
                e.Handled = true;
                return;
            }

            vm.HandleViewportLeftClick(new DomainPoint3D(world.X, world.Y, world.Z));
            e.Handled = true;
        }

        private void IfcViewer_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton != MouseButton.Left)
                return;

            VM?.EndDrag();
        }

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
            //OsnapPopup.IsOpen = true;
        }

        private void StatusOsnapMaster_Click(object sender, RoutedEventArgs e) { }
        private void StatusOsnap_Click(object sender, RoutedEventArgs e) { }
        //private void OsnapDropBtn_Click(object sender, RoutedEventArgs e)
        //{
        //    // Kept for XAML event wiring — actual popup is now inside OsnapStatusPanel UserControl.
        //}

        //private void StatusOsnapMaster_Click(object sender, RoutedEventArgs e)
        //{
        //    if (sender is ToggleButton tb)
        //        _osnapVm.MasterEnabled = tb.IsChecked == true;
        //}

        //private void StatusOsnap_Click(object sender, RoutedEventArgs e)
        //{
        //    if (sender is ToggleButton btn && btn.Tag is string snapName
        //        && Enum.TryParse<OsnapType>(snapName, out var snapType))
        //        _osnapVm.SetSnapEnabled(snapType, btn.IsChecked == true);
        //}
        private void ShowCommandPanel_Click(object sender, RoutedEventArgs e) { }
        private void ShowLogsPanel_Click(object sender, RoutedEventArgs e) { }

        private void ApplyGridSize_Click(object sender, RoutedEventArgs e)
        {
            if (!TryReadGridSizeMeters(out var widthMeters, out var lengthMeters))
            {
                VM!.StatusText = "Grid size must be positive numbers in meters.";
                return;
            }

            _manualGridSize = true;
            _renderingService.SetGridSize(
                widthMeters * 1000.0,
                lengthMeters * 1000.0,
                _gridCenterXMillimeters,
                _gridCenterYMillimeters);
            VM!.StatusText = $"Grid size set to {widthMeters:F0}m x {lengthMeters:F0}m.";
        }

        private void AutoGridSize_Click(object sender, RoutedEventArgs e)
        {
            _manualGridSize = false;
            OnElementsChanged(_sceneService.GetElements());
            VM!.StatusText = "Grid size set from building extents.";
        }

        private bool TryReadGridSizeMeters(out double widthMeters, out double lengthMeters)
        {
            var hasWidth = double.TryParse(GridSizeXTextBox.Text, out widthMeters);
            var hasLength = double.TryParse(GridSizeYTextBox.Text, out lengthMeters);

            return hasWidth
                && hasLength
                && widthMeters > 0
                && lengthMeters > 0;
        }

        private void SetGridText(double widthMeters, double lengthMeters)
        {
            GridSizeXTextBox.Text = widthMeters.ToString("0");
            GridSizeYTextBox.Text = lengthMeters.ToString("0");
        }

        private static (double widthMeters, double lengthMeters, double centerXMillimeters, double centerYMillimeters)
            CalculateGridSizeMeters(IReadOnlyList<PrecastElement> elements)
        {
            if (elements.Count == 0)
                return (DefaultGridSizeMeters, DefaultGridSizeMeters, 0d, 0d);

            var minX = elements.Min(e => e.ElementCOG.X - ((e.BBoxLength ?? 0d) / 2d));
            var maxX = elements.Max(e => e.ElementCOG.X + ((e.BBoxLength ?? 0d) / 2d));
            var minY = elements.Min(e => e.ElementCOG.Y - ((e.BBoxWidth ?? 0d) / 2d));
            var maxY = elements.Max(e => e.ElementCOG.Y + ((e.BBoxWidth ?? 0d) / 2d));

            var buildingWidthMeters = Math.Max(0d, (maxX - minX) / 1000.0);
            var buildingLengthMeters = Math.Max(0d, (maxY - minY) / 1000.0);
            var centerXMillimeters = (minX + maxX) / 2.0;
            var centerYMillimeters = (minY + maxY) / 2.0;

            return (
                CalculateGridAxisMeters(buildingWidthMeters),
                CalculateGridAxisMeters(buildingLengthMeters),
                centerXMillimeters,
                centerYMillimeters);
        }

        private static double CalculateGridAxisMeters(double buildingAxisMeters)
        {
            if (buildingAxisMeters <= DefaultGridSizeMeters)
                return DefaultGridSizeMeters;

            var padded = buildingAxisMeters * GridPaddingFactor;
            return Math.Ceiling(padded / GridRoundStepMeters) * GridRoundStepMeters;
        }



        #region Helpers

        #region Dragging the Crane Visual   
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

        #endregion
        #endregion
 

        private bool TryGetHitCrane(HelixViewport3D viewport, System.Windows.Point screenPt, out SceneCrane crane)
        {
            crane = null!;

            try
            {
                var hits = viewport.Viewport.FindHits(screenPt);
                foreach (var h in hits)
                {
                    if (h.Visual is ModelVisual3D mv)
                    {
                        var tag = mv.GetValue(FrameworkElement.TagProperty);
                        if (tag is SceneCrane sc)
                        {
                            crane = sc;
                            return true;
                        }
                    }

                    if (h.Visual is Visual3D v3)
                    {
                        var tag = v3.GetValue(FrameworkElement.TagProperty);
                        if (tag is SceneCrane sc2)
                        {
                            crane = sc2;
                            return true;
                        }
                    }
                }
            }
            catch { }

            return false;
        }
    }
}
