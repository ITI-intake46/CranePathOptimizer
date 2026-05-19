using System.Windows;
using System.Windows.Controls;

namespace CraneOptimizer.UI.Features.F9_Settings.Views
{
    public partial class UnitsSettingsDialog : Window
    {
        public UnitsSettingsDialog()
        {
            InitializeComponent();
            LoadOptions();
        }

        private void LoadOptions()
        {
            CboLength.ItemsSource    = new[] { "Metres (m)", "Feet (ft)", "Millimetres (mm)" };
            CboArea.ItemsSource      = new[] { "m²", "ft²" };
            CboPrecision.ItemsSource = new[] { "0", "1", "2", "3" };
            CboFormat.ItemsSource    = new[] { "1.23 m", "1m 23cm", "1'-2\"" };

            CboLength.SelectedIndex    = 0;
            CboArea.SelectedIndex      = 0;
            CboPrecision.SelectedIndex = 2;
            CboFormat.SelectedIndex    = 0;

            UpdatePreview();
        }

        private void OnAnyComboChanged(object sender, SelectionChangedEventArgs e) => UpdatePreview();

        private void UpdatePreview()
        {
            TxtPreview.Text     = "12.34 m";
            TxtPreviewArea.Text = "152.00 m²";
        }

        private void BtnSave_Click(object sender, RoutedEventArgs e)
        {
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
