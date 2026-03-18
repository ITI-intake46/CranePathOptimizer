using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using WPF_IFC_test1.ZoneModule.Drawing;

namespace WPF_IFC_test1.Main_Application_UserControl.Ribbon_Tabs
{
    public partial class ZoneRibbonTab : UserControl
    {
        // ── Events wired by MainRibbonControl ──────────────────────────────
        public event Action<ZoneTabEventArgs> OnZoneAction;
        public event Action OnEnterPlanView;
        public event Action OnReturnTo3D;

        // ── Active mode tracking ────────────────────────────────────────────
        // FIX: Track which mode is active so Escape knows what to cancel,
        //      and so switching modes deactivates the previous button.
        private enum ActiveMode { None, Drawing, Measuring }
        private ActiveMode _currentMode = ActiveMode.None;

        public ZoneRibbonTab() => InitializeComponent();

        private void Emit(string action, object param = null)
            => OnZoneAction?.Invoke(new ZoneTabEventArgs(action, param));

        // ── Escape key handler ─────────────────────────────────────────────
        // FIX: Escape now cancels whatever is active (drawing OR measuring).
        // The ribbon tab must be Focusable="True" (set in XAML) and the
        // viewport must route PreviewKeyDown to this.  MainWindow should also
        // call CancelCurrentMode() from its Window_KeyDown handler for
        // reliable capture even when focus is in the 3D viewport.
        private void UserControl_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                CancelCurrentMode();
                e.Handled = true;
            }
        }

        /// <summary>
        /// Public entry-point so MainWindow can forward Escape from the viewport.
        /// Call this from MainWindow.Window_KeyDown when e.Key == Key.Escape.
        /// </summary>
        public void CancelCurrentMode()
        {
            switch (_currentMode)
            {
                case ActiveMode.Drawing:
                    CancelDrawing();
                    break;
                case ActiveMode.Measuring:
                    CancelMeasure();
                    break;
            }
        }

        // ── Draw mode buttons ──────────────────────────────────────────────
        // FIX: Each draw button is now a ToggleButton (in XAML).
        //      Clicking one deactivates the others.
        //      Clicking the same one again cancels the mode.

        private void BtnSiteBoundary_Click(object s, RoutedEventArgs e)
        {
            if (BtnSiteBoundary.IsChecked == false)
            {
                // User un-toggled — cancel
                CancelDrawing();
                return;
            }
            ActivateDrawMode(BtnSiteBoundary);
            ShowDrawTools(isSiteBoundary: true);
            Emit("DrawSiteBoundary");
        }

        private void BtnDrawAllowed_Click(object s, RoutedEventArgs e)
        {
            if (BtnDrawAllowed.IsChecked == false)
            {
                CancelDrawing();
                return;
            }
            ActivateDrawMode(BtnDrawAllowed);
            ShowDrawTools(isSiteBoundary: false);
            Emit("DrawAllowed");
        }

        private void BtnDrawForbidden_Click(object s, RoutedEventArgs e)
        {
            if (BtnDrawForbidden.IsChecked == false)
            {
                CancelDrawing();
                return;
            }
            ActivateDrawMode(BtnDrawForbidden);
            ShowDrawTools(isSiteBoundary: false);
            Emit("DrawForbidden");
        }

        // Enforce mutual exclusion among the three draw buttons + measure
        private void ActivateDrawMode(ToggleButton active)
        {
            _currentMode = ActiveMode.Drawing;

            // Deactivate all other mode buttons
            foreach (var btn in new ToggleButton[] { BtnSiteBoundary, BtnDrawAllowed, BtnDrawForbidden })
                if (!ReferenceEquals(btn, active)) btn.IsChecked = false;

            // Also cancel any active measure
            BtnMeasure.IsChecked = false;
            MeasureLabel.Text = "Measure";
        }

        private void CancelDrawing()
        {
            _currentMode = ActiveMode.None;
            BtnSiteBoundary.IsChecked = false;
            BtnDrawAllowed.IsChecked = false;
            BtnDrawForbidden.IsChecked = false;
            HideDrawTools();
            Emit("CancelDraw");
        }

        // ── Show / hide draw tools panel ───────────────────────────────────
        public void ShowDrawTools(bool isSiteBoundary = false)
        {
            DrawToolsPanel.Visibility = Visibility.Visible;
        }

        /// <summary>Call from ZoneModuleController when drawing is finished/cancelled externally.</summary>
        public void HideDrawTools()
        {
            DrawToolsPanel.Visibility = Visibility.Collapsed;
            // Also reset draw buttons if called from outside (e.g. zone finished)
            BtnSiteBoundary.IsChecked = false;
            BtnDrawAllowed.IsChecked = false;
            BtnDrawForbidden.IsChecked = false;
            if (_currentMode == ActiveMode.Drawing)
                _currentMode = ActiveMode.None;
        }

        /// <summary>
        /// Called by MainWindow whenever the site boundary state changes.
        /// Site boundary is no longer a prerequisite — Allowed/Forbidden buttons are always enabled.
        /// </summary>
        public void SetSiteBoundaryExists(bool exists)
        {
            // Site boundary is no longer required; keep buttons always enabled.
            BtnDrawAllowed.IsEnabled = true;
            BtnDrawForbidden.IsEnabled = true;
            BtnDrawAllowed.ToolTip = "Draw an Allowed (green) zone";
            BtnDrawForbidden.ToolTip = "Draw a Forbidden (red) zone";
        }

        // ── Measure tool ───────────────────────────────────────────────────
        // FIX: Measure is now a ToggleButton.
        //      - First click  → enter measure mode (controller picks two points)
        //      - Second click or Esc → exit measure mode, clear geometry from viewport
        //      - Distance result comes back via MeasureCompleted event and is shown
        //        as a floating label in the viewport (not permanent geometry).
        //      - The label/line is cleared when any other mode starts or Esc pressed.

        /// <summary>
        /// Raised when measure mode is activated. MainWindow should enable pick-two-points mode.
        /// </summary>
        public event Action OnStartMeasure;

        /// <summary>
        /// Raised when measure mode is cancelled/deactivated.
        /// MainWindow must remove the temporary measure geometry AND label from the viewport.
        /// </summary>
        public event Action OnCancelMeasure;

        private void BtnMeasure_Click(object s, RoutedEventArgs e)
        {
            if (BtnMeasure.IsChecked == true)
            {
                // Entering measure mode — cancel any active drawing first
                if (_currentMode == ActiveMode.Drawing)
                    CancelDrawing();

                _currentMode = ActiveMode.Measuring;
                MeasureLabel.Text = "Measure";
                OnStartMeasure?.Invoke();
            }
            else
            {
                CancelMeasure();
            }
        }

        private void CancelMeasure()
        {
            _currentMode = ActiveMode.None;
            BtnMeasure.IsChecked = false;
            MeasureLabel.Text = "Measure";
            // FIX: Tell the viewport to remove both the temporary line AND the distance label
            OnCancelMeasure?.Invoke();
            Emit("CancelMeasure");
        }

        /// <summary>
        /// Called by MainWindow / viewport controller when two points have been picked.
        /// Shows the distance on the button label.
        /// The viewport controller is responsible for:
        ///   1. Drawing a temporary dashed line between the two points.
        ///   2. Showing a floating TextBlock (NOT permanent geometry) at the midpoint
        ///      with the distance string.
        ///   3. Removing BOTH when OnCancelMeasure fires or a new mode starts.
        /// </summary>
        public void ShowMeasureResult(double distanceMetres)
        {
            // FIX: Show the numeric result on the button itself
            string label = distanceMetres >= 1.0
                ? $"{distanceMetres:F2} m"
                : $"{distanceMetres * 100:F1} cm";

            MeasureLabel.Text = label;

            // Stay in measure mode so the user can see the result,
            // but emit the result so viewport shows the floating label.
            Emit("MeasureResult", distanceMetres);
        }

        // ── Shape tool selection ───────────────────────────────────────────
        private void ShapeTool_Click(object sender, RoutedEventArgs e)
        {
            if (sender is ToggleButton btn && btn.Tag is string tool)
            {
                btn.IsChecked = true;
                foreach (var tb in new[] { BtnPolyline, BtnRect, BtnCircle, BtnPickObjects })
                    if (!ReferenceEquals(tb, btn)) tb.IsChecked = false;

                string action = tool switch
                {
                    "Polyline" => "SetToolPolyline",
                    "Rectangle" => "SetToolRectangle",
                    "Circle" => "SetToolCircle",
                    "PickObjects" => "SetToolPickObjects",
                    _ => null
                };
                if (action != null) Emit(action);
            }
        }

        // ── OSNAP toggles ──────────────────────────────────────────────────
        private void Osnap_Click(object sender, RoutedEventArgs e)
        {
            if (sender is ToggleButton btn && btn.Tag is string snapName)
            {
                bool enabled = btn.IsChecked == true;
                Emit("OsnapToggle", new OsnapToggleArg(snapName, enabled));
            }
        }

        // ── Action buttons ─────────────────────────────────────────────────
        private void BtnFinishZone_Click(object s, RoutedEventArgs e) => Emit("FinishZone");
        private void BtnUndoVertex_Click(object s, RoutedEventArgs e) => Emit("UndoVertex");

        private void BtnCancelDraw_Click(object s, RoutedEventArgs e)
        {
            // FIX: Cancel button = same as Escape in drawing mode
            CancelCurrentMode();
        }

        private void BtnDeleteZone_Click(object s, RoutedEventArgs e) => Emit("DeleteZone");
        private void BtnDeleteAllZones_Click(object s, RoutedEventArgs e) => Emit("DeleteAllZones");
        private void BtnExportZones_Click(object s, RoutedEventArgs e) => Emit("ExportZones");

        private void BtnPlanView_Click(object s, RoutedEventArgs e) => OnEnterPlanView?.Invoke();
        private void BtnReturn3D_Click(object s, RoutedEventArgs e) => OnReturnTo3D?.Invoke();

        // Called by MainRibbonControl to pass ZoneManager — stub for now
        public void SetZoneManager(object zm) { }
    }

    /// <summary>Payload for OSNAP toggle events.</summary>
    public class OsnapToggleArg
    {
        public string SnapName { get; }
        public bool Enabled { get; }
        public OsnapToggleArg(string name, bool enabled) { SnapName = name; Enabled = enabled; }
    }
}