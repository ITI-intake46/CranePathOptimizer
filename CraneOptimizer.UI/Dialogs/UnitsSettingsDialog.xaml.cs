using System;
using System.Windows;
using System.Windows.Controls;
using WPF_IFC_test1.Utilities;

namespace WPF_IFC_test1.Dialogs
{
    /// <summary>
    /// Units Settings dialog — opened by the 'UN' command in the CommandLine,
    /// or from any menu/ribbon button that calls <see cref="Open"/>.
    ///
    /// When the user clicks "Save &amp; Apply":
    ///   1. Calls <see cref="UnitSystem.ApplySettings"/> with the chosen values.
    ///   2. UnitSystem fires its <see cref="UnitSystem.Changed"/> event.
    ///   3. Every subscribed UI element refreshes its displayed values automatically.
    /// </summary>
    public partial class UnitsSettingsDialog : Window
    {
        // ── Static entry point ────────────────────────────────────────────────

        /// <summary>
        /// Opens the dialog as a modal window.
        /// Call this from anywhere — CommandLine, ribbon button, menu item, etc.
        /// </summary>
        /// <param name="owner">Parent window for centering. Can be null.</param>
        public static void Open(Window owner = null)
        {
            var dlg = new UnitsSettingsDialog();
            if (owner != null) dlg.Owner = owner;
            dlg.ShowDialog();
        }

        // ── Constructor ───────────────────────────────────────────────────────

        public UnitsSettingsDialog()
        {
            InitializeComponent();
            PopulateComboBoxes();
            LoadCurrentSettings();
            UpdatePreview();
        }

        // ── Populate combos ───────────────────────────────────────────────────

        private void PopulateComboBoxes()
        {
            // Length units
            foreach (LengthUnit u in Enum.GetValues<LengthUnit>())
                CboLength.Items.Add(new ComboBoxItem
                {
                    Content = UnitSystem.GetDisplayName(u),
                    Tag = u
                });

            // Area units
            foreach (AreaUnit u in Enum.GetValues<AreaUnit>())
                CboArea.Items.Add(new ComboBoxItem
                {
                    Content = UnitSystem.GetDisplayName(u),
                    Tag = u
                });

            // Precision (0–4 decimal places)
            for (int i = 0; i <= 4; i++)
                CboPrecision.Items.Add(new ComboBoxItem
                {
                    Content = i == 0 ? "0  (whole numbers)" : $"{i}  (e.g. {(1.0).ToString("F" + i)})",
                    Tag = i
                });

            // Display format
            foreach (DisplayFormat f in Enum.GetValues<DisplayFormat>())
                CboFormat.Items.Add(new ComboBoxItem
                {
                    Content = UnitSystem.GetDisplayName(f),
                    Tag = f
                });
        }

        // ── Load current UnitSystem state into combos ─────────────────────────

        private void LoadCurrentSettings()
        {
            SelectByTag(CboLength, UnitSystem.CurrentUnit);
            SelectByTag(CboArea, UnitSystem.CurrentArea);
            SelectByTag(CboPrecision, UnitSystem.Precision);
            SelectByTag(CboFormat, UnitSystem.Format_Style);
        }

        private static void SelectByTag(ComboBox cbo, object tag)
        {
            foreach (ComboBoxItem item in cbo.Items)
            {
                if (item.Tag?.Equals(tag) == true)
                {
                    cbo.SelectedItem = item;
                    return;
                }
            }
            if (cbo.Items.Count > 0)
                cbo.SelectedIndex = 0;
        }

        // ── Live preview ──────────────────────────────────────────────────────

        private void UpdatePreview()
        {
            // Sample values: 12.345 m length, 250.75 m² area
            const double sampleLength = 12.345;
            const double sampleArea = 250.75;

            var unit = GetSelected<LengthUnit>(CboLength);
            var area = GetSelected<AreaUnit>(CboArea);
            int prec = GetSelected<int>(CboPrecision);
            var fmt = GetSelected<DisplayFormat>(CboFormat);

            // Temporarily compute without changing UnitSystem global state
            double lenConverted = UnitSystem.ConvertFromMetres(sampleLength, unit);
            string lenSymbol = UnitSystem.GetSymbol(unit);
            double areaConverted = UnitSystem.ConvertAreaFromSqMetres(sampleArea, area);
            string areaSymbol = UnitSystem.GetAreaSymbol(area);

            string lenStr = Compose(lenConverted, lenSymbol, prec, fmt);
            string areaStr = Compose(areaConverted, areaSymbol, prec, fmt);

            TxtPreview.Text = lenStr;
            TxtPreviewArea.Text = areaStr;
        }

        private static string Compose(double value, string symbol, int precision, DisplayFormat fmt)
        {
            string num = value.ToString("F" + precision, System.Globalization.CultureInfo.InvariantCulture);
            return fmt switch
            {
                DisplayFormat.ValueOnly => num,
                DisplayFormat.SymbolThenValue => $"{symbol} {num}",
                _ => $"{num} {symbol}"
            };
        }

        private static T GetSelected<T>(ComboBox cbo)
        {
            if (cbo.SelectedItem is ComboBoxItem item && item.Tag is T val)
                return val;
            return default;
        }

        // ── Event handlers ────────────────────────────────────────────────────

        private void CboLength_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // Auto-suggest matching area unit when length changes
            var lengthUnit = GetSelected<LengthUnit>(CboLength);
            var suggestedArea = lengthUnit switch
            {
                LengthUnit.Feet => AreaUnit.SquareFeet,
                LengthUnit.Centimetres => AreaUnit.SquareCentimetres,
                _ => AreaUnit.SquareMetres
            };
            SelectByTag(CboArea, suggestedArea);

            UpdatePreview();
        }

        private void OnAnyComboChanged(object sender, SelectionChangedEventArgs e)
            => UpdatePreview();

        private void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            UnitSystem.ApplySettings(
                unit: GetSelected<LengthUnit>(CboLength),
                area: GetSelected<AreaUnit>(CboArea),
                precision: GetSelected<int>(CboPrecision),
                displayFormat: GetSelected<DisplayFormat>(CboFormat)
            );

            DialogResult = true;
            Close();
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

    }
}