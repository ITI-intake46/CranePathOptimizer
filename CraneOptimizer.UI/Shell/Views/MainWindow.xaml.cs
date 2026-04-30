using CraneOptimizer.Domain.Common;
using CraneOptimizer.Domain.Schemas;
using CraneOptimizer.UI.Core.Interfaces;
using CraneOptimizer.UI.Core.Models;
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
        private bool _isDraggingIfcElements;
        private Point3D _lastIfcDragPoint;

        public MainWindow(
            MainViewModel vm,
            IRenderingService renderingService,
            ISceneService sceneService)
        {
            InitializeComponent();

            _renderingService = renderingService;
            _sceneService = sceneService;

            DataContext = vm;

            _renderingService.Initialize(IfcViewer);

            vm.Dxf.Initialize(IfcViewer);

            _sceneService.ZoneAdded += OnZoneAdded;
            _sceneService.ZoneSelected += OnZoneSelected;
            _sceneService.ZoneRemoved += OnZoneChanged;
            _sceneService.ZoneUpdated += OnZoneChanged;
            _sceneService.ZonesCleared += OnZonesCleared;

            _sceneService.CraneSelected += OnCraneSelected;
        }

        private MainViewModel? VM => DataContext as MainViewModel;

        private void OnZoneAdded(GreenAreaZone zone) => RenderZones();
        private void OnZoneSelected(GreenAreaZone? zone) => RenderZones();
        private void OnZoneChanged(GreenAreaZone zone) => RenderZones();

        private void OnZonesCleared() => RenderZones();

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

                    if (tag is IfcElementVisualRef ifcElement)
                    {
                        SelectIfcElement(ifcElement, e);
                        return;
                    }
                }
            }

            _sceneService.SetSelectedZone(null);
            _sceneService.ClearIfcElementSelection();
        }

        private void SelectIfcElement(IfcElementVisualRef ifcElement, MouseButtonEventArgs e)
        {
            _sceneService.SetSelectedZone(null);
            _sceneService.SetSelectedCrane(null);

            if ((Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
                _sceneService.ToggleSelectedIfcElement(ifcElement);
            else
                _sceneService.SetSelectedIfcElements(new List<IfcElementVisualRef> { ifcElement });

            var viewport = IfcViewer;
            if (TryGetWorldPoint(viewport, e.GetPosition(viewport), out var dragStart))
            {
                _lastIfcDragPoint = dragStart;
                _isDraggingIfcElements = true;
            }

            e.Handled = true;
        }

        protected override void OnClosed(EventArgs e)
        {
            _sceneService.ZoneAdded -= OnZoneAdded;
            _sceneService.ZoneSelected -= OnZoneSelected;
            _sceneService.ZoneRemoved -= OnZoneChanged;
            _sceneService.ZoneUpdated -= OnZoneChanged;
            _sceneService.ZonesCleared -= OnZonesCleared;
            _sceneService.CraneSelected -= OnCraneSelected;

            base.OnClosed(e);
        }

        private void IfcViewer_MouseMove(object sender, MouseEventArgs e)
        {
            var viewport = sender as HelixViewport3D;
            if (viewport == null) return;

            var screen = e.GetPosition(viewport);
            if (!TryGetWorldPoint(viewport, screen, out var world))
                return;

            if (VM?.IsDraggingCrane == true && e.LeftButton == MouseButtonState.Pressed)
            {
                VM.DragCrane(new DomainPoint3D(world.X, world.Y, world.Z));
                e.Handled = true;
                return;
            }

            if (_isDraggingIfcElements && e.LeftButton == MouseButtonState.Pressed)
            {
                var selected = _sceneService.GetSelectedIfcElements();
                if (selected.Count > 0)
                {
                    var dx = world.X - _lastIfcDragPoint.X;
                    var dy = world.Y - _lastIfcDragPoint.Y;
                    var dz = world.Z - _lastIfcDragPoint.Z;

                    _sceneService.MoveIfcElements(selected, dx, dy, dz);
                    _lastIfcDragPoint = world;
                    e.Handled = true;
                    return;
                }
            }

            var vm = VM?.Drawing;
            if (vm == null || !vm.IsDrawing) return;

            vm.SetHoverPoint(new DomainPoint3D(world.X, world.Y, world.Z));
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

        private void IfcViewer_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton != MouseButton.Left)
                return;

            VM?.EndDrag();
            _isDraggingIfcElements = false;
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
            OsnapPopup.IsOpen = true;
        }

        private void StatusOsnapMaster_Click(object sender, RoutedEventArgs e) { }
        private void StatusOsnap_Click(object sender, RoutedEventArgs e) { }

        private void ShowCommandPanel_Click(object sender, RoutedEventArgs e) { }
        private void ShowLogsPanel_Click(object sender, RoutedEventArgs e) { }

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
