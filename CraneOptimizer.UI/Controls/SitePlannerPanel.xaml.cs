using System.Windows;
using System.Windows.Controls;
using WPF_IFC_test1.Utilities;

namespace WPF_IFC_test1.Main_Application_UserControl
{
    public partial class SitePlannerPanel : UserControl
    {
        // ── Bubble crane events up to MainWindow ────────────────────────────
        public event RoutedEventHandler AddTowerCraneRequested;
        public event RoutedEventHandler AddMobileCraneRequested;
        public event RoutedEventHandler RemoveSelectedRequested;
        public event RoutedEventHandler RemoveAllRequested;

        public SitePlannerPanel()
        {
            InitializeComponent();
        }

        // ── Tab switching ────────────────────────────────────────────────────

        private void TabCranes_Click(object sender, RoutedEventArgs e)
        {
            TabCranes.IsChecked = true;
            TabZones.IsChecked = false;

            CranePanel.Visibility = Visibility.Visible;
            ZonePanel.Visibility = Visibility.Collapsed;
        }

        private void TabZones_Click(object sender, RoutedEventArgs e)
        {
            TabZones.IsChecked = true;
            TabCranes.IsChecked = false;

            ZonePanel.Visibility = Visibility.Visible;
            CranePanel.Visibility = Visibility.Collapsed;
        }

        // ── Public API (called from MainWindow) ──────────────────────────────

        public void SetCraneDataContext(object craneManager)
        {
            CranePanel.DataContext = craneManager;
        }

        public void RefreshCraneParameters(CraneInstance crane)
        {
            CranePanel.RefreshParameterPanel(crane);
            // Auto-switch to crane tab when a crane is selected
            TabCranes_Click(null, null);
        }

        public void SwitchToCranes() => TabCranes_Click(null, null);
        public void SwitchToZones() => TabZones_Click(null, null);

        // ── Forward crane events ─────────────────────────────────────────────

        private void CranePanel_AddTowerCraneRequested(object sender, RoutedEventArgs e)
            => AddTowerCraneRequested?.Invoke(sender, e);

        private void CranePanel_AddMobileCraneRequested(object sender, RoutedEventArgs e)
            => AddMobileCraneRequested?.Invoke(sender, e);

        private void CranePanel_RemoveSelectedRequested(object sender, RoutedEventArgs e)
            => RemoveSelectedRequested?.Invoke(sender, e);

        private void CranePanel_RemoveAllRequested(object sender, RoutedEventArgs e)
            => RemoveAllRequested?.Invoke(sender, e);

        private void CranePanel_CloseRequested(object sender, RoutedEventArgs e)
        {
            // Close just switches to zones tab instead of hiding the whole panel
            TabZones_Click(null, null);
        }
    }
}