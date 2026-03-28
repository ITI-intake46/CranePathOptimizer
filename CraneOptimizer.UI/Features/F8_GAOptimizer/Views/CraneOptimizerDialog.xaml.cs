using System.Windows;
using System.Windows.Controls;

namespace CraneOptimizer.UI.Features.F8_GAOptimizer.Views
{
    public partial class CraneOptimizerDialog : Window
    {
        public CraneOptimizerDialog()
        {
            InitializeComponent();
        }

        private void CboCrane_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            TxtChartStatus.Text = CboCrane.SelectedItem != null
                ? "✓ Load chart assigned"
                : string.Empty;
        }

        private void RbExclusion_Checked(object sender, RoutedEventArgs e)
        {
            if (TxtExcInfo == null) return;
            TxtExcInfo.Text = RbExcIgnore?.IsChecked == true
                ? "⚠ Crane may overlap structures."
                : string.Empty;
        }

        // ── Element quick-filters ─────────────────────────────────────────
        private void BtnIncludeAll_Click(object sender, RoutedEventArgs e)  { /* TODO */ }
        private void BtnExcludeAll_Click(object sender, RoutedEventArgs e)  { /* TODO */ }
        private void BtnNoSlabs_Click(object sender, RoutedEventArgs e)     { /* TODO */ }
        private void BtnNoStairs_Click(object sender, RoutedEventArgs e)    { /* TODO */ }
        private void BtnNoRoofs_Click(object sender, RoutedEventArgs e)     { /* TODO */ }

        // ── Run / Cancel / Apply ─────────────────────────────────────────
        private async void BtnRun_Click(object sender, RoutedEventArgs e)
        {
            BtnRun.IsEnabled    = false;
            BtnCancel.IsEnabled = true;
            BtnApply.IsEnabled  = false;
            TxtStatus.Text      = "Running optimizer…";
            ProgBar.Value       = 0;

            // TODO: call IGeneticAlgorithmEngine via injected service
            // Simulated progress placeholder:
            await System.Threading.Tasks.Task.Delay(100);

            TxtStatus.Text      = "Ready";
            BtnRun.IsEnabled    = true;
            BtnCancel.IsEnabled = false;
            BtnApply.IsEnabled  = true;
            PanelResults.Visibility = Visibility.Visible;
        }

        private void BtnCancelRun_Click(object sender, RoutedEventArgs e)
        {
            // TODO: cancel running GA token
            BtnRun.IsEnabled    = true;
            BtnCancel.IsEnabled = false;
            TxtStatus.Text      = "Cancelled.";
        }

        private void BtnApply_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
            Close();
        }
    }
}
