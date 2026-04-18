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

            // ✅ IMPORTANT: attach ViewModel
            DataContext = vm;

            // ✅ Initialize Helix
            _renderingService.Initialize(IfcViewer);
        }

        // =========================
        // Helpers
        // =========================
        private MainViewModel VM => DataContext as MainViewModel;

        // =========================
        // TITLE BAR
        // =========================
        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount == 2)
                ToggleMaximize();
            else
                DragMove();
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

        // =========================
        // TOOLBAR
        // =========================
        private void ClearHistory_Click(object sender, RoutedEventArgs e)
        {
            if (VM != null)
                VM.HistoryBreadcrumb = "No recent action";
        }

        private void ElementItem_Click(object sender, MouseButtonEventArgs e)
        {
            if (sender is FrameworkElement fe && fe.Tag is { } element)
            {
                if (VM != null)
                    VM.SelectedElementName = element.ToString() ?? string.Empty;
            }
        }

        // =========================
        // VIEWPORT
        // =========================
        private void ResetCamera_Click(object sender, RoutedEventArgs e)
        {
            IfcViewer.ZoomExtents();
        }

        private void IfcViewer_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            // Keep default Helix behavior
        }

        // =========================
        // RENDER QUALITY
        // =========================
        private void RenderQualityDropBtn_Click(object sender, RoutedEventArgs e)
        {
            // TODO: wire when Popup is added to XAML
        }
        private void RenderQuality_Changed(object sender, RoutedEventArgs e)
        {
            if (VM == null) return;
            VM.RenderQualityLabel = "● STD"; // temporary until Popup restored
        }

        // =========================
        // OSNAP
        // =========================
        private void OsnapDropBtn_Click(object sender, RoutedEventArgs e)
        {
            // TODO: wire when OsnapPopup is added to XAML
        }
        private void StatusOsnap_Click(object sender, RoutedEventArgs e)
        {
            if (sender is ToggleButton tb)
            {
                var snapMode = tb.Tag?.ToString();
                var isEnabled = tb.IsChecked == true;

                // TODO → delegate to DrawingViewModel
            }
        }

        // =========================
        // STATUS HELPERS (MVVM SAFE)
        // =========================
        public void SetCoordinates(double x, double y)
        {
            VM?.SetCoordinates(x, y);
        }

        public void SetModelLoaded(bool loaded)
        {
            if (VM != null)
                VM.IsModelLoaded = loaded;
        }

        public void SetLastAction(string description)
        {
            if (VM != null)
                VM.HistoryBreadcrumb = description;
        }

        public void SetStatus(string text)
        {
            if (VM != null)
                VM.StatusText = text;
        }
    }
}