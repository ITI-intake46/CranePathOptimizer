using CraneOptimizer.Domain.Interfaces;
using CraneOptimizer.UI.Features.F4_LoadChart.Views;
using CraneOptimizer.UI.Shell.ViewModels;
using Microsoft.Extensions.DependencyInjection;
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
        {
            var dialog = new LoadChartImportDialog
            {
                Owner = Window.GetWindow(this)
            };

            if (dialog.ShowDialog() == true)
                await RefreshCranesAsync();
        }

        private async void ViewSavedCharts_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new CraneCatalogBrowserDialog
            {
                Owner = Window.GetWindow(this)
            };

            dialog.ShowDialog();
            await RefreshCranesAsync();
        }

        private async void CraneCatalog_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new CraneCatalogBrowserDialog
            {
                Owner = Window.GetWindow(this)
            };

            dialog.ShowDialog();
            await RefreshCranesAsync();
        }

        private async Task RefreshCranesAsync()
        {
            if (DataContext is not MainViewModel vm)
                return;

            var repository = App.ServiceProvider.GetRequiredService<ICraneRepository>();
            var cranes = await repository.GetAllWithSpecsAsync();

            vm.Cranes.Clear();
            foreach (var crane in cranes)
                vm.Cranes.Add(crane);
        }
    }
}
