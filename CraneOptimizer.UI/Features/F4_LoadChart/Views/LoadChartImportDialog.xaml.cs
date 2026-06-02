using CraneOptimizer.Domain.Units;
using CraneOptimizer.Infrastructure.Contexts;
using CraneOptimizer.UI.Features.F4_LoadChart.DTOs;
using CraneOptimizer.UI.Features.F4_LoadChart.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Win32;
using System.Data;
using System.Globalization;
using System.IO;
using System.Text.Json;
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
        private readonly IUnitSettingsService _unitSettingsService;
        private UnitSettings _unitSettings;
        private ParsedLoadChart? _nominalChart;
        private ParsedLoadChart? _importableEnvelopeChart;
        private List<string> _gridHeaders = new();
        private DataTable? _exactVisualPreviewTable;
        private List<string> _exactVisualPreviewHeaders = new();
        private bool _hasExactVisualPreview;
        private bool _isShowingExactVisualPreview;
        private double _vehicleLengthMeters;
        private double _vehicleHeightMeters;
        private double _vehicleWidthMeters;
        private double? _outriggerWidthMeters;
        private double? _outriggerLengthMeters;

        public LoadChartImportDialog(LoadChartCraneDto? craneToEdit = null)
        {
            InitializeComponent();
            var factory = App.ServiceProvider.GetRequiredService<IDbContextFactory<CraneDbContext>>();
            _catalogService = new LoadChartCatalogService(factory);
            _unitSettingsService = App.ServiceProvider.GetRequiredService<IUnitSettingsService>();
            _unitSettings = _unitSettingsService.Current;
            _existingCrane = craneToEdit;
            ApplyUnitLabels();
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
            TxtCounterweight.Text = FormatMassInput(crane.Counterweight);
            TxtMaxRadius.Text = FormatLengthInput(crane.MaxRadius);
            _vehicleLengthMeters = crane.VehicleLengthMeters;
            _vehicleHeightMeters = crane.VehicleHeightMeters;
            _vehicleWidthMeters = crane.VehicleWidthMeters;
            _outriggerWidthMeters = crane.OutriggerWidthMeters;
            _outriggerLengthMeters = crane.OutriggerLengthMeters;
            RefreshDimensionsSummary();

            var rawGrid = BuildRawGridFromDto(crane);
            var parsedChart = crane.HasExactVisualLoadChart
                ? LoadChartCsvService.ParseExactVisualRawGrid(rawGrid, crane.LoadChartSourceDescription)
                : LoadChartCsvService.ParseRawGrid(rawGrid);

            SetNominalChart(parsedChart);

            if (crane.HasExactVisualLoadChart)
            {
                _hasExactVisualPreview = true;
                _exactVisualPreviewTable = parsedChart.PreviewTable;
                _exactVisualPreviewHeaders = parsedChart.PreviewHeaders.ToList();
                TxtEnvelopePreviewTitle.Text = "Exact PDF Visual Load Chart";
                TxtEnvelopePreviewDescription.Text = "This saved crane preserves repeated manufacturer PDF visual columns. Save Crane will keep the exact visual chart; optimization uses the conservative minimum capacity across duplicate numeric boom columns.";
                EnvelopePreviewPanel.Visibility = Visibility.Visible;
                BtnShowImportableEnvelope.IsEnabled = false;
                ShowExactVisualPreview();
            }

            TxtStatus.Text = crane.HasExactVisualLoadChart
                ? $"Editing saved exact PDF visual crane '{crane.Name}'."
                : $"Editing saved crane '{crane.Name}'.";
        }

        private static List<List<string>> BuildRawGridFromDto(LoadChartCraneDto crane)
        {
            if (crane.HasExactVisualLoadChart)
            {
                var exactRawGrid = TryReadExactRawGrid(crane.ExactLoadChartRawGridJson);
                if (exactRawGrid.Count > 1)
                    return exactRawGrid;

                return BuildExactRawGridFromDtoPoints(crane);
            }

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

        private static List<List<string>> BuildExactRawGridFromDtoPoints(LoadChartCraneDto crane)
        {
            var orderedColumns = crane.Points
                .Where(point => point.VisualColumnIndex.HasValue)
                .GroupBy(point => point.VisualColumnIndex.Value)
                .OrderBy(group => group.Key)
                .Select(group => new
                {
                    Index = group.Key,
                    Header = group.Select(point => point.VisualColumnHeader)
                        .FirstOrDefault(header => !string.IsNullOrWhiteSpace(header))
                        ?? group.First().BoomLength.ToString("G", CultureInfo.InvariantCulture),
                    BoomLength = group.First().BoomLength
                })
                .ToList();

            if (orderedColumns.Count == 0)
                return BuildRawGridFromDto(crane with { LoadChartStorageMode = "Standard2D" });

            var rawGrid = new List<List<string>>
            {
                new List<string> { "Radius (m)" }
            };
            rawGrid[0].AddRange(orderedColumns.Select(column => column.Header));

            var radii = crane.Points
                .Select(point => point.Radius)
                .Distinct()
                .OrderBy(value => value)
                .ToList();

            foreach (double radius in radii)
            {
                var row = new List<string> { radius.ToString("G", CultureInfo.InvariantCulture) };

                foreach (var column in orderedColumns)
                {
                    var point = crane.Points.FirstOrDefault(item =>
                        item.VisualColumnIndex == column.Index && Math.Abs(item.Radius - radius) < 0.0001);

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
                Filter = "Load chart files (*.csv;*.pdf)|*.csv;*.pdf|CSV files (*.csv)|*.csv|PDF files (*.pdf)|*.pdf"
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
                var importResult = LoadChartImportService.Import(path, this);

                ClearExactVisualPreview();
                _importableEnvelopeChart = importResult.ImportableEnvelopeChart;
                SetNominalChart(importResult.Chart);
                ApplyExactVisualPreview(importResult);

                TxtFileName.Text = Path.GetFileName(path);
                var info = new FileInfo(path);
                TxtFileSize.Text = $"{info.Length / 1024.0:F1} KB";
                FileInfoPanel.Visibility = Visibility.Visible;

                if (string.IsNullOrWhiteSpace(TxtModel.Text))
                    TxtModel.Text = Path.GetFileNameWithoutExtension(path);

                TxtMaxRadius.Text = _nominalChart == null ? string.Empty : FormatLengthInput(_nominalChart.MaxRadius);

                TxtStatus.Text = importResult.BuildStatusMessage(Path.GetFileName(path));

                if (importResult.HasActionableWarnings())
                {
                    MessageBox.Show(this, importResult.BuildWarningsMessage(), "PDF Import Notes", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                TxtStatus.Text = ex.Message;
                MessageBox.Show(this, ex.Message, "Load Chart Import Error", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void BtnTranspose_Click(object sender, RoutedEventArgs e)
        {
            if (_hasExactVisualPreview)
            {
                TxtStatus.Text = "Swap Radius / Boom is disabled for exact PDF visual charts so repeated manufacturer columns stay aligned.";
                return;
            }

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
                _vehicleLengthMeters,
                _vehicleHeightMeters,
                _vehicleWidthMeters,
                _outriggerWidthMeters,
                _outriggerLengthMeters)
            {
                Owner = this
            };

            if (dialog.ShowDialog() != true)
                return;

            _vehicleLengthMeters = dialog.VehicleLengthMeters;
            _vehicleHeightMeters = dialog.VehicleHeightMeters;
            _vehicleWidthMeters = dialog.VehicleWidthMeters;
            _outriggerWidthMeters = dialog.OutriggerWidthMeters;
            _outriggerLengthMeters = dialog.OutriggerLengthMeters;
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

                ParsedLoadChart chartForSave;

                if (_nominalChart?.IsExactVisualChart == true)
                {
                    if (_isShowingExactVisualPreview)
                    {
                        if (!CaptureExactVisualEdits())
                            return;
                    }

                    if (!ConfirmExactVisualSaveForCurrentChart())
                        return;

                    chartForSave = CopyChartWith(_nominalChart);
                }
                else
                {
                    CommitGridEdits();
                    chartForSave = LoadChartCsvService.ParseTable(dataView.Table, _gridHeaders);
                    _nominalChart = chartForSave;
                }

                if (TryParseOptionalNumber(TxtMaxCap.Text, "Max Capacity", out double manualMaxCapacity))
                    chartForSave = CopyChartWith(chartForSave, maxCapacity: UnitSystem.ToTons(manualMaxCapacity, _unitSettings.DisplayMassUnit));

                if (TryParseOptionalNumber(TxtMaxRadius.Text, "Max Radius", out double manualMaxRadius))
                    chartForSave = CopyChartWith(chartForSave, maxRadius: UnitSystem.ToMeters(manualMaxRadius, _unitSettings.DisplayLengthUnit));

                var crane = LoadChartCatalogService.BuildCraneDto(
                    TxtModel.Text.Trim(),
                    TxtMfg.Text.Trim(),
                    GetSelectedOutriggerConfig(),
                    UnitSystem.ToTons(ParseNumberOrZero(TxtCounterweight.Text, "Counterweight"), _unitSettings.DisplayMassUnit),
                    _vehicleLengthMeters,
                    _vehicleHeightMeters,
                    _vehicleWidthMeters,
                    _outriggerWidthMeters,
                    _outriggerLengthMeters,
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
                $"{parsedChart.PreviewTable.Rows.Count} radii | {parsedChart.PreviewTable.Columns.Count - 1} booms | max {UnitSystem.FormatMass(parsedChart.MaxCapacity, _unitSettings)}";
        }

        private void RefreshDimensionsSummary()
        {
            var parts = new List<string>();

            if (_vehicleLengthMeters > 0 || _vehicleWidthMeters > 0 || _vehicleHeightMeters > 0)
            {
                parts.Add(
                    $"Transport L {FormatLength(_vehicleLengthMeters)}  W {FormatLength(_vehicleWidthMeters)}  H {FormatLength(_vehicleHeightMeters)}");
            }

            if (_outriggerWidthMeters.HasValue || _outriggerLengthMeters.HasValue)
            {
                parts.Add(
                    $"Outriggers W {FormatLength(_outriggerWidthMeters)}  B {FormatLength(_outriggerLengthMeters)}");
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

        private void ClearExactVisualPreview()
        {
            _importableEnvelopeChart = null;
            _exactVisualPreviewTable = null;
            _exactVisualPreviewHeaders = new List<string>();
            _hasExactVisualPreview = false;
            _isShowingExactVisualPreview = false;
            EnvelopePreviewPanel.Visibility = Visibility.Collapsed;
            BtnTranspose.IsEnabled = true;
        }

        private void ApplyExactVisualPreview(LoadChartImportResult importResult)
        {
            if (!importResult.HasExactVisualPreview && !importResult.Chart.IsExactVisualChart)
                return;

            if (_nominalChart == null)
                return;

            _exactVisualPreviewTable = _nominalChart.PreviewTable;
            _exactVisualPreviewHeaders = _nominalChart.PreviewHeaders.ToList();
            _hasExactVisualPreview = true;

            TxtEnvelopePreviewTitle.Text = string.IsNullOrWhiteSpace(importResult.ExactVisualPreviewTitle)
                ? "Exact PDF Visual Load Chart"
                : importResult.ExactVisualPreviewTitle;

            TxtEnvelopePreviewDescription.Text = string.IsNullOrWhiteSpace(importResult.ExactVisualPreviewDescription)
                ? "Save Crane will save the exact PDF visual chart. Duplicate numeric boom columns are preserved with visual metadata; optimization uses the conservative minimum capacity across duplicates."
                : importResult.ExactVisualPreviewDescription;

            EnvelopePreviewPanel.Visibility = Visibility.Visible;
            BtnShowImportableEnvelope.IsEnabled = _importableEnvelopeChart != null;
            ShowExactVisualPreview();
        }

        private void BtnShowExactPdfPreview_Click(object sender, RoutedEventArgs e)
        {
            ShowExactVisualPreview();
        }

        private void BtnShowImportableEnvelope_Click(object sender, RoutedEventArgs e)
        {
            ShowImportableEnvelopePreview();
        }

        private void ShowExactVisualPreview()
        {
            if (_nominalChart == null)
                return;

            _gridHeaders = _nominalChart.PreviewHeaders.ToList();
            ConfigureChartGrid(_nominalChart.PreviewTable, _gridHeaders, previewOnly: false);
            ChartGrid.ItemsSource = _nominalChart.PreviewTable.DefaultView;
            UpdateGridStats(_nominalChart);

            _isShowingExactVisualPreview = _nominalChart.IsExactVisualChart;
            BtnTranspose.IsEnabled = !_nominalChart.IsExactVisualChart;
            BtnShowExactPdfPreview.IsEnabled = false;
            BtnShowImportableEnvelope.IsEnabled = _importableEnvelopeChart != null;

            if (_nominalChart.IsExactVisualChart)
            {
                int visualColumns = Math.Max(0, _nominalChart.PreviewTable.Columns.Count - 1);
                TxtGridStats.Text = $"{_nominalChart.PreviewTable.Rows.Count} radii | {visualColumns} exact PDF visual columns | saveable";
                TxtPreviewMode.Text = "Viewing exact PDF visual load chart. Save Crane will preserve these repeated manufacturer columns and use conservative duplicate-column capacity lookup.";
                TxtStatus.Text = "Exact PDF visual load chart shown. Save Crane will save this exact visual chart.";
            }
        }

        private void ShowImportableEnvelopePreview()
        {
            if (_importableEnvelopeChart == null)
                return;

            if (_nominalChart?.IsExactVisualChart == true && _isShowingExactVisualPreview && !CaptureExactVisualEdits())
                return;

            ConfigureChartGrid(_importableEnvelopeChart.PreviewTable, _importableEnvelopeChart.PreviewHeaders, previewOnly: true);
            ChartGrid.ItemsSource = _importableEnvelopeChart.PreviewTable.DefaultView;
            UpdateGridStats(_importableEnvelopeChart);

            _isShowingExactVisualPreview = false;
            BtnTranspose.IsEnabled = false;
            BtnShowExactPdfPreview.IsEnabled = true;
            BtnShowImportableEnvelope.IsEnabled = false;

            TxtPreviewMode.Text = "Viewing reference 2D envelope only. This is not what Save Crane will save for this exact PDF visual import.";
            TxtStatus.Text = "Reference envelope shown. Save Crane will still save the exact PDF visual chart.";
        }

        private bool CaptureExactVisualEdits()
        {
            if (_nominalChart?.IsExactVisualChart != true)
                return true;

            if (ChartGrid.ItemsSource is not DataView dataView || dataView.Table == null)
                return true;

            try
            {
                CommitGridEdits();
                _nominalChart = LoadChartCsvService.ParseExactVisualTable(
                    dataView.Table,
                    _gridHeaders,
                    sourceDescription: _nominalChart.LoadChartSourceDescription);
                _gridHeaders = _nominalChart.PreviewHeaders.ToList();
                return true;
            }
            catch (Exception ex)
            {
                TxtStatus.Text = ex.Message;
                MessageBox.Show(this, ex.Message, "Exact PDF Visual Chart Has Invalid Data", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }
        }

        private bool ConfirmExactVisualSaveForCurrentChart()
        {
            string message =
                "This crane will be saved as an exact PDF visual load chart." +
                Environment.NewLine + Environment.NewLine +
                "Repeated visual boom columns will be preserved as separate saved capacity points with visual column metadata." +
                Environment.NewLine + Environment.NewLine +
                "For optimization, when repeated columns share the same radius and numeric boom length, the app will use the conservative minimum capacity across those duplicate columns." +
                Environment.NewLine + Environment.NewLine +
                "Continue saving this exact visual chart?";

            return MessageBox.Show(
                this,
                message,
                "Confirm Saving Exact PDF Visual Chart",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning,
                MessageBoxResult.Yes) == MessageBoxResult.Yes;
        }

        private static DataTable BuildSafeRawPreviewTable(
            IReadOnlyList<List<string>> rawGrid,
            List<string> previewHeaders)
        {
            var table = new DataTable();

            if (rawGrid.Count == 0)
                return table;

            int columnCount = rawGrid.Max(row => row.Count);
            var usedHeaders = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            for (int columnIndex = 0; columnIndex < columnCount; columnIndex++)
            {
                string rawHeader = columnIndex < rawGrid[0].Count
                    ? rawGrid[0][columnIndex]
                    : string.Empty;

                string header = string.IsNullOrWhiteSpace(rawHeader)
                    ? columnIndex == 0 ? "Radius (m)" : $"Column {columnIndex + 1}"
                    : rawHeader.Trim();

                string uniqueHeader = header;
                int duplicateIndex = 2;

                while (!usedHeaders.Add(uniqueHeader))
                {
                    uniqueHeader = $"{header} ({duplicateIndex})";
                    duplicateIndex++;
                }

                previewHeaders.Add(uniqueHeader);
                table.Columns.Add($"C{columnIndex}", typeof(string));
            }

            foreach (var rawRow in rawGrid.Skip(1))
            {
                var row = table.NewRow();

                for (int columnIndex = 0; columnIndex < columnCount; columnIndex++)
                {
                    row[columnIndex] = columnIndex < rawRow.Count
                        ? rawRow[columnIndex]
                        : string.Empty;
                }

                table.Rows.Add(row);
            }

            return table;
        }

        private void CommitGridEdits()
        {
            ChartGrid.CommitEdit(DataGridEditingUnit.Cell, true);
            ChartGrid.CommitEdit(DataGridEditingUnit.Row, true);
        }

        private void ShowChart(ParsedLoadChart chart)
        {
            _gridHeaders = chart.PreviewHeaders.ToList();
            _isShowingExactVisualPreview = false;
            BtnTranspose.IsEnabled = !_hasExactVisualPreview;
            TxtMaxCap.Text = FormatMassInput(chart.MaxCapacity);

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
                        Capacity = point.Capacity,
                        VisualColumnIndex = point.VisualColumnIndex,
                        VisualColumnHeader = point.VisualColumnHeader
                    })
                    .ToList(),
                MinRadius = source.MinRadius,
                MaxRadius = maxRadius ?? source.MaxRadius,
                MinBoomLength = source.MinBoomLength,
                MaxBoomLength = source.MaxBoomLength,
                MaxCapacity = maxCapacity ?? source.MaxCapacity,
                LoadChartStorageMode = source.LoadChartStorageMode,
                LoadChartCapacityPolicy = source.LoadChartCapacityPolicy,
                ExactLoadChartRawGridJson = source.ExactLoadChartRawGridJson,
                LoadChartSourceDescription = source.LoadChartSourceDescription
            };
        }

        private void ApplyUnitLabels()
        {
            LblCounterweight.Text = $"Counterweight ({UnitSystem.MassSymbol(_unitSettings.DisplayMassUnit)})";
            LblMaxCapacity.Text = $"Max Capacity ({UnitSystem.MassSymbol(_unitSettings.DisplayMassUnit)})";
            LblMaxRadius.Text = $"Max Radius ({UnitSystem.LengthSymbol(_unitSettings.DisplayLengthUnit)})";
        }

        private void ConfigureChartGrid(DataTable table, IReadOnlyList<string> headers, bool previewOnly = false)
        {
            ChartGrid.AutoGenerateColumns = false;
            ChartGrid.Columns.Clear();
            ChartGrid.IsReadOnly = previewOnly;
            ChartGrid.CanUserAddRows = !previewOnly;
            ChartGrid.CanUserDeleteRows = !previewOnly;

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
                        Mode = previewOnly ? BindingMode.OneWay : BindingMode.TwoWay,
                        UpdateSourceTrigger = UpdateSourceTrigger.LostFocus
                    },
                    Width = columnIndex == 0 ? new DataGridLength(88) : new DataGridLength(58),
                    MinWidth = columnIndex == 0 ? 76 : 52
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

        private string FormatLength(double? value)
        {
            return value.HasValue && value.Value > 0
                ? UnitSystem.FormatLength(value.Value, _unitSettings, CultureInfo.InvariantCulture)
                : "-";
        }

        private string FormatLengthInput(double value)
        {
            return UnitSystem.FromMeters(value, _unitSettings.DisplayLengthUnit)
                .ToString("0.###", CultureInfo.InvariantCulture);
        }

        private string FormatMassInput(double value)
        {
            return UnitSystem.FromTons(value, _unitSettings.DisplayMassUnit)
                .ToString("0.###", CultureInfo.InvariantCulture);
        }
    }
}
