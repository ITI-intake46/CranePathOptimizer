using CraneOptimizer.Domain.Entities;
using CraneOptimizer.Infrastructure.Contexts;
using CraneOptimizer.UI.Features.F4_LoadChart.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Data;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;

namespace CraneOptimizer.UI.Features.F4_LoadChart.Views
{
    public partial class CraneCatalogBrowserDialog : Window
    {
        private readonly LoadChartCatalogService _catalogService;
        private List<Crane> _allCranes = new();

        public CraneCatalogBrowserDialog()
        {
            InitializeComponent();
            var factory = App.ServiceProvider.GetRequiredService<IDbContextFactory<CraneDbContext>>();
            _catalogService = new LoadChartCatalogService(factory);
            Loaded += async (_, _) => await LoadCranesAsync();
        }

        private async Task LoadCranesAsync()
        {
            _allCranes = await _catalogService.GetAllAsync();
            ApplyFilter();
            TxtStatus.Text = $"{_allCranes.Count} crane(s) loaded.";
        }

        private void TxtSearch_TextChanged(object sender, TextChangedEventArgs e)
        {
            ApplyFilter();
        }

        private void ApplyFilter()
        {
            string filter = TxtSearch.Text?.Trim() ?? string.Empty;
            var filtered = string.IsNullOrWhiteSpace(filter)
                ? _allCranes
                : _allCranes.Where(crane =>
                        crane.Name.Contains(filter, StringComparison.OrdinalIgnoreCase)
                        || crane.Manufacturer.Contains(filter, StringComparison.OrdinalIgnoreCase))
                    .ToList();

            LstCranes.ItemsSource = filtered;
            if (LstCranes.SelectedItem is not Crane selectedCrane || !filtered.Contains(selectedCrane))
                LstCranes.SelectedItem = filtered.FirstOrDefault();
        }

        private void LstCranes_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (LstCranes.SelectedItem is not Crane crane)
            {
                DetailModel.Text = "Select a crane";
                DetailMfg.Text = string.Empty;
                DetailDimensions.Text = string.Empty;
                DetailOutrigger.Text = string.Empty;
                DetailCounterweight.Text = string.Empty;
                DetailPointCount.Text = string.Empty;
                MiniGrid.ItemsSource = null;
                return;
            }

            var spec = crane.Specifications.FirstOrDefault();
            DetailModel.Text = crane.Name;
            DetailMfg.Text = crane.Manufacturer;
            DetailDimensions.Text = BuildDimensionSummary(crane);
            DetailOutrigger.Text = spec?.OutriggerConfig ?? "-";
            DetailCounterweight.Text = $"{spec?.CounterWeight.ToString("F0", CultureInfo.InvariantCulture) ?? "0"} kg";
            DetailPointCount.Text = spec?.LoadChart.Count.ToString(CultureInfo.InvariantCulture) ?? "0";
            MiniGrid.ItemsSource = spec == null ? null : BuildPreviewTable(spec).DefaultView;
        }

        private static DataTable BuildPreviewTable(CraneSpecification specification)
        {
            var booms = specification.LoadChart
                .Select(point => point.BoomLength)
                .Distinct()
                .OrderBy(value => value)
                .ToList();

            var radii = specification.LoadChart
                .Select(point => point.Radius)
                .Distinct()
                .OrderBy(value => value)
                .ToList();

            var table = new DataTable();
            table.Columns.Add("Radius");
            foreach (double boom in booms)
                table.Columns.Add(boom.ToString(CultureInfo.InvariantCulture));

            foreach (double radius in radii)
            {
                var row = table.NewRow();
                row[0] = radius.ToString(CultureInfo.InvariantCulture);

                for (int i = 0; i < booms.Count; i++)
                {
                    var point = specification.LoadChart
                        .FirstOrDefault(item => Math.Abs(item.Radius - radius) < 0.0001 && Math.Abs(item.BoomLength - booms[i]) < 0.0001);

                    row[i + 1] = point == null || point.Capacity == 0d
                        ? string.Empty
                        : point.Capacity.ToString(CultureInfo.InvariantCulture);
                }

                table.Rows.Add(row);
            }

            return table;
        }

        private static string BuildDimensionSummary(Crane crane)
        {
            var parts = new List<string>();

            if (crane.VehicleLength > 0 || crane.VehicleWidth > 0 || crane.VehicleHeight > 0)
            {
                parts.Add(
                    $"Transport L {FormatMillimetres(crane.VehicleLength)}  W {FormatMillimetres(crane.VehicleWidth)}  H {FormatMillimetres(crane.VehicleHeight)}");
            }

            if (crane.OutriggerWidth.HasValue || crane.OutriggerLength.HasValue)
            {
                parts.Add(
                    $"Outriggers W {FormatMillimetres(crane.OutriggerWidth)}  B {FormatMillimetres(crane.OutriggerLength)}");
            }

            return parts.Count > 0 ? string.Join("   |   ", parts) : "No crane dimensions saved.";
        }

        private static string FormatMillimetres(double? value)
        {
            return value.HasValue && value.Value > 0
                ? $"{value.Value.ToString("F0", CultureInfo.InvariantCulture)} mm"
                : "-";
        }

        private async void BtnEdit_Click(object sender, RoutedEventArgs e)
        {
            if (LstCranes.SelectedItem is not Crane crane)
            {
                TxtStatus.Text = "Select a crane to edit.";
                return;
            }

            var dialog = new LoadChartImportDialog(crane) { Owner = this };
            if (dialog.ShowDialog() == true)
                await LoadCranesAsync();
        }

        private async void BtnDelete_Click(object sender, RoutedEventArgs e)
        {
            if (LstCranes.SelectedItem is not Crane crane)
            {
                TxtStatus.Text = "Select a crane to delete.";
                return;
            }

            var result = MessageBox.Show(
                this,
                $"Delete saved crane '{crane.Name}'?",
                "Delete Crane",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result != MessageBoxResult.Yes)
                return;

            await _catalogService.DeleteAsync(crane.Id);
            await LoadCranesAsync();
            TxtStatus.Text = $"Deleted '{crane.Name}'.";
        }

        private async void BtnImport_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new LoadChartImportDialog { Owner = this };
            if (dialog.ShowDialog() == true)
                await LoadCranesAsync();
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
