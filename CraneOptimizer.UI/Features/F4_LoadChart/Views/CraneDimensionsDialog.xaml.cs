using CraneOptimizer.UI.Features.F4_LoadChart.Services;
using System.Globalization;
using System.Windows;

namespace CraneOptimizer.UI.Features.F4_LoadChart.Views
{
    public partial class CraneDimensionsDialog : Window
    {
        public double VehicleLengthMm { get; private set; }
        public double VehicleHeightMm { get; private set; }
        public double VehicleWidthMm { get; private set; }
        public double? OutriggerWidthMm { get; private set; }
        public double? OutriggerLengthMm { get; private set; }

        public CraneDimensionsDialog(
            double vehicleLengthMm,
            double vehicleHeightMm,
            double vehicleWidthMm,
            double? outriggerWidthMm,
            double? outriggerLengthMm)
        {
            InitializeComponent();

            VehicleLengthMm = vehicleLengthMm;
            VehicleHeightMm = vehicleHeightMm;
            VehicleWidthMm = vehicleWidthMm;
            OutriggerWidthMm = outriggerWidthMm;
            OutriggerLengthMm = outriggerLengthMm;

            TxtVehicleLength.Text = FormatNumber(vehicleLengthMm);
            TxtVehicleHeight.Text = FormatNumber(vehicleHeightMm);
            TxtVehicleWidth.Text = FormatNumber(vehicleWidthMm);
            TxtOutriggerWidth.Text = FormatNumber(outriggerWidthMm);
            TxtOutriggerLength.Text = FormatNumber(outriggerLengthMm);
        }

        private void BtnApply_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                VehicleLengthMm = ParseNumberOrZero(TxtVehicleLength.Text, "Total Length");
                VehicleHeightMm = ParseNumberOrZero(TxtVehicleHeight.Text, "Transport Height");
                VehicleWidthMm = ParseNumberOrZero(TxtVehicleWidth.Text, "Travel Width");
                OutriggerWidthMm = TryParseOptionalNumber(TxtOutriggerWidth.Text, "Outrigger Span", out double width) ? width : null;
                OutriggerLengthMm = TryParseOptionalNumber(TxtOutriggerLength.Text, "Span to Span", out double length) ? length : null;

                DialogResult = true;
                Close();
            }
            catch (FormatException ex)
            {
                TxtStatus.Text = ex.Message;
                MessageBox.Show(this, ex.Message, "Invalid Dimension", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private static double ParseNumberOrZero(string text, string fieldName)
        {
            if (string.IsNullOrWhiteSpace(text))
                return 0d;

            if (LoadChartCsvService.TryParseFlexibleNumber(text, out double value))
                return value;

            throw new FormatException($"{fieldName} value '{text}' is invalid.");
        }

        private static bool TryParseOptionalNumber(string text, string fieldName, out double value)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                value = 0d;
                return false;
            }

            if (LoadChartCsvService.TryParseFlexibleNumber(text, out value))
                return true;

            throw new FormatException($"{fieldName} value '{text}' is invalid.");
        }

        private static string FormatNumber(double? value)
        {
            return value.HasValue && value.Value > 0
                ? value.Value.ToString("F0", CultureInfo.InvariantCulture)
                : string.Empty;
        }
    }
}
