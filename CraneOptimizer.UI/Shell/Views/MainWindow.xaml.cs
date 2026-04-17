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
        // ─────────────────────────────────────────────────────────────────
        // Constructor
        // ─────────────────────────────────────────────────────────────────

        public MainWindow(MainViewModel viewModel, IRenderingService renderingService)
        {
            InitializeComponent();
            DataContext = viewModel;

            // Initialise the Helix rendering service immediately after the visual
            // tree is built. This populates the scene with the default grid, lights,
            // and coordinate system so the viewport is usable even before any IFC
            // file is loaded ("Empty Scene Mode").
            renderingService.Initialize(IfcViewer);
        }

        // ─────────────────────────────────────────────────────────────────
        // TITLE BAR — drag, window chrome
        // ─────────────────────────────────────────────────────────────────

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

        // ─────────────────────────────────────────────────────────────────
        // QUICK-ACCESS TOOLBAR
        // ─────────────────────────────────────────────────────────────────

        private void BackToWelcome_Click(object sender, RoutedEventArgs e)
        {
            // TODO: navigate back to WelcomePage (show WelcomePage, hide this window)
        }

        private void SaveProject_Click(object sender, RoutedEventArgs e)
        {
            // TODO: delegate to IProjectRepository / MainViewModel
        }

        private void UndoBtn_Click(object sender, RoutedEventArgs e)
        {
            // TODO: delegate to DrawingViewModel.Undo()
        }

        private void RedoBtn_Click(object sender, RoutedEventArgs e)
        {
            // TODO: delegate to DrawingViewModel.Redo()
        }

        private void ClearHistory_Click(object sender, RoutedEventArgs e)
        {
            TxtHistoryBreadcrumb.Text = "No recent action";
        }

        private void BtnDimensionTool_Click(object sender, RoutedEventArgs e)
        {
            // TODO: activate dimension/measure tool
        }

        // ─────────────────────────────────────────────────────────────────
        // ELEMENT LIST
        // ─────────────────────────────────────────────────────────────────

        private void ElementItem_Click(object sender, MouseButtonEventArgs e)
        {
            if (sender is FrameworkElement fe && fe.Tag is { } element)
            {
                // TODO: select element in scene + populate PropertiesPanel
                TxtSelectedElementName.Text = element.ToString();
            }
        }

        // ─────────────────────────────────────────────────────────────────
        // VIEWPORT
        // ─────────────────────────────────────────────────────────────────

        private void IfcViewer_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            // Let HelixViewport3D handle zoom natively — override only if needed
        }

        private void ResetCamera_Click(object sender, RoutedEventArgs e)
        {
            IfcViewer.ZoomExtents();
        }

        // ─────────────────────────────────────────────────────────────────
        // CRANE PANEL EVENTS (routed up from SitePlannerPanel)
        // ─────────────────────────────────────────────────────────────────

        private void AddTowerCrane_Click(object sender, RoutedEventArgs e)
        {
            // TODO: delegate to MainViewModel / CranePlacementViewModel
        }

        private void AddMobileCrane_Click(object sender, RoutedEventArgs e)
        {
            // TODO: delegate to MainViewModel / CranePlacementViewModel
        }

        private void RemoveSelectedCrane_Click(object sender, RoutedEventArgs e)
        {
            // TODO: delegate to CranePlacementViewModel.RemoveSelected()
        }

        private void RemoveAllCranes_Click(object sender, RoutedEventArgs e)
        {
            // TODO: delegate to CranePlacementViewModel.RemoveAll()
        }

        // ─────────────────────────────────────────────────────────────────
        // STATUS BAR — RENDER QUALITY POPUP
        // ─────────────────────────────────────────────────────────────────

        private void RenderQualityDropBtn_Click(object sender, RoutedEventArgs e)
            => RenderQualityPopup.IsOpen = !RenderQualityPopup.IsOpen;

        private void RenderQuality_Changed(object sender, RoutedEventArgs e)
        {
            if (rbRenderHigh?.IsChecked == true)
                TxtRenderQuality.Text = "● HQ";
            else
                TxtRenderQuality.Text = "● STD";

            RenderQualityPopup.IsOpen = false;
        }

        // ─────────────────────────────────────────────────────────────────
        // STATUS BAR — OSNAP POPUP
        // ─────────────────────────────────────────────────────────────────

        private void OsnapDropBtn_Click(object sender, RoutedEventArgs e)
            => OsnapPopup.IsOpen = !OsnapPopup.IsOpen;

        private void StatusOsnapMaster_Click(object sender, RoutedEventArgs e)
        {
            // TODO: enable/disable all osnap modes via DrawingService / DrawingViewModel
        }

        private void StatusOsnap_Click(object sender, RoutedEventArgs e)
        {
            if (sender is ToggleButton tb)
            {
                var snapMode = tb.Tag?.ToString();
                var isEnabled = tb.IsChecked == true;
                // TODO: toggle snap mode in DrawingService/DrawingViewModel
            }
        }

        // ─────────────────────────────────────────────────────────────────
        // STATUS BAR — COMMAND / LOGS PANELS
        // ─────────────────────────────────────────────────────────────────

        private void ShowCommandPanel_Click(object sender, RoutedEventArgs e)
        {
            // TODO: show/collapse CmdLine history panel
            //CmdLine.Visibility = CmdLine.Visibility == Visibility.Collapsed
            //    ? Visibility.Visible
            //    : Visibility.Collapsed;
        }

        private void ShowLogsPanel_Click(object sender, RoutedEventArgs e)
        {
            // TODO: open a floating logs window or panel
        }

        // ─────────────────────────────────────────────────────────────────
        // PUBLIC HELPERS — called by services
        // ─────────────────────────────────────────────────────────────────

        /// <summary>Updates the X / Y coordinate readout in the status bar.</summary>
        public void SetCoordinates(double x, double y)
        {
            TxtCoordX.Text = $"{x:F2}M";
            TxtCoordY.Text = $"{y:F2}M";
        }

        /// <summary>
        /// Shows / hides the no-model placeholder overlay.
        /// Called by the IFC loader when a model is successfully parsed (loaded=true)
        /// or when the scene is explicitly cleared (loaded=false).
        /// </summary>
        public void SetModelLoaded(bool loaded)
        {
            // loaded=true  → IFC is in the scene → hide the "no model" notice
            // loaded=false → scene cleared       → show the notice again
            NoModelOverlay.Visibility = loaded ? Visibility.Collapsed : Visibility.Visible;
        }

        /// <summary>Updates the status bar breadcrumb after an action.</summary>
        public void SetLastAction(string description)
        {
            TxtHistoryBreadcrumb.Text = description;
        }

        /// <summary>Updates the IDLE / active status indicator.</summary>
        public void SetStatus(string text)
        {
            StatusText.Text = text;
        }

        /// <summary>Shows / hides the drawing options bar (called by DrawingService).</summary>
        public void SetDrawingOptionsBarVisible(bool visible)
        {
            //DrawingOptionsBar.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
            //UndoRedoToolbar.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
        }
    }
}
