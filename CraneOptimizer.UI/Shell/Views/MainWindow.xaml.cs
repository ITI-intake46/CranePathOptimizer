using CraneOptimizer.Domain.Common;
using CraneOptimizer.Domain.Schemas;
using CraneOptimizer.UI.Core.Interfaces;
using CraneOptimizer.UI.Core.Services;
using CraneOptimizer.UI.Features.F7_Zones;
using CraneOptimizer.UI.Features.F7_Zones.ViewModels;
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
        private readonly IRenderingService _renderingService;
        private readonly ISceneService _sceneService;

        public MainWindow(
            MainViewModel vm,
            IRenderingService renderingService,
            ISceneService sceneService)   // 👈 أضفنا SceneService
        {
            InitializeComponent();

            _renderingService = renderingService;
            _sceneService = sceneService;

            DataContext = vm;

            // ── Initialize Rendering ─────────────────────
            _renderingService.Initialize(IfcViewer);

            // ── 🔥 IMPORTANT: Hook Zones Rendering ───────
            _sceneService.ZoneAdded += OnZoneAdded;
        }

        private MainViewModel? VM => DataContext as MainViewModel;

        // ────────────────────────────────────────────────
        // 🔥 Zone Rendering Hook
        // ────────────────────────────────────────────────
        private void OnZoneAdded(GreenAreaZone zone)
        {
            _renderingService.Render(
                new[] { zone },
                new ZoneVisualBuilder(),
                $"Zone_{zone.ZoneId}"
            );
        }

        // ────────────────────────────────────────────────
        // Lifecycle (important)
        // ────────────────────────────────────────────────
        protected override void OnClosed(EventArgs e)
        {
            _sceneService.ZoneAdded -= OnZoneAdded;
            base.OnClosed(e);
        }

        // ── TITLE BAR ───────────────────────────────────
        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount == 2) ToggleMaximize();
            else DragMove();
        }

        private void MinimizeWindow_Click(object sender, RoutedEventArgs e)
            => WindowState = WindowState.Minimized;

        private void MaximizeWindow_Click(object sender, RoutedEventArgs e)
            => ToggleMaximize();

        private void CloseWindow_Click(object sender, RoutedEventArgs e)
            => Close();

        private void ToggleMaximize()
        {
            WindowState = WindowState == WindowState.Maximized
                ? WindowState.Normal
                : WindowState.Maximized;
        }

        // ── TOOLBAR STUBS ───────────────────────────────
        private void BackToWelcome_Click(object sender, RoutedEventArgs e)
        {
            // TODO
        }

        private void SaveProject_Click(object sender, RoutedEventArgs e)
        {
            // TODO
        }

        private void UndoBtn_Click(object sender, RoutedEventArgs e)
        {
            // TODO
        }

        private void RedoBtn_Click(object sender, RoutedEventArgs e)
        {
            // TODO
        }

        private void BtnDimensionTool_Click(object sender, RoutedEventArgs e)
        {
            // TODO
        }

        private void ClearHistory_Click(object sender, RoutedEventArgs e)
        {
            if (VM != null) VM.HistoryBreadcrumb = "No recent action";
        }

        // ── ELEMENT LIST ────────────────────────────────
        private void ElementItem_Click(object sender, MouseButtonEventArgs e)
        {
            if (sender is FrameworkElement fe && fe.Tag is { } element)
                if (VM != null)
                    VM.SelectedElementName = element.ToString() ?? string.Empty;
        }

        // ── VIEWPORT ────────────────────────────────────
        private void ResetCamera_Click(object sender, RoutedEventArgs e)
            => IfcViewer.ZoomExtents();

        private void IfcViewer_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            // Helix handles zoom
        }

        // ── RENDER QUALITY ──────────────────────────────
        private void RenderQualityDropBtn_Click(object sender, RoutedEventArgs e)
            => RenderQualityPopup.IsOpen = !RenderQualityPopup.IsOpen;

        private void RenderQuality_Changed(object sender, RoutedEventArgs e)
        {
            if (VM == null) return;
            VM.RenderQualityLabel = rbRenderHigh?.IsChecked == true ? "● HQ" : "● STD";
            RenderQualityPopup.IsOpen = false;
        }

        // ── OSNAP ───────────────────────────────────────
        private void OsnapDropBtn_Click(object sender, RoutedEventArgs e)
            => OsnapPopup.IsOpen = !OsnapPopup.IsOpen;

        private void StatusOsnapMaster_Click(object sender, RoutedEventArgs e)
        {
            // TODO
        }

        private void StatusOsnap_Click(object sender, RoutedEventArgs e)
        {
            if (sender is ToggleButton tb)
            {
                var snapMode = tb.Tag?.ToString();
                var isEnabled = tb.IsChecked == true;
                // TODO
            }
        }

        // ── STATUS BAR ──────────────────────────────────
        private void ShowCommandPanel_Click(object sender, RoutedEventArgs e)
        {
            // TODO
        }

        private void ShowLogsPanel_Click(object sender, RoutedEventArgs e)
        {
            // TODO
        }

        // ── PUBLIC HELPERS ─────────────────────────────
        public void SetCoordinates(double x, double y)
            => VM?.SetCoordinates(x, y);

        public void SetLastAction(string description)
        {
            if (VM != null) VM.HistoryBreadcrumb = description;
        }

        public void SetStatus(string text)
        {
            if (VM != null) VM.StatusText = text;
        }

        private void IfcViewer_MouseMove(object sender, MouseEventArgs e)
        {
            var viewport = sender as HelixViewport3D;
            if (viewport == null) return;

            var mainVm = DataContext as MainViewModel;
            var vm = mainVm?.Drawing;
            if (vm == null || !vm.IsDrawing) return;

            var screen = e.GetPosition(viewport);
            var world = GetWorldPoint(viewport, screen);

            vm.SetHoverPoint(new DomainPoint3D(world.X, world.Y, world.Z));
        }

        private void IfcViewer_MouseDown(object sender, MouseButtonEventArgs e)
        {
            var viewport = sender as HelixViewport3D;
            if (viewport == null) return;

            var mainVm = DataContext as MainViewModel;
            var vm = mainVm?.Drawing;
            if (vm == null || !vm.IsDrawing) return;
            if (e.ChangedButton == MouseButton.Right)
            {
                vm.Finish();
                e.Handled = true;
                return;
            }

            if (e.ChangedButton != MouseButton.Left)
            {
                return;
            }

            var screen = e.GetPosition(viewport);

            var world = GetWorldPoint(viewport, screen);

            var point = new DomainPoint3D(world.X, world.Y, world.Z);

            vm.AddPoint(point);
            e.Handled = true;
        }
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
    }
}
