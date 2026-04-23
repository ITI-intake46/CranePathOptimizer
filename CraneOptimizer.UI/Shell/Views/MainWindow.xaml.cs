using CraneOptimizer.Domain.Common;
using CraneOptimizer.Domain.Schemas;
using CraneOptimizer.UI.Core.Interfaces;
using CraneOptimizer.UI.Features.F7_Zones;
using CraneOptimizer.UI.Shell.ViewModels;
using HelixToolkit.Wpf;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media.Media3D;

namespace CraneOptimizer.UI.Shell.Views
{
    public partial class MainWindow : Window
    {
        private readonly IRenderingService _renderingService;
        private readonly ISceneService _sceneService;

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
            var world = GetWorldPoint(viewport, screen);

            vm.SetHoverPoint(new DomainPoint3D(world.X, world.Y, world.Z));
        }

        private void IfcViewer_MouseDown(object sender, MouseButtonEventArgs e)
        {
            var viewport = sender as HelixViewport3D;
            if (viewport == null) return;

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
            var world = GetWorldPoint(viewport, screen);

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

        private Point3D GetWorldPoint(HelixViewport3D viewport, System.Windows.Point screenPt)
        {
            try
            {
                var hit = viewport.FindNearestPoint(screenPt);
                if (hit.HasValue)
                    return hit.Value;
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
                            return near + t * dir;
                    }
                }
            }
            catch { }

            return new Point3D();
        }

        #endregion
    }
}