using CraneOptimizer.UI.Core.Interfaces;
using CraneOptimizer.UI.Shell.ViewModels;
using System.Windows;
using System.Windows.Input;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;

namespace CraneOptimizer.UI.Shell.Views
{
    public partial class MainWindow : Window
    {
        private readonly IRenderingService _renderingService;

        public MainWindow(MainViewModel vm, IRenderingService renderingService)
        {
            InitializeComponent();
            _renderingService = renderingService;
            DataContext = vm;
            _renderingService.Initialize(IfcViewer);
        }

        private MainViewModel? VM => DataContext as MainViewModel;

        // ── TITLE BAR ────────────────────────────────────────────────────
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

        // ── TOOLBAR STUBS ─────────────────────────────────────────────────
        private void BackToWelcome_Click(object sender, RoutedEventArgs e)
        {
            // TODO: navigate back to WelcomePage
        }

        private void SaveProject_Click(object sender, RoutedEventArgs e)
        {
            // TODO: delegate to MainViewModel
        }

        private void UndoBtn_Click(object sender, RoutedEventArgs e)
        {
            // TODO: delegate to DrawingViewModel.Undo()
        }

        private void RedoBtn_Click(object sender, RoutedEventArgs e)
        {
            // TODO: delegate to DrawingViewModel.Redo()
        }

        private void BtnDimensionTool_Click(object sender, RoutedEventArgs e)
        {
            // TODO: activate dimension tool
        }

        private void ClearHistory_Click(object sender, RoutedEventArgs e)
        {
            if (VM != null) VM.HistoryBreadcrumb = "No recent action";
        }

        // ── ELEMENT LIST ──────────────────────────────────────────────────
        private void ElementItem_Click(object sender, MouseButtonEventArgs e)
        {
            if (sender is FrameworkElement fe && fe.Tag is { } element)
                if (VM != null)
                    VM.SelectedElementName = element.ToString() ?? string.Empty;
        }

        // ── VIEWPORT ─────────────────────────────────────────────────────
        private void ResetCamera_Click(object sender, RoutedEventArgs e)
            => IfcViewer.ZoomExtents();

        private void IfcViewer_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            // Helix handles zoom natively
        }

        // ── RENDER QUALITY ────────────────────────────────────────────────
        private void RenderQualityDropBtn_Click(object sender, RoutedEventArgs e)
            => RenderQualityPopup.IsOpen = !RenderQualityPopup.IsOpen;

        private void RenderQuality_Changed(object sender, RoutedEventArgs e)
        {
            if (VM == null) return;
            VM.RenderQualityLabel = rbRenderHigh?.IsChecked == true ? "● HQ" : "● STD";
            RenderQualityPopup.IsOpen = false;
        }

        // ── OSNAP ─────────────────────────────────────────────────────────
        private void OsnapDropBtn_Click(object sender, RoutedEventArgs e)
            => OsnapPopup.IsOpen = !OsnapPopup.IsOpen;

        private void StatusOsnapMaster_Click(object sender, RoutedEventArgs e)
        {
            // TODO: enable/disable all snap modes via DrawingViewModel
        }

        private void StatusOsnap_Click(object sender, RoutedEventArgs e)
        {
            if (sender is ToggleButton tb)
            {
                var snapMode = tb.Tag?.ToString();
                var isEnabled = tb.IsChecked == true;
                // TODO: delegate to DrawingViewModel
            }
        }

        // ── STATUS BAR PANELS ────────────────────────────────────────────
        private void ShowCommandPanel_Click(object sender, RoutedEventArgs e)
        {
            // TODO: show/collapse command panel
        }

        private void ShowLogsPanel_Click(object sender, RoutedEventArgs e)
        {
            // TODO: open logs panel
        }

        // ── PUBLIC HELPERS (called by services) ──────────────────────────
        public void SetCoordinates(double x, double y)
            => VM?.SetCoordinates(x, y);

        public void SetModelLoaded(bool loaded)
        {
            // ✅ NoModelOverlay is now driven by IsModelLoaded binding
            // No need to touch Visibility directly
            if (VM != null) VM.IsModelLoaded = loaded;
        }

        public void SetLastAction(string description)
        {
            if (VM != null) VM.HistoryBreadcrumb = description;
        }

        public void SetStatus(string text)
        {
            if (VM != null) VM.StatusText = text;
        }
    }
}