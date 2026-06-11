using CraneOptimizer.Domain.Units;
using CraneOptimizer.UI.Features.F4_LoadChart.Services;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;

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
        private readonly bool _isOnRubber;

        public CraneDimensionsDialog(
            double vehicleLengthMeters,
            double vehicleHeightMeters,
            double vehicleWidthMeters,
            double? outriggerWidthMeters,
            double? outriggerLengthMeters,
            bool isOnRubber)
        {
            InitializeComponent();
            _unitSettings = App.ServiceProvider.GetService(typeof(IUnitSettingsService)) is IUnitSettingsService unitSettingsService
                ? unitSettingsService.Current
                : UnitSettings.Default;
            _isOnRubber = isOnRubber;

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

            OutriggerFieldsPanel.Visibility = _isOnRubber ? Visibility.Collapsed : Visibility.Visible;
            OnRubberNotice.Visibility = _isOnRubber ? Visibility.Visible : Visibility.Collapsed;
        }

        private void BtnApply_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                VehicleLengthMeters = ParseRequiredLength(TxtVehicleLength, "Total Length");
                VehicleHeightMeters = ParseRequiredLength(TxtVehicleHeight, "Transport Height");
                VehicleWidthMeters = ParseRequiredLength(TxtVehicleWidth, "Travel Width");

                if (_isOnRubber)
                {
                    OutriggerWidthMeters = null;
                    OutriggerLengthMeters = null;
                }
                else
                {
                    OutriggerWidthMeters = ParseRequiredLength(TxtOutriggerWidth, "Outrigger Span");
                    OutriggerLengthMeters = ParseRequiredLength(TxtOutriggerLength, "Span to Span");
                }

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
            LblVehicleLength.Text = $"Total Length * ({symbol})";
            LblVehicleHeight.Text = $"Transport Height * ({symbol})";
            LblVehicleWidth.Text = $"Travel Width * ({symbol})";
            LblOutriggerWidth.Text = $"Outrigger Span (pad to pad) * ({symbol})";
            LblOutriggerLength.Text = $"Span to Span (front to rear) * ({symbol})";
        }

        private double ParseRequiredLength(TextBox textBox, string fieldName)
        {
            string text = textBox.Text;
            if (string.IsNullOrWhiteSpace(text))
            {
                textBox.Focus();
                throw new FormatException($"{fieldName} is required.");
            }

            if (LoadChartCsvService.TryParseUserEnteredNumber(text, out double value))
            {
                double meters = UnitSystem.ToMeters(value, _unitSettings.DisplayLengthUnit);
                if (double.IsFinite(meters) && meters > 0d)
                    return meters;
            }

            textBox.Focus();
            throw new FormatException($"{fieldName} must be a valid value greater than zero.");
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
