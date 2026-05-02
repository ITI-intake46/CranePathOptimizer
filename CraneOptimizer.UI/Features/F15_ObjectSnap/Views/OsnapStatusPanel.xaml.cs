using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace CraneOptimizer.UI.Features.F15_ObjectSnap.Views
{
    /// <summary>
    /// Code-behind for OsnapStatusPanel.
    ///
    /// The "OSN ▾" button is a plain Button — clicking it ONLY opens/closes the popup.
    /// Master ON/OFF is handled by the ToggleButton INSIDE the popup header (bound to MasterEnabled).
    /// This fully decouples popup visibility from snap-active state.
    /// </summary>
    public partial class OsnapStatusPanel : UserControl
    {
        public OsnapStatusPanel()
        {
            InitializeComponent();
            // After the DataContext is set (from MainWindow code-behind) refresh the button colour
            DataContextChanged += (_, _) => RefreshButtonAppearance();
        }

        // ─── Button click: open/close the popup only ──────────────────────────
        private void OsnLaunchBtn_Click(object sender, RoutedEventArgs e)
        {
            OsnapPopup.IsOpen = !OsnapPopup.IsOpen;
        }

        // ─── Popup closed: nothing special needed (StaysOpen=False handles it) ─
        private void OsnapPopup_Closed(object sender, EventArgs e)
        {
            // Keep button colour accurate after closing
            RefreshButtonAppearance();
        }

        /// <summary>
        /// Colours the OSN button text gold when any snap is active, grey when all are off.
        /// Called after DataContext is assigned and after popup closes.
        /// </summary>
        private void RefreshButtonAppearance()
        {
            if (DataContext is ViewModels.OsnapPanelViewModel vm)
            {
                var active = vm.IsAnySnapActive;
                OsnBtnText.Foreground = active
                    ? (Brush)new SolidColorBrush(Color.FromRgb(0xFF, 0xBB, 0x00))  // gold
                    : (Brush)FindResource("TextDisabledBrush");
            }
        }
    }
}
