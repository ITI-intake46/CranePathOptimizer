using CraneOptimizer.Infrastructure.Contexts;
using CraneOptimizer.UI.Features.F4_LoadChart.DTOs;
using CraneOptimizer.UI.Features.F4_LoadChart.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Win32;
using System.Data;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;

namespace CraneOptimizer.UI.Features.F4_LoadChart.Views
{
    public partial class LoadChartImportDialog : Window
    {
        private readonly LoadChartCatalogService _catalogService;
        private readonly LoadChartCraneDto? _existingCrane;
        private ParsedLoadChart? _nominalChart;
        private List<string> _gridHeaders = new();
        private double _vehicleLengthMm;
        private double _vehicleHeightMm;
        private double _vehicleWidthMm;
        private double? _outriggerWidthMm;
        private double? _outriggerLengthMm;

        public LoadChartImportDialog(LoadChartCraneDto? craneToEdit = null)
        {
            InitializeComponent();

            var factory = App.ServiceProvider.GetRequiredService<IDbContextFactory<CraneDbContext>>();
            _catalogService = new LoadChartCatalogService(factory);
            _existingCrane = craneToEdit;
            RefreshDimensionsSummary();

            if (_existingCrane != null)
                LoadExistingCrane(_existingCrane);
        }

        private void LoadExistingCrane(LoadChartCraneDto crane)
        {
            TxtHeader.Text = "Edit Crane Load Chart";
            TxtModel.Text = crane.Name;
            TxtMfg.Text = crane.Manufacturer;
            SelectOutriggerConfig(crane.OutriggerConfig);
            TxtCounterweight.Text = crane.Counterweight.ToString(CultureInfo.InvariantCulture);
            TxtMaxRadius.Text = crane.MaxRadius.ToString(CultureInfo.InvariantCulture);
            _vehicleLengthMm = crane.VehicleLengthMm;
            _vehicleHeightMm = crane.VehicleHeightMm;
            _vehicleWidthMm = crane.VehicleWidthMm;
            _outriggerWidthMm = crane.OutriggerWidthMm;
            _outriggerLengthMm = crane.OutriggerLengthMm;
            RefreshDimensionsSummary();

            SetNominalChart(LoadChartCsvService.ParseRawGrid(BuildRawGridFromDto(crane)));
            TxtStatus.Text = $"Editing saved crane '{crane.Name}'.";
        }

        private static List<List<string>> BuildRawGridFromDto(LoadChartCraneDto crane)
        {
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

            var rawGrid = new List<List<string>>
            {
                new() { "Radius (m)" }
            };
            rawGrid[0].AddRange(booms.Select(boom => boom.ToString("G", CultureInfo.InvariantCulture)));

            foreach (double radius in radii)
            {
                var row = new List<string> { radius.ToString("G", CultureInfo.InvariantCulture) };

                for (int i = 0; i < booms.Count; i++)
                {
                    var point = crane.Points
                        .FirstOrDefault(item => Math.Abs(item.Radius - radius) < 0.0001 && Math.Abs(item.BoomLength - booms[i]) < 0.0001);

                    row.Add(point == null || point.Capacity == 0d
                        ? string.Empty
                        : point.Capacity.ToString("G", CultureInfo.InvariantCulture));
                }

                rawGrid.Add(row);
            }

            return rawGrid;
        }

        private void DropZone_Click(object sender, MouseButtonEventArgs e)
        {
            var dialog = new OpenFileDialog
            {
                Filter = "CSV files (*.csv)|*.csv"
            };

            if (dialog.ShowDialog() == true)
                LoadFile(dialog.FileName);
        }

        private void DropZone_DragOver(object sender, DragEventArgs e)
            => e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop)
                ? DragDropEffects.Copy
                : DragDropEffects.None;

        private void DropZone_DragEnter(object sender, DragEventArgs e)
            => DropZone.Opacity = 0.7;

        private void DropZone_DragLeave(object sender, DragEventArgs e)
            => DropZone.Opacity = 1.0;

        private void DropZone_Drop(object sender, DragEventArgs e)
        {
            DropZone.Opacity = 1.0;
            if (e.Data.GetData(DataFormats.FileDrop) is string[] files && files.Length > 0)
                LoadFile(files[0]);
        }

        private void LoadFile(string path)
        {
            try
            {
                SetNominalChart(LoadChartCsvService.ParseCsv(path));

                TxtFileName.Text = Path.GetFileName(path);
                var info = new FileInfo(path);
                TxtFileSize.Text = $"{info.Length / 1024.0:F1} KB";
                FileInfoPanel.Visibility = Visibility.Visible;

                if (string.IsNullOrWhiteSpace(TxtModel.Text))
                    TxtModel.Text = Path.GetFileNameWithoutExtension(path);

                TxtMaxRadius.Text = _nominalChart?.MaxRadius.ToString(CultureInfo.InvariantCulture) ?? string.Empty;

                TxtStatus.Text = $"Loaded {Path.GetFileName(path)}.";
            }
            catch (Exception ex)
            {
                TxtStatus.Text = ex.Message;
                MessageBox.Show(this, ex.Message, "CSV Import Error", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void BtnTranspose_Click(object sender, RoutedEventArgs e)
        {
            if (_nominalChart == null)
            {
                TxtStatus.Text = "Load chart data first.";
                return;
            }

            CommitGridEdits();
            if (ChartGrid.ItemsSource is DataView dataView && dataView.Table != null)
            {
                try
                {
                    _nominalChart = LoadChartCsvService.ParseTable(dataView.Table, _gridHeaders);
                }
                catch (Exception ex)
                {
                    TxtStatus.Text = ex.Message;
                    return;
                }
            }

            if (_nominalChart == null)
                return;

            SetNominalChart(LoadChartCsvService.TransposePreview(_nominalChart.PreviewTable, _gridHeaders));
            TxtStatus.Text = "Grid orientation swapped.";
        }

        private void BtnToggleDimensions_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new CraneDimensionsDialog(
                _vehicleLengthMm,
                _vehicleHeightMm,
                _vehicleWidthMm,
                _outriggerWidthMm,
                _outriggerLengthMm)
            {
                Owner = this
            };

            if (dialog.ShowDialog() != true)
                return;

            _vehicleLengthMm = dialog.VehicleLengthMm;
            _vehicleHeightMm = dialog.VehicleHeightMm;
            _vehicleWidthMm = dialog.VehicleWidthMm;
            _outriggerWidthMm = dialog.OutriggerWidthMm;
            _outriggerLengthMm = dialog.OutriggerLengthMm;
            RefreshDimensionsSummary();
            TxtStatus.Text = "Crane dimensions updated.";
        }

        private async void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(TxtModel.Text))
                {
                    TxtStatus.Text = "Model name is required.";
                    return;
                }

                if (ChartGrid.ItemsSource is not DataView dataView || dataView.Table == null)
                {
                    TxtStatus.Text = "Load chart data is required.";
                    return;
                }

                CommitGridEdits();
                var chartForSave = LoadChartCsvService.ParseTable(dataView.Table, _gridHeaders);

                if (TryParseOptionalNumber(TxtMaxCap.Text, "Max Capacity", out double manualMaxCapacity))
                    chartForSave = CopyChartWith(chartForSave, maxCapacity: manualMaxCapacity);

                if (TryParseOptionalNumber(TxtMaxRadius.Text, "Max Radius", out double manualMaxRadius))
                    chartForSave = CopyChartWith(chartForSave, maxRadius: manualMaxRadius);

                var crane = LoadChartCatalogService.BuildCraneDto(
                    TxtModel.Text.Trim(),
                    TxtMfg.Text.Trim(),
                    GetSelectedOutriggerConfig(),
                    ParseNumberOrZero(TxtCounterweight.Text, "Counterweight"),
                    _vehicleLengthMm,
                    _vehicleHeightMm,
                    _vehicleWidthMm,
                    _outriggerWidthMm,
                    _outriggerLengthMm,
                    chartForSave,
                    _existingCrane?.Id ?? 0);

                if (_existingCrane == null)
                    await _catalogService.CreateAsync(crane);
                else
                    await _catalogService.UpdateAsync(crane);

                DialogResult = true;
                Close();
            }
            catch (Exception ex)
            {
                TxtStatus.Text = ex.Message;
                MessageBox.Show(this, ex.Message, "Save Error", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private string GetSelectedOutriggerConfig()
        {
            return CboOutriggerConfig.SelectedItem is ComboBoxItem item
                ? item.Content?.ToString() ?? "100%"
                : "100%";
        }

        private void SelectOutriggerConfig(string? value)
        {
            string normalized = string.IsNullOrWhiteSpace(value)
                ? "100%"
                : value.Trim();

            foreach (ComboBoxItem item in CboOutriggerConfig.Items)
            {
                if (string.Equals(item.Content?.ToString(), normalized, StringComparison.OrdinalIgnoreCase))
                {
                    CboOutriggerConfig.SelectedItem = item;
                    return;
                }
            }

            CboOutriggerConfig.SelectedIndex = 0;
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void UpdateGridStats(ParsedLoadChart parsedChart)
        {
            TxtGridStats.Text =
                $"{parsedChart.PreviewTable.Rows.Count} radii | {parsedChart.PreviewTable.Columns.Count - 1} booms | max {FormatCapacity(parsedChart.MaxCapacity)} t";
        }

        private void RefreshDimensionsSummary()
        {
            var parts = new List<string>();

            if (_vehicleLengthMm > 0 || _vehicleWidthMm > 0 || _vehicleHeightMm > 0)
            {
                parts.Add(
                    $"Transport L {FormatMillimetres(_vehicleLengthMm)}  W {FormatMillimetres(_vehicleWidthMm)}  H {FormatMillimetres(_vehicleHeightMm)}");
            }

            if (_outriggerWidthMm.HasValue || _outriggerLengthMm.HasValue)
            {
                parts.Add(
                    $"Outriggers W {FormatMillimetres(_outriggerWidthMm)}  B {FormatMillimetres(_outriggerLengthMm)}");
            }

            TxtDimensionsSummary.Text = parts.Count > 0
                ? string.Join(Environment.NewLine, parts)
                : "No crane dimensions entered yet.";

            BtnToggleDimensions.Content = parts.Count > 0
                ? "Edit Crane Dimensions"
                : "Enter Crane Dimensions";
        }

        private void SetNominalChart(ParsedLoadChart nominalChart)
        {
            _nominalChart = nominalChart;
            ShowChart(nominalChart);
        }

        private void CommitGridEdits()
        {
            ChartGrid.CommitEdit(DataGridEditingUnit.Cell, true);
            ChartGrid.CommitEdit(DataGridEditingUnit.Row, true);
        }

        private void ShowChart(ParsedLoadChart chart)
        {
            _gridHeaders = chart.PreviewHeaders.ToList();
            TxtMaxCap.Text = FormatCapacity(chart.MaxCapacity);

            ConfigureChartGrid(chart.PreviewTable, _gridHeaders);
            ChartGrid.ItemsSource = chart.PreviewTable.DefaultView;
            UpdateGridStats(chart);
        }

        private static ParsedLoadChart CopyChartWith(
            ParsedLoadChart source,
            DataTable? previewTable = null,
            List<CraneOptimizer.Domain.Entities.TargetPoint>? points = null,
            double? maxCapacity = null,
            double? maxRadius = null)
        {
            return new ParsedLoadChart
            {
                PreviewTable = previewTable ?? source.PreviewTable.Copy(),
                PreviewHeaders = source.PreviewHeaders.ToList(),
                Points = points ?? source.Points
                    .Select(point => new CraneOptimizer.Domain.Entities.TargetPoint
                    {
                        Radius = point.Radius,
                        BoomLength = point.BoomLength,
                        Capacity = point.Capacity
                    })
                    .ToList(),
                MinRadius = source.MinRadius,
                MaxRadius = maxRadius ?? source.MaxRadius,
                MinBoomLength = source.MinBoomLength,
                MaxBoomLength = source.MaxBoomLength,
                MaxCapacity = maxCapacity ?? source.MaxCapacity
            };
        }

        private static string FormatCapacity(double value)
        {
            return value.ToString("0.##", CultureInfo.InvariantCulture);
        }

        private void ConfigureChartGrid(DataTable table, IReadOnlyList<string> headers)
        {
            ChartGrid.AutoGenerateColumns = false;
            ChartGrid.Columns.Clear();

            for (int columnIndex = 0; columnIndex < table.Columns.Count; columnIndex++)
            {
                string header = columnIndex < headers.Count
                    ? headers[columnIndex]
                    : table.Columns[columnIndex].ColumnName;

                ChartGrid.Columns.Add(new DataGridTextColumn
                {
                    Header = header,
                    Binding = new Binding($"[{table.Columns[columnIndex].ColumnName}]")
                    {
                        Mode = BindingMode.TwoWay,
                        UpdateSourceTrigger = UpdateSourceTrigger.LostFocus
                    },
                    Width = columnIndex == 0 ? new DataGridLength(96) : new DataGridLength(60)
                });
            }
        }

        private static double ParseNumberOrZero(string text, string fieldName)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return 0d;
            }

            if (LoadChartCsvService.TryParseFlexibleNumber(text, out double value))
                return value;

            throw new FormatException($"{fieldName} value '{text}' is invalid.");
        }

        private static bool TryParseOptionalNumber(string text, string fieldName, out double value)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                value = 0d;
                return false;
            }

            if (LoadChartCsvService.TryParseFlexibleNumber(text, out value))
                return true;

            throw new FormatException($"{fieldName} value '{text}' is invalid.");
        }

        private static string FormatMillimetres(double? value)
        {
            return value.HasValue && value.Value > 0
                ? $"{value.Value.ToString("F0", CultureInfo.InvariantCulture)} mm"
                : "-";
        }
    }
}
