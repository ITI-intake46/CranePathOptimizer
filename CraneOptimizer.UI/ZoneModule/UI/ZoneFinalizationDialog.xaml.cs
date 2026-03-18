using System;
using System.Windows;
using System.Windows.Controls;
using WPF_IFC_test1.ZoneModule.Models;

namespace WPF_IFC_test1.ZoneModule.UI
{
    public partial class ZoneFinalizationDialog : Window
    {
        public ZoneType SelectedZoneType { get; private set; }
        public string ZoneName { get; private set; }
        public double MinHeight { get; private set; }
        public double MaxHeight { get; private set; }

        public ZoneFinalizationDialog(ZoneType defaultType, double area)
        {
            InitializeComponent();

            SelectedZoneType = defaultType;
            AreaLabel.Text = $"Footprint area: {area:F2} m²";

            if (defaultType == ZoneType.Forbidden)
                RadioForbidden.IsChecked = true;
            else
                RadioAllowed.IsChecked = true;
        }

        // ── Zone type radio ───────────────────────────────────────────────────
        private void ZoneType_Changed(object sender, RoutedEventArgs e)
        {
            SelectedZoneType = RadioAllowed.IsChecked == true
                ? ZoneType.Allowed
                : ZoneType.Forbidden;
        }

        // ── Watermark placeholder behaviour ───────────────────────────────────
        private void TxtZoneName_GotFocus(object sender, RoutedEventArgs e)
            => ZoneNamePlaceholder.Visibility = Visibility.Collapsed;

        private void TxtZoneName_LostFocus(object sender, RoutedEventArgs e)
            => ZoneNamePlaceholder.Visibility =
                string.IsNullOrEmpty(TxtZoneName.Text)
                    ? Visibility.Visible
                    : Visibility.Collapsed;

        // ── Confirm ───────────────────────────────────────────────────────────
        private void Confirm_Click(object sender, RoutedEventArgs e)
        {
            ErrorLabel.Text = "";
            try
            {
                double minH = double.Parse(TxtMinHeight.Text);
                double maxH = double.Parse(TxtMaxHeight.Text);

                if (minH < 0)
                { ErrorLabel.Text = "Min height cannot be negative."; return; }
                if (maxH <= minH)
                { ErrorLabel.Text = "Max height must be greater than min height."; return; }

                MinHeight = minH;
                MaxHeight = maxH;
                ZoneName = TxtZoneName.Text.Trim();

                DialogResult = true;
                Close();
            }
            catch
            {
                ErrorLabel.Text = "Please enter valid numeric heights.";
            }
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}