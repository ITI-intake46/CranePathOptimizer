using System.Windows;
using System.Windows.Controls;

namespace CraneOptimizer.UI.Features.F4_LoadChart.Views
{
    public partial class CraneCatalogBrowserDialog : Window
    {
        public CraneCatalogBrowserDialog()
        {
            InitializeComponent();
            Loaded += (_, _) => TxtStatus.Text = "Catalog ready.";
        }

        private void TxtSearch_TextChanged(object sender, TextChangedEventArgs e)
        {
            // TODO: filter CatalogListPanel items by search text
        }

        private void BtnAssign_Click(object sender, RoutedEventArgs e)
        {
            // TODO: assign selected chart to active crane
            DialogResult = true;
        }

        private void BtnImport_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new LoadChartImportDialog { Owner = this };
            dlg.ShowDialog();
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e) => Close();
    }
}
