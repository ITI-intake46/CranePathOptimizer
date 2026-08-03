using CraneOptimizer.UI.Features.F4_LoadChart.Views;
using CraneOptimizer.UI.Shell.ViewModels;
using System.Windows;
using System.Windows.Controls;

namespace CraneOptimizer.UI.Features.F3_CraneSetup.Views
{
    /// <summary>
    /// Interaction logic for CranesTabView.xaml
    /// </summary>
    public partial class CranesTabView : UserControl
    {
        public CranesTabView()
        {
            InitializeComponent();
        }

        private async void ImportLoadChart_Click(object sender, RoutedEventArgs e)
            => await OpenLoadChartImportAsync(
                this,
                DataContext as MainViewModel);

        public static async Task OpenLoadChartImportAsync(
            FrameworkElement ownerElement,
            MainViewModel? viewModel)
        {
            var dialog = new LoadChartImportDialog
            {
                Owner = Window.GetWindow(ownerElement)
            };

            if (dialog.ShowDialog() == true)
                await RefreshCranesAsync(viewModel);
        }

        private async void ViewSavedCharts_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new CraneCatalogBrowserDialog(isReadOnlyView: true)
            {
                Owner = Window.GetWindow(this)
            };

            dialog.ShowDialog();
            await RefreshCranesAsync();
        }

        private async void CraneCatalog_Click(object sender, RoutedEventArgs e)
            => await OpenCraneCatalogAsync(
                this,
                DataContext as MainViewModel);

        public static async Task OpenCraneCatalogAsync(
            FrameworkElement ownerElement,
            MainViewModel? viewModel)
        {
            var dialog = new CraneCatalogBrowserDialog(
                isReadOnlyView: false,
                onCraneUpdated: _ => RefreshCranesAsync(viewModel))
            {
                Owner = Window.GetWindow(ownerElement)
            };

            dialog.ShowDialog();
            await RefreshCranesAsync(viewModel);
        }

        private Task RefreshCranesAsync()
            => RefreshCranesAsync(DataContext as MainViewModel);

        private static async Task RefreshCranesAsync(MainViewModel? vm)
        {
            if (vm == null)
                return;

            await vm.ReloadCranesAsync();
            await vm.CraneManager.ReloadCranesAsync();
        }
    }
}
