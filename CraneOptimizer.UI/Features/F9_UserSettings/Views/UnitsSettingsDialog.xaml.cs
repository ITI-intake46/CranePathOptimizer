using CraneOptimizer.Domain.Units;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;

namespace CraneOptimizer.UI.Features.F9_Settings.Views
{
    public partial class UnitsSettingsDialog : Window
    {
        private readonly IUnitSettingsService? _unitSettingsService;
        private UnitSettings _settings;

        public UnitsSettingsDialog()
        {
            _settings = UnitSettings.Default.Clone();
            InitializeComponent();
            LoadOptions();
        }

        public UnitsSettingsDialog(IUnitSettingsService unitSettingsService)
        {
            _unitSettingsService = unitSettingsService;
            _settings = unitSettingsService.Current;
            InitializeComponent();
            LoadOptions();
        }

        public Guid? ProjectId { get; set; }

        private void LoadOptions()
        {
            CboLength.ItemsSource    = new[] { "Millimetres (mm)", "Metres (m)" };
            CboArea.ItemsSource      = new[] { "m²", "ft²" };
            CboMass.ItemsSource      = new[] { "Kilograms (kg)", "Tons (t)" };
            CboPrecision.ItemsSource = new[] { "0", "1", "2", "3" };
            CboFormat.ItemsSource    = new[] { "1.23 m", "1m 23cm", "1'-2\"" };

            CboLength.SelectedIndex    = _settings.DisplayLengthUnit == LengthUnit.Millimeter ? 0 : 1;
            CboArea.SelectedIndex      = (int)_settings.DisplayAreaUnit;
            CboMass.SelectedIndex      = (int)_settings.DisplayMassUnit;
            CboPrecision.SelectedIndex = Math.Clamp(_settings.DecimalPlaces, 0, 3);
            CboFormat.SelectedIndex    = 0;

            UpdatePreview();
        }

        private void OnAnyComboChanged(object sender, SelectionChangedEventArgs e) => UpdatePreview();

        private void UpdatePreview()
        {
            var settings = ReadSettings();
            TxtPreview.Text = UnitSystem.FormatLength(12.34, settings, CultureInfo.InvariantCulture);
            TxtPreviewArea.Text = UnitSystem.FormatArea(152.0, settings, CultureInfo.InvariantCulture);
        }

        private async void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            _settings = ReadSettings();
            if (_unitSettingsService != null && ProjectId.HasValue)
                await _unitSettingsService.SaveProjectSettingsAsync(ProjectId.Value, _settings);
            else
                _unitSettingsService?.ApplyRuntimeSettings(_settings);

            DialogResult = true;
            Close();
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private UnitSettings ReadSettings()
        {
            return new UnitSettings
            {
                DisplayLengthUnit = CboLength.SelectedIndex == 0 ? LengthUnit.Millimeter : LengthUnit.Meter,
                DisplayAreaUnit = (AreaUnit)Math.Max(0, CboArea.SelectedIndex),
                DisplayMassUnit = (MassUnit)Math.Max(0, CboMass.SelectedIndex),
                DecimalPlaces = Math.Clamp(CboPrecision.SelectedIndex, 0, 6)
            };
        }
    }
}
