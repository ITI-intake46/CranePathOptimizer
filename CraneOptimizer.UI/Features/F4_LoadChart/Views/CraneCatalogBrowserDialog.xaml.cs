using CraneOptimizer.Domain.Units;
using CraneOptimizer.Infrastructure.Contexts;
using CraneOptimizer.UI.Features.F4_LoadChart.DTOs;
using CraneOptimizer.UI.Features.F4_LoadChart.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Data;
using System.Globalization;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;

namespace CraneOptimizer.UI.Features.F4_LoadChart.Views
{
    public partial class CraneCatalogBrowserDialog : Window
    {
        private readonly LoadChartCatalogService _catalogService;
        private readonly IUnitSettingsService _unitSettingsService;
        private List<LoadChartCraneDto> _allCranes = new();

        public CraneCatalogBrowserDialog()
        {
            InitializeComponent();
            var factory = App.ServiceProvider.GetRequiredService<IDbContextFactory<CraneDbContext>>();
            _catalogService = new LoadChartCatalogService(factory);
            _unitSettingsService = App.ServiceProvider.GetRequiredService<IUnitSettingsService>();
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
            if (LstCranes.SelectedItem is not LoadChartCraneDto selectedCrane || !filtered.Contains(selectedCrane))
                LstCranes.SelectedItem = filtered.FirstOrDefault();
        }

        private void LstCranes_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (LstCranes.SelectedItem is not LoadChartCraneDto crane)
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

            DetailModel.Text = crane.Name;
            DetailMfg.Text = crane.Manufacturer;
            DetailDimensions.Text = BuildDimensionSummary(crane, _unitSettingsService.Current);
            DetailOutrigger.Text = crane.HasExactVisualLoadChart
                ? $"{crane.OutriggerConfig} | exact PDF visual chart"
                : crane.OutriggerConfig;
            DetailCounterweight.Text = UnitSystem.FormatMass(crane.Counterweight, _unitSettingsService.Current, CultureInfo.InvariantCulture);
            DetailPointCount.Text = crane.HasExactVisualLoadChart
                ? $"{crane.Points.Count} exact visual cell(s)"
                : crane.Points.Count.ToString(CultureInfo.InvariantCulture);
            MiniGrid.ItemsSource = BuildPreviewTable(crane).DefaultView;
        }

        private static DataTable BuildPreviewTable(LoadChartCraneDto crane)
        {
            if (crane.HasExactVisualLoadChart)
                return BuildExactPreviewTable(crane);

            var booms = crane.Points
                .Select(point => point.BoomLength)
                .Distinct()
                .OrderBy(value => value)
                .ToList();

            var radii = crane.Points
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
                    var point = crane.Points
                        .FirstOrDefault(item => Math.Abs(item.Radius - radius) < 0.0001 && Math.Abs(item.BoomLength - booms[i]) < 0.0001);

                    row[i + 1] = point == null || point.Capacity == 0d
                        ? string.Empty
                        : point.Capacity.ToString(CultureInfo.InvariantCulture);
                }

                table.Rows.Add(row);
            }

            return table;
        }

        private static DataTable BuildExactPreviewTable(LoadChartCraneDto crane)
        {
            var rawGrid = TryReadExactRawGrid(crane.ExactLoadChartRawGridJson);
            if (rawGrid.Count > 1)
                return BuildTableFromRawGrid(rawGrid);

            var orderedColumns = crane.Points
                .Where(point => point.VisualColumnIndex.HasValue)
                .GroupBy(point => point.VisualColumnIndex.Value)
                .OrderBy(group => group.Key)
                .Select(group => new
                {
                    Index = group.Key,
                    Header = group.Select(point => point.VisualColumnHeader)
                        .FirstOrDefault(header => !string.IsNullOrWhiteSpace(header))
                        ?? group.First().BoomLength.ToString("G", CultureInfo.InvariantCulture)
                })
                .ToList();

            var table = new DataTable();
            table.Columns.Add("Radius");
            foreach (var column in orderedColumns)
                table.Columns.Add(column.Header);

            foreach (double radius in crane.Points.Select(point => point.Radius).Distinct().OrderBy(value => value))
            {
                var row = table.NewRow();
                row[0] = radius.ToString("G", CultureInfo.InvariantCulture);

                for (int i = 0; i < orderedColumns.Count; i++)
                {
                    var column = orderedColumns[i];
                    var point = crane.Points.FirstOrDefault(item =>
                        item.VisualColumnIndex == column.Index && Math.Abs(item.Radius - radius) < 0.0001);

                    row[i + 1] = point == null || point.Capacity == 0d
                        ? string.Empty
                        : point.Capacity.ToString("G", CultureInfo.InvariantCulture);
                }

                table.Rows.Add(row);
            }

            return table;
        }

        private static List<List<string>> TryReadExactRawGrid(string? json)
        {
            if (string.IsNullOrWhiteSpace(json))
                return new List<List<string>>();

            try
            {
                return JsonSerializer.Deserialize<List<List<string>>>(json) ?? new List<List<string>>();
            }
            catch
            {
                return new List<List<string>>();
            }
        }

        private static DataTable BuildTableFromRawGrid(IReadOnlyList<List<string>> rawGrid)
        {
            var table = new DataTable();
            int columnCount = rawGrid.Max(row => row.Count);

            for (int columnIndex = 0; columnIndex < columnCount; columnIndex++)
            {
                string header = rawGrid[0].Count > columnIndex && !string.IsNullOrWhiteSpace(rawGrid[0][columnIndex])
                    ? rawGrid[0][columnIndex]
                    : columnIndex == 0 ? "Radius" : $"Column {columnIndex + 1}";

                string uniqueHeader = header;
                int suffix = 2;
                while (table.Columns.Contains(uniqueHeader))
                {
                    uniqueHeader = $"{header} ({suffix})";
                    suffix++;
                }

                table.Columns.Add(uniqueHeader);
            }

            foreach (var sourceRow in rawGrid.Skip(1))
            {
                var row = table.NewRow();
                for (int columnIndex = 0; columnIndex < columnCount; columnIndex++)
                    row[columnIndex] = columnIndex < sourceRow.Count ? sourceRow[columnIndex] : string.Empty;

                table.Rows.Add(row);
            }

            return table;
        }

        private static string BuildDimensionSummary(LoadChartCraneDto crane, UnitSettings settings)
        {
            var parts = new List<string>();

            if (crane.VehicleLengthMeters > 0 || crane.VehicleWidthMeters > 0 || crane.VehicleHeightMeters > 0)
            {
                parts.Add(
                    $"Transport L {FormatLength(crane.VehicleLengthMeters, settings)}  W {FormatLength(crane.VehicleWidthMeters, settings)}  H {FormatLength(crane.VehicleHeightMeters, settings)}");
            }

            if (crane.OutriggerWidthMeters.HasValue || crane.OutriggerLengthMeters.HasValue)
            {
                parts.Add(
                    $"Outriggers W {FormatLength(crane.OutriggerWidthMeters, settings)}  B {FormatLength(crane.OutriggerLengthMeters, settings)}");
            }

            return parts.Count > 0 ? string.Join("   |   ", parts) : "No crane dimensions saved.";
        }

        private static string FormatLength(double? value, UnitSettings settings)
        {
            return value.HasValue && value.Value > 0
                ? UnitSystem.FormatLength(value.Value, settings, CultureInfo.InvariantCulture)
                : "-";
        }

        private async void BtnEdit_Click(object sender, RoutedEventArgs e)
        {
            if (LstCranes.SelectedItem is not LoadChartCraneDto crane)
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
            if (LstCranes.SelectedItem is not LoadChartCraneDto crane)
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
