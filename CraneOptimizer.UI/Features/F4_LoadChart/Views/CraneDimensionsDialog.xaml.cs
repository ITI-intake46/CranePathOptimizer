using CraneOptimizer.Domain.Units;
using CraneOptimizer.UI.Features.F4_LoadChart.Services;
using System.Globalization;
using System.Windows;

namespace CraneOptimizer.UI.Features.F4_LoadChart.Views
{
    public partial class CraneDimensionsDialog : Window
    {
        public double VehicleLengthMeters { get; private set; }
        public double VehicleHeightMeters { get; private set; }
        public double VehicleWidthMeters { get; private set; }
        public double? OutriggerWidthMeters { get; private set; }
        public double? OutriggerLengthMeters { get; private set; }
        private readonly UnitSettings _unitSettings;

        public CraneDimensionsDialog(
            double vehicleLengthMeters,
            double vehicleHeightMeters,
            double vehicleWidthMeters,
            double? outriggerWidthMeters,
            double? outriggerLengthMeters)
        {
            InitializeComponent();
            _unitSettings = App.ServiceProvider.GetService(typeof(IUnitSettingsService)) is IUnitSettingsService unitSettingsService
                ? unitSettingsService.Current
                : UnitSettings.Default;

            VehicleLengthMeters = vehicleLengthMeters;
            VehicleHeightMeters = vehicleHeightMeters;
            VehicleWidthMeters = vehicleWidthMeters;
            OutriggerWidthMeters = outriggerWidthMeters;
            OutriggerLengthMeters = outriggerLengthMeters;

            ApplyUnitLabels();
            TxtVehicleLength.Text = FormatLengthInput(vehicleLengthMeters);
            TxtVehicleHeight.Text = FormatLengthInput(vehicleHeightMeters);
            TxtVehicleWidth.Text = FormatLengthInput(vehicleWidthMeters);
            TxtOutriggerWidth.Text = FormatLengthInput(outriggerWidthMeters);
            TxtOutriggerLength.Text = FormatLengthInput(outriggerLengthMeters);
        }

        private void BtnApply_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                VehicleLengthMeters = ParseLengthOrZero(TxtVehicleLength.Text, "Total Length");
                VehicleHeightMeters = ParseLengthOrZero(TxtVehicleHeight.Text, "Transport Height");
                VehicleWidthMeters = ParseLengthOrZero(TxtVehicleWidth.Text, "Travel Width");
                OutriggerWidthMeters = TryParseOptionalLength(TxtOutriggerWidth.Text, "Outrigger Span", out double width) ? width : null;
                OutriggerLengthMeters = TryParseOptionalLength(TxtOutriggerLength.Text, "Span to Span", out double length) ? length : null;

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

        private void ApplyUnitLabels()
        {
            var symbol = UnitSystem.LengthSymbol(_unitSettings.DisplayLengthUnit);
            LblVehicleLength.Text = $"Total Length ({symbol})";
            LblVehicleHeight.Text = $"Transport Height ({symbol})";
            LblVehicleWidth.Text = $"Travel Width ({symbol})";
            LblOutriggerWidth.Text = $"Outrigger Span (pad to pad) ({symbol})";
            LblOutriggerLength.Text = $"Span to Span (front to rear) ({symbol})";
        }

        private double ParseLengthOrZero(string text, string fieldName)
        {
            if (string.IsNullOrWhiteSpace(text))
                return 0d;

            if (LoadChartCsvService.TryParseFlexibleNumber(text, out double value))
                return UnitSystem.ToMeters(value, _unitSettings.DisplayLengthUnit);

            throw new FormatException($"{fieldName} value '{text}' is invalid.");
        }

        private bool TryParseOptionalLength(string text, string fieldName, out double value)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                value = 0d;
                return false;
            }

            if (LoadChartCsvService.TryParseFlexibleNumber(text, out value))
            {
                value = UnitSystem.ToMeters(value, _unitSettings.DisplayLengthUnit);
                return true;
            }

            throw new FormatException($"{fieldName} value '{text}' is invalid.");
        }

        private string FormatLengthInput(double? value)
        {
            return value.HasValue && value.Value > 0
                ? UnitSystem.FromMeters(value.Value, _unitSettings.DisplayLengthUnit)
                    .ToString("0.###", CultureInfo.InvariantCulture)
                : string.Empty;
        }
    }
}
