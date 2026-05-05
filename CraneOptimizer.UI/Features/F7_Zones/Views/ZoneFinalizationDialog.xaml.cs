using System.Windows;

namespace CraneOptimizer.UI.Features.F7_Zones.Views
{
    public partial class ZoneFinalizationDialog : Window
    {
        // ── Result properties ─────────────────────────────────────────────
        // Use ...Value names so they don't shadow Window.MinHeight/MaxHeight
        // (which are the resize-bound dependency properties on FrameworkElement).
        public string ZoneName { get; private set; } = string.Empty;
        public string ZoneType { get; private set; } = "Allowed";
        public double MinHeightValue { get; private set; } = 0;
        public double MaxHeightValue { get; private set; } = 999;

        public ZoneFinalizationDialog(double areaM2 = 0, string initialZoneType = "Allowed")
        {
            InitializeComponent();
            AreaLabel.Text = areaM2 > 0
                ? $"Area: {areaM2:F2} m²"
                : string.Empty;
            var isAllowed = string.Equals(initialZoneType, "Allowed", System.StringComparison.OrdinalIgnoreCase);
            RadioAllowed.IsChecked = isAllowed;
            RadioForbidden.IsChecked = !isAllowed;
            ZoneType = isAllowed ? "Allowed" : "Forbidden";
        }

        private void ZoneType_Changed(object sender, RoutedEventArgs e)
            => ZoneType = RadioAllowed.IsChecked == true ? "Allowed" : "Forbidden";

        private void TxtZoneName_GotFocus(object sender, RoutedEventArgs e)
            => ZoneNamePlaceholder.Visibility = Visibility.Collapsed;

        private void TxtZoneName_LostFocus(object sender, RoutedEventArgs e)
            => ZoneNamePlaceholder.Visibility = string.IsNullOrEmpty(TxtZoneName.Text)
                ? Visibility.Visible
                : Visibility.Collapsed;

        private void Confirm_Click(object sender, RoutedEventArgs e)
        {
            ErrorLabel.Text = string.Empty;
            if (!double.TryParse(TxtMinHeight.Text, out double min) ||
                !double.TryParse(TxtMaxHeight.Text, out double max) ||
                min >= max)
            {
                ErrorLabel.Text = "Invalid height range. Min must be less than Max.";
                return;
            }

            ZoneName = TxtZoneName.Text.Trim();
            MinHeightValue = min;
            MaxHeightValue = max;
            ZoneType = RadioAllowed.IsChecked == true ? "Allowed" : "Forbidden";
            DialogResult = true;
            Close();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}