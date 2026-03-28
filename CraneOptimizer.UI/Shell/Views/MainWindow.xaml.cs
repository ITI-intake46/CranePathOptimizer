using System.Windows;
using System.Windows.Input;

namespace CraneOptimizer.UI.Shell.Views
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
        }

        // ── Title bar drag ─────────────────────────────────────────────────
        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount == 2)
                ToggleMaximize();
            else
                DragMove();
        }

        // ── Window chrome ──────────────────────────────────────────────────
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

            MaximizeButton.Content = WindowState == WindowState.Maximized ? "❐" : "☐";
        }

        // ── Toolbar passthrough (routed to ViewModel via commands) ─────────
        private void SaveProject_Click(object sender, RoutedEventArgs e) { /* TODO: bind to command */ }
        private void UndoBtn_Click(object sender, RoutedEventArgs e)     { /* TODO: bind to command */ }
        private void RedoBtn_Click(object sender, RoutedEventArgs e)     { /* TODO: bind to command */ }
        private void ClearHistory_Click(object sender, RoutedEventArgs e){ /* TODO: bind to command */ }

        // ── Viewport ───────────────────────────────────────────────────────
        private void ResetCamera_Click(object sender, RoutedEventArgs e)
            => IfcViewer?.ZoomExtents();
    }
}
