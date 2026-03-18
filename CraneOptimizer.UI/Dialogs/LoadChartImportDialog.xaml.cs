using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.EntityFrameworkCore;
using WPF_IFC_test1.Database;
using WPF_IFC_test1.Database.Entities;
using WPF_IFC_test1.Database.Services;

namespace WPF_IFC_test1.Dialogs
{
    public partial class LoadChartImportDialog : Window
    {
        // ── Public results ────────────────────────────────────────────────────
        public CraneLoadChartEntity? ImportedChart { get; private set; }
        public List<CraneLoadChartPointEntity>? ImportedPoints { get; private set; }

        // ── Internal state ────────────────────────────────────────────────────
        private string? _selectedFilePath;
        private bool _isPdf => RboPdf.IsChecked == true;
        private bool _hasData = false;
        private List<List<string>>? _rawGrid; // stored for axis-flip re-render

        // The DataTable is the backing store for the DataGrid.
        // Columns: "Radius (m)" then one column per boom length.
        private DataTable? _table;

        // ── Physical dimensions entered by user (stored in mm) ────────────────
        private double _dimWidthMm = 0;          // W: outrigger span (pad to pad)
        private double _dimTotalLengthMm = 0;    // L: total transport length
        private double _dimHeightMm = 0;         // H: transport height
        private double _dimTransportWidthMm = 0; // T: travel width including tires
        private double _dimOutriggerBaseMm = 0;  // B: front-to-rear span to span

        // ── Bounding radius ───────────────────────────────────────────────────
        private double _calcBaseRadiusM = 0;   // auto-calculated from footprint (meters)
        private double _radiusExtraM = 0;      // user adjustment (+0 to +10 m)

        // ── Edit mode ─────────────────────────────────────────────────────────
        private int _editingChartId = -1; // -1 = new chart, >0 = editing existing
        private bool _heatmapEnabled = false;
        private double _heatmapMin = 0, _heatmapMax = 1;

        // Unit conversion fields
        private double _capacityConversionFactor = 1.0;  // multiply stored tonnes by this to get display
        private double _distanceConversionFactor = 1.0;  // multiply stored metres by this to get display

        public LoadChartImportDialog()
        {
            InitializeComponent();
        }

        /// <summary>
        /// Opens the dialog in Edit mode — pre-fills all fields from an existing chart.
        /// </summary>
        public LoadChartImportDialog(int existingChartId)
            : this()
        {
            _editingChartId = existingChartId;
            Title = "Edit Load Chart";
            Loaded += async (s, e) => await LoadExistingChartAsync(existingChartId);
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Edit mode: load existing chart
        // ─────────────────────────────────────────────────────────────────────

        private async Task LoadExistingChartAsync(int chartId)
        {
            try
            {
                using var db = DatabaseService.Instance.CreateContext();
                var chart = await db.CraneLoadCharts
                    .Include(c => c.Points)
                    .FirstOrDefaultAsync(c => c.Id == chartId);

                if (chart == null) { SetStatus("Chart not found.", true); return; }

                // Pre-fill metadata fields
                TxtModel.Text        = chart.CraneModel;
                TxtManufacturer.Text = chart.Manufacturer;

                // Set combo selections
                SetComboByContent(CboChartType, chart.ChartType);

                // Outrigger config radio buttons
                if (chart.OutriggerConfig == "75%") RbOutrigger75.IsChecked = true;
                else if (chart.OutriggerConfig == "50%") RbOutrigger50.IsChecked = true;
                else if (chart.OutriggerConfig == "On Rubber") RbOutriggerRubber.IsChecked = true;
                else RbOutrigger100.IsChecked = true;

                // Crane type radio button
                if (chart.CraneCategory == "Tower") RboCraneTypeTower.IsChecked = true;
                else RboCraneTypeMobile.IsChecked = true;

                // Derating slider
                SldDerating.Value = chart.DeratingPct;

                // Counterweight
                if (chart.CounterweightKg > 0)
                    TxtCounterweight.Text = chart.CounterweightKg.ToString("F0");

                // Physical dims
                _dimWidthMm          = chart.OutriggerSpanMm;
                _dimTotalLengthMm    = chart.TotalLengthMm;
                _dimHeightMm         = chart.HeightMm;
                _dimTransportWidthMm = chart.TransportWidthMm;
                _dimOutriggerBaseMm  = chart.SpanToSpanMm;
                _calcBaseRadiusM     = chart.BoundingRadiusM; // restore so summary shows correctly

                // Show dimension badge if dims exist
                if (_dimWidthMm > 0 || _dimTotalLengthMm > 0)
                {
                    var sb = new System.Text.StringBuilder();
                    if (_dimTotalLengthMm > 0)    sb.AppendLine($"Length:           {_dimTotalLengthMm / 1000:F2} m");
                    if (_dimHeightMm > 0)          sb.AppendLine($"Height:           {_dimHeightMm / 1000:F2} m");
                    if (_dimTransportWidthMm > 0)  sb.AppendLine($"Travel width:     {_dimTransportWidthMm / 1000:F2} m");
                    if (_dimWidthMm > 0)           sb.AppendLine($"Outrigger span:   {_dimWidthMm / 1000:F2} m");
                    if (_dimOutriggerBaseMm > 0)   sb.AppendLine($"Span to span:     {_dimOutriggerBaseMm / 1000:F2} m");
                    if (_calcBaseRadiusM > 0)      sb.AppendLine($"Bounding radius:  {_calcBaseRadiusM:F2} m");
                    DimEnteredBadge.Visibility = Visibility.Visible;
                    TxtDimSummary.Text = sb.ToString().TrimEnd();
                }

                // Rebuild grid from saved points
                if (chart.Points.Any())
                    PopulateGridFromPoints(chart.Points.ToList());

                SetStatus($"Editing chart: {chart.CraneModel} — {chart.ChartType}", false);
            }
            catch (Exception ex)
            {
                SetStatus($"Failed to load chart: {ex.Message}", true);
            }
        }

        private void SetComboByContent(ComboBox combo, string value)
        {
            foreach (ComboBoxItem item in combo.Items)
                if (item.Content?.ToString() == value)
                { combo.SelectedItem = item; return; }
        }

        /// <summary>
        /// Reconstructs the DataTable from saved CraneLoadChartPointEntity rows.
        /// </summary>
        private void PopulateGridFromPoints(List<CraneLoadChartPointEntity> points)
        {
            if (points.Count == 0) return;

            var booms = points.Select(p => p.BoomLengthM).Distinct().OrderBy(b => b).ToList();
            var radii = points.Select(p => p.RadiusM).Distinct().OrderBy(r => r).ToList();

            // Build raw grid: first row = headers, subsequent rows = data
            var raw = new List<List<string>>();
            var header = new List<string> { "Radius" };
            header.AddRange(booms.Select(b => b.ToString("G")));
            raw.Add(header);

            // GroupBy to tolerate duplicate (radius, boom) pairs that may have been saved
            // when a CSV had two columns with the same boom value
            var lookup = points
                .Where(p => p.CapacityTonne.HasValue)
                .GroupBy(p => (p.RadiusM, p.BoomLengthM))
                .ToDictionary(g => g.Key, g => g.Last().CapacityTonne!.Value.ToString("G"));

            foreach (double r in radii)
            {
                var row = new List<string> { r.ToString("G") };
                foreach (double b in booms)
                    row.Add(lookup.TryGetValue((r, b), out string? v) ? v : "");
                raw.Add(row);
            }

            _rawGrid = raw;
            PopulateGrid(raw);
            _hasData = true;
            BtnSave.IsEnabled = true;
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Method selector
        // ─────────────────────────────────────────────────────────────────────

        private void Method_Checked(object sender, RoutedEventArgs e)
        {
            if (DropIcon == null) return; // not yet initialized

            if (_isPdf)
            {
                DropIcon.Text = "📑";
                DropTitle.Text = "Drop PDF here";
                DropSub.Text = "or click to browse";
                PdfPagePanel.Visibility = Visibility.Visible;
                PdfWarningPanel.Visibility = Visibility.Visible;
                CsvTemplatePanel.Visibility = Visibility.Collapsed;
                TxtSubtitle.Text = "Extracts the table automatically — ALWAYS verify numbers before saving.";
            }
            else
            {
                DropIcon.Text = "📄";
                DropTitle.Text = "Drop CSV here";
                DropSub.Text = "or click to browse";
                PdfPagePanel.Visibility = Visibility.Collapsed;
                PdfWarningPanel.Visibility = Visibility.Collapsed;
                CsvTemplatePanel.Visibility = Visibility.Visible;
                TxtSubtitle.Text = "First row = boom lengths, first column = radius. Clean, reliable, free.";
            }

            ClearAll();
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Drop zone
        // ─────────────────────────────────────────────────────────────────────

        private void DropZone_DragEnter(object s, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
                DropZone.BorderBrush = new SolidColorBrush(Color.FromRgb(0x27, 0xAE, 0x60));
        }
        private void DropZone_DragLeave(object s, DragEventArgs e) => ResetDropBorder();
        private void DropZone_DragOver(object s, DragEventArgs e)
        {
            e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop)
                ? DragDropEffects.Copy : DragDropEffects.None;
            e.Handled = true;
        }
        private void DropZone_Drop(object s, DragEventArgs e)
        {
            ResetDropBorder();
            if (!e.Data.GetDataPresent(DataFormats.FileDrop)) return;
            var files = (string[])e.Data.GetData(DataFormats.FileDrop);
            if (files?.Length > 0) LoadFile(files[0]);
        }
        private void DropZone_Click(object s, System.Windows.Input.MouseButtonEventArgs e)
            => BrowseFile();

        private void BrowseFile()
        {
            var dlg = new OpenFileDialog
            {
                Filter = _isPdf
                    ? "PDF files|*.pdf|All files|*.*"
                    : "CSV files|*.csv|All files|*.*"
            };
            if (dlg.ShowDialog() == true) LoadFile(dlg.FileName);
        }

        private void ReExtract_Click(object s, RoutedEventArgs e)
        {
            if (_selectedFilePath != null && _isPdf)
                LoadFile(_selectedFilePath);
        }

        // ─────────────────────────────────────────────────────────────────────
        //  File loading  (runs extraction, populates grid)
        // ─────────────────────────────────────────────────────────────────────

        private async void LoadFile(string path)
        {
            string ext = Path.GetExtension(path).ToLower();

            // Basic extension check
            if (_isPdf && ext != ".pdf")
            { SetStatus("⚠  Please drop a PDF file.", isError: true); return; }
            if (!_isPdf && ext != ".csv" && ext != ".txt")
            { SetStatus("⚠  Please drop a CSV file.", isError: true); return; }

            _selectedFilePath = path;
            ShowFileInfo(path);
            ClearGrid();
            SetStatus("Reading file...");
            BtnSave.IsEnabled = false;

            try
            {
                List<List<string>>? rawGrid = null;
                List<string> warnings = new();
                string detectedModel = "";

                if (_isPdf)
                {
                    // PDF: run on background thread
                    ProgressBar.Visibility = Visibility.Visible;
                    int page = int.TryParse(TxtPageNumber.Text, out int p) ? p : 0;

                    var result = await Task.Run(() =>
                        LoadChartPdfExtractor.ExtractRawGrid(path, page));

                    rawGrid = result.Cells;
                    warnings = result.Warnings;
                    detectedModel = result.DetectedCraneModel;

                    ProgressBar.Visibility = Visibility.Collapsed;

                    // Auto-fill model if not already typed
                    if (string.IsNullOrEmpty(TxtModel.Text) && !string.IsNullOrEmpty(detectedModel))
                        TxtModel.Text = detectedModel;
                }
                else
                {
                    // CSV: instant, no async needed
                    rawGrid = ReadCsvAsRawGrid(path, out warnings);
                }

                if (rawGrid == null || rawGrid.Count < 2)
                {
                    SetStatus("⚠  Could not extract table data from this file.", isError: true);
                    return;
                }

                _rawGrid = rawGrid; // store for axis-flip
                AxisPanel.Visibility = Visibility.Visible;
                RboRowsRadius.IsChecked = true; // reset to default on new file load

                PopulateGrid(rawGrid);
                ShowWarnings(warnings);

                RefreshGridStats(rawGrid);
                RefreshChartSummary(rawGrid);
                SetStatus(warnings.Count > 0
                    ? $"⚠  Extracted with {warnings.Count} warning(s) — please review highlighted cells."
                    : "✅  Data extracted — review and click Save when ready.");

                BtnSave.IsEnabled = true;
                _hasData = true;
            }
            catch (Exception ex)
            {
                ProgressBar.Visibility = Visibility.Collapsed;
                SetStatus($"❌  {ex.Message}", isError: true);
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Axis orientation (swap rows ↔ columns)
        // ─────────────────────────────────────────────────────────────────────

        private void AxisOrientation_Changed(object sender, RoutedEventArgs e)
        {
            if (_rawGrid == null) return;
            var grid = RboRowsBoom.IsChecked == true ? TransposeGrid(_rawGrid) : _rawGrid;
            PopulateGrid(grid);
            RefreshGridStats(grid);
        }

        /// <summary>
        /// Transposes the rawGrid so rows become columns and vice-versa.
        /// The first row/column header is re-labeled accordingly.
        /// </summary>
        private static List<List<string>> TransposeGrid(List<List<string>> grid)
        {
            if (grid.Count == 0) return grid;

            int rows = grid.Count;
            int cols = grid[0].Count;

            var transposed = new List<List<string>>();
            for (int c = 0; c < cols; c++)
            {
                var newRow = new List<string>();
                for (int r = 0; r < rows; r++)
                    newRow.Add(r < grid.Count && c < grid[r].Count ? grid[r][c] : "");
                transposed.Add(newRow);
            }

            // Fix the corner header label
            if (transposed.Count > 0 && transposed[0].Count > 0)
                transposed[0][0] = "Radius (m)";

            return transposed;
        }

        private void RefreshGridStats(List<List<string>> grid)
        {
            int rows = grid.Count - 1;
            int cols = grid[0].Count - 1;

            // Feature 05: completeness score
            int totalCells = rows * cols;
            int ratedCells = 0;
            for (int r = 1; r < grid.Count; r++)
                for (int c = 1; c < grid[r].Count; c++)
                    if (!string.IsNullOrEmpty(grid[r][c])) ratedCells++;
            double completePct = totalCells > 0 ? ratedCells * 100.0 / totalCells : 0;
            string statsText = $"{cols} boom lengths × {rows} radius rows = {ratedCells} rated / {totalCells} possible ({completePct:F0}%)";
            TxtGridStats.Text = statsText;
            if (completePct < 30 && totalCells > 0)
                SetStatus("⚠ Sparse chart — only " + $"{completePct:F0}% of cells are rated. Check table was read correctly.", false);
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Crane Derating slider — set hidden TxtCalibration value
        // ─────────────────────────────────────────────────────────────────────

        private void SldDerating_ValueChanged(object sender,
            System.Windows.RoutedPropertyChangedEventArgs<double> e)
        {
            if (TxtCalibration == null || TxtDeratingVal == null) return;

            int val = (int)(Math.Round(SldDerating.Value / 5.0) * 5);
            val = Math.Max(25, Math.Min(100, val));

            TxtDeratingVal.Text = $"{val}%";

            // Colour: green at 100 %, amber 75–95 %, red below 75 %
            TxtDeratingVal.Foreground = val == 100
                ? new SolidColorBrush(Color.FromRgb(0x3A, 0xAA, 0x5A))
                : val >= 75
                    ? new SolidColorBrush(Color.FromRgb(0xF3, 0x9C, 0x12))
                    : new SolidColorBrush(Color.FromRgb(0xC0, 0x50, 0x50));

            TxtCalibration.Text = val.ToString();
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Calibration change — re-render the grid with the new factor
        // ─────────────────────────────────────────────────────────────────────

        private void Calibration_Changed(object sender,
            System.Windows.Controls.TextChangedEventArgs e)
        {
            if (_rawGrid == null) return;
            var grid = RboRowsBoom.IsChecked == true ? TransposeGrid(_rawGrid) : _rawGrid;
            PopulateGrid(grid);
            RefreshGridStats(grid);
            RefreshChartSummary(grid);
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Auto-compute max rated capacity from the raw grid (100% baseline)
        // ─────────────────────────────────────────────────────────────────────

        private static double? GetMaxCapacityFromGrid(List<List<string>> grid)
        {
            double max = double.MinValue;
            bool found = false;
            foreach (var row in grid.Skip(1))
                foreach (var cell in row.Skip(1))
                    if (!string.IsNullOrEmpty(cell) &&
                        double.TryParse(cell, NumberStyles.Any,
                            CultureInfo.InvariantCulture, out double v) && v > 0)
                    { if (v > max) { max = v; found = true; } }
            return found ? max : (double?)null;
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Chart Summary Panel — auto-computes stats from chart + entered data
        // ─────────────────────────────────────────────────────────────────────

        private void RefreshChartSummary(List<List<string>>? grid)
        {
            if (grid == null || grid.Count < 2) return;

            // ── Radius values (column 0, skip header) ─────────────────────────
            var radii = grid.Skip(1)
                .Select(r => r.Count > 0 ? r[0] : "")
                .Where(s => double.TryParse(s, NumberStyles.Any,
                                CultureInfo.InvariantCulture, out _))
                .Select(s => double.Parse(s, CultureInfo.InvariantCulture))
                .OrderBy(v => v).ToList();

            // ── Boom lengths (header row, col>0, strip dedup suffix) ───────────
            var booms = grid[0].Skip(1)
                .Select(h => System.Text.RegularExpressions.Regex
                                .Replace(h?.Trim() ?? "", @"_\d+$", ""))
                .Where(s => double.TryParse(s, NumberStyles.Any,
                                CultureInfo.InvariantCulture, out _))
                .Select(s => double.Parse(s, CultureInfo.InvariantCulture))
                .OrderBy(v => v).ToList();

            TxtSummaryMinRadius.Text = radii.Count > 0 ? $"{radii.First():F1} m" : "—";
            TxtSummaryMaxRadius.Text = radii.Count > 0 ? $"{radii.Last():F1} m" : "—";
            TxtSummaryMinBoom.Text = booms.Count > 0 ? $"{booms.First():F1} m" : "—";
            TxtSummaryMaxBoom.Text = booms.Count > 0 ? $"{booms.Last():F1} m" : "—";

            // ── Auto-fill Max Radius field (user can edit afterwards) ──────────
            if (radii.Count > 0 && string.IsNullOrWhiteSpace(TxtMaxRadius.Text))
                TxtMaxRadius.Text = $"{radii.Last():F1}";

            // ── Calibration ────────────────────────────────────────────────────
            bool hasCalib = double.TryParse(TxtCalibration.Text.Trim(),
                NumberStyles.Any, CultureInfo.InvariantCulture, out double calibPct)
                && calibPct > 0;
            double effectiveCalib = hasCalib ? calibPct : 100.0;
            TxtSummaryCalib.Text = $"{effectiveCalib:F0}%";

            // ── Max capacity from raw data (always 100% baseline) ──────────────
            var rawMaxCap = GetMaxCapacityFromGrid(_rawGrid ?? grid);
            double? displayMax = rawMaxCap.HasValue ? rawMaxCap * (effectiveCalib / 100.0) : (double?)null;
            TxtSummaryMaxCap.Text = displayMax.HasValue
                ? $"{displayMax.Value:F1} t  (at {effectiveCalib:F0}%)"
                : "— (load data)";
            // Leave TxtMaxCapacity blank — user must enter it manually
            // (do not auto-fill capacity)

            // ── Counterweight ──────────────────────────────────────────────────
            bool hasCW = double.TryParse(
                TxtCounterweight.Text.Replace(" ", "").Replace(",", "."),
                NumberStyles.Any, CultureInfo.InvariantCulture, out double cwKg) && cwKg > 0;
            TxtSummaryCW.Text = hasCW ? $"{cwKg / 1000:F1} t  ({cwKg:N0} kg)" : "— (not entered)";

            // ── Crane Bounding Radius ──────────────────────────────────────────
            if (_calcBaseRadiusM > 0)
            {
                double totalRadiusM = _calcBaseRadiusM + _radiusExtraM;
                TxtSummaryCG.Text =
                    $"R = {totalRadiusM:F2} m   ∅ = {totalRadiusM * 2:F2} m";
                TxtSummaryCGNote.Text =
                    $"Outrigger span: {_dimWidthMm / 1000:F2} m   " +
                    $"Span-to-span: {_dimOutriggerBaseMm / 1000:F2} m";
            }
            else
            {
                TxtSummaryCG.Text = "—";
                TxtSummaryCGNote.Text = "Enter Outrigger Span in Crane Dimensions for bounding radius.";
            }

            ChartSummaryPanel.Visibility = Visibility.Visible;
        }

        // Called whenever counterweight / calibration fields change
        private void CraneDetails_Changed(object sender,
            System.Windows.Controls.TextChangedEventArgs e)
        {
            if (_rawGrid != null) RefreshChartSummary(_rawGrid);
        }

        // ─────────────────────────────────────────────────────────────────────
        //  CSV parser — handles European decimals, semicolon separators,
        //               and multi-line merged cells
        // ─────────────────────────────────────────────────────────────────────

        private static List<List<string>> ReadCsvAsRawGrid(string path, out List<string> warnings)
        {
            warnings = new List<string>();
            var grid = new List<List<string>>();

            // Try UTF-8 first, fall back to system default (handles Windows-1252 European files)
            string content;
            try { content = File.ReadAllText(path, System.Text.Encoding.UTF8); }
            catch { content = File.ReadAllText(path, System.Text.Encoding.Default); }

            // ── Fix 1: Strip UTF-8 BOM (EF BB BF) if present ─────────────
            content = content.TrimStart('\uFEFF');

            // ── Auto-detect field separator (comma vs semicolon) ───────────
            char separator = DetectCsvSeparator(content);
            if (separator == ';')
                warnings.Add("Detected semicolon-separated CSV (European Excel format) — parsing automatically.");

            // Parse CSV properly — respects quoted fields that contain newlines
            var logicalRows = ParseCsvLogicalRows(content, separator);

            // Remove comment / blank rows
            logicalRows = logicalRows
                .Where(r => r.Count > 0
                         && !r[0].TrimStart().StartsWith("#")
                         && r.Any(c => !string.IsNullOrWhiteSpace(c)))
                .ToList();

            if (logicalRows.Count < 2) return grid;

            // ── Detect European decimal format (comma as decimal separator) ─
            bool isEuropean = separator == ';' || DetectEuropeanDecimal(logicalRows[0]);
            if (isEuropean && separator != ';')
                warnings.Add("Detected European number format (comma as decimal separator) — converting automatically.");

            // ── Fix 2: Handle two-row headers (e.g. row 0 = "Main boom length", row 1 = numbers) ─
            // If row 0 has no numeric values after column 0, but row 1 does → skip row 0.
            bool row0HasNumbers = logicalRows[0].Skip(1)
                .Any(h => double.TryParse(NormalizeCsvNumber(h, isEuropean),
                          NumberStyles.Any, CultureInfo.InvariantCulture, out _));

            if (!row0HasNumbers && logicalRows.Count >= 2)
            {
                warnings.Add("Two-row header detected — using second row as boom lengths.");
                logicalRows.RemoveAt(0);
            }

            // ── Build header row ───────────────────────────────────────────
            var headerCells = logicalRows[0];
            var boomColumns = new List<(int index, string boomLength)>();

            for (int i = 1; i < headerCells.Count; i++)
            {
                string h = NormalizeCsvNumber(headerCells[i], isEuropean);
                if (!string.IsNullOrEmpty(h)
                    && double.TryParse(h, NumberStyles.Any,
                           CultureInfo.InvariantCulture, out _))
                    boomColumns.Add((i, h));
            }

            if (boomColumns.Count == 0)
            {
                warnings.Add("No valid boom-length columns found in header row.");
                return grid;
            }

            // ── Fix 3: Deduplicate repeated boom lengths (e.g. same length, different jib config) ─
            var seenBooms = new Dictionary<string, int>();
            var deduplicatedBoomColumns = new List<(int index, string displayName)>();
            foreach (var (idx, boom) in boomColumns)
            {
                if (seenBooms.TryGetValue(boom, out int count))
                {
                    seenBooms[boom] = count + 1;
                    deduplicatedBoomColumns.Add((idx, $"{boom}_{count + 1}"));
                }
                else
                {
                    seenBooms[boom] = 1;
                    deduplicatedBoomColumns.Add((idx, boom));
                }
            }
            boomColumns = deduplicatedBoomColumns;

            var headerRow = new List<string> { "Radius (m)" };
            headerRow.AddRange(boomColumns.Select(b => b.boomLength));
            grid.Add(headerRow);

            // ── Build data rows (explode multi-line cells) ─────────────────
            for (int r = 1; r < logicalRows.Count; r++)
            {
                var row = logicalRows[r];

                // If any cell has embedded newlines, split into sub-rows
                var exploded = ExplodeMultiLineCells(row);

                foreach (var subRow in exploded)
                {
                    string radius = subRow.Count > 0
                        ? NormalizeCsvNumber(subRow[0], isEuropean) : "";

                    if (string.IsNullOrEmpty(radius)
                        || !double.TryParse(radius, NumberStyles.Any,
                                CultureInfo.InvariantCulture, out _))
                        continue;   // skip non-numeric radius

                    var dataRow = new List<string> { radius };
                    foreach (var (idx, _) in boomColumns)
                    {
                        string val = idx < subRow.Count
                            ? NormalizeCsvNumber(subRow[idx], isEuropean) : "";
                        dataRow.Add(val);
                    }
                    grid.Add(dataRow);
                }
            }

            return grid;
        }

        /// <summary>
        /// Auto-detects whether the CSV uses comma or semicolon as the field separator.
        /// European Excel exports (Windows with European locale) use semicolons.
        /// </summary>
        private static char DetectCsvSeparator(string content)
        {
            // Count occurrences on first few data lines (skip comment lines)
            int semicolons = 0, commas = 0;
            int linesChecked = 0;

            foreach (string line in content.Split('\n').Take(20))
            {
                string trimmed = line.Trim();
                if (string.IsNullOrEmpty(trimmed) || trimmed.StartsWith("#")) continue;

                bool inQuotes = false;
                foreach (char c in trimmed)
                {
                    if (c == '"') { inQuotes = !inQuotes; continue; }
                    if (inQuotes) continue;
                    if (c == ';') semicolons++;
                    else if (c == ',') commas++;
                }

                linesChecked++;
                if (linesChecked >= 5) break;
            }

            // Use semicolon if it appears as frequently or more than commas,
            // OR if semicolons appear more than twice (handles lines where commas
            // are decimal separators and semicolons are field separators).
            return (semicolons > 0 && semicolons >= commas / 2) ? ';' : ',';
        }

        /// <summary>
        /// Full CSV parser that respects quoted fields containing newlines.
        /// Supports both comma and semicolon as field separators.
        /// </summary>
        private static List<List<string>> ParseCsvLogicalRows(string content, char separator = ',')
        {
            var rows = new List<List<string>>();
            var currentRow = new List<string>();
            var cell = new System.Text.StringBuilder();
            bool inQuotes = false;
            int i = 0;

            while (i < content.Length)
            {
                char c = content[i];

                if (c == '"')
                {
                    // Escaped "" inside quotes → literal quote
                    if (inQuotes && i + 1 < content.Length && content[i + 1] == '"')
                    { cell.Append('"'); i += 2; continue; }
                    inQuotes = !inQuotes;
                    i++;
                }
                else if (c == separator && !inQuotes)
                {
                    currentRow.Add(cell.ToString());
                    cell.Clear();
                    i++;
                }
                else if (!inQuotes && (c == '\r' || c == '\n'))
                {
                    // End of logical row
                    currentRow.Add(cell.ToString());
                    cell.Clear();
                    if (currentRow.Any(s => !string.IsNullOrWhiteSpace(s)))
                        rows.Add(currentRow);
                    currentRow = new List<string>();
                    if (c == '\r' && i + 1 < content.Length && content[i + 1] == '\n') i++;
                    i++;
                }
                else
                {
                    cell.Append(c);
                    i++;
                }
            }

            // Flush last row
            currentRow.Add(cell.ToString());
            if (currentRow.Any(s => !string.IsNullOrWhiteSpace(s)))
                rows.Add(currentRow);

            return rows;
        }

        /// <summary>
        /// If any cell contains embedded newlines (from merged Excel rows),
        /// split the single row into multiple sub-rows.
        /// </summary>
        private static List<List<string>> ExplodeMultiLineCells(List<string> row)
        {
            int maxLines = row.Max(c => c.Split('\n').Length);
            if (maxLines <= 1) return new() { row };

            var result = new List<List<string>>();
            for (int line = 0; line < maxLines; line++)
            {
                var newRow = new List<string>();
                foreach (var cell in row)
                {
                    var parts = cell.Split('\n');
                    newRow.Add(line < parts.Length ? parts[line].Trim() : "");
                }
                result.Add(newRow);
            }
            return result;
        }

        /// <summary>
        /// Detect European decimal format by checking if header cells contain "13,1"-style values.
        /// This is only relevant for comma-separated CSVs; semicolon-separated files always
        /// treat commas as decimal separators.
        /// </summary>
        private static bool DetectEuropeanDecimal(List<string> headerRow)
        {
            var re = new System.Text.RegularExpressions.Regex(@"\d+,\d");
            return headerRow.Skip(1).Any(h => re.IsMatch(h));
        }

        /// <summary>
        /// Strip "m" suffix and convert European comma-decimal to dot-decimal.
        /// e.g. "13,1 m" → "13.1",  "86" → "86",  "3,5" → "3.5",  "42,2" → "42.2"
        /// Also strips thousand-separator dots: "1.200" → "1200" (handled as non-decimal dot).
        /// </summary>
        private static string NormalizeCsvNumber(string value, bool europeanFormat)
        {
            if (string.IsNullOrWhiteSpace(value)) return "";
            string clean = value.Trim().TrimEnd('m', ' ').Trim();

            if (europeanFormat)
            {
                // Replace decimal comma: "42,2" → "42.2", "3,5" → "3.5"
                clean = System.Text.RegularExpressions.Regex.Replace(
                    clean, @"(\d),(\d)", "$1.$2");

                // Remove thousand-separator dots that are NOT decimal points:
                // e.g. "1.200" in European format means 1200, not 1.2
                // Only remove dots where they appear as thousand separators (3 digits after)
                clean = System.Text.RegularExpressions.Regex.Replace(
                    clean, @"(\d)\.(\d{3})(?!\d*\.)", "$1$2");
            }

            return clean;
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Grid population  (fills DataGrid from raw string grid)
        // ─────────────────────────────────────────────────────────────────────

        private void PopulateGrid(List<List<string>> rawGrid)
        {
            _table = new DataTable();

            // ── Read calibration factor ────────────────────────────────────────
            double calibFactor = 1.0;
            if (TxtCalibration != null &&
                double.TryParse(TxtCalibration.Text.Trim(), NumberStyles.Any,
                    CultureInfo.InvariantCulture, out double calibPct) && calibPct > 0)
                calibFactor = calibPct / 100.0;

            // ── Add columns with safe unique internal names ────────────────────
            // Display headers strip the dedup suffix (_2, _3 …) so the grid shows
            // "43.3" / "43.3" / "43.3" exactly as in the Excel — matching reality.
            int colCount = rawGrid[0].Count;
            var displayHeaders = new List<string>();

            for (int c = 0; c < colCount; c++)
            {
                string raw = rawGrid[0][c].Trim();
                if (string.IsNullOrEmpty(raw)) raw = c == 0 ? "Radius (m)" : $"Col{c}";

                // Strip dedup suffix: "43.3_2" → "43.3"
                string display = System.Text.RegularExpressions.Regex
                                    .Replace(raw, @"_\d+$", "");

                // Round numeric boom headers to 1 decimal → "43.32" becomes "43.3",
                // "47.63" becomes "47.6" — matching how Excel displays these values.
                if (c > 0 && double.TryParse(display, NumberStyles.Any,
                        CultureInfo.InvariantCulture, out double headerNum))
                    display = headerNum.ToString("F1", CultureInfo.InvariantCulture);

                displayHeaders.Add(display);
                _table.Columns.Add($"C{c}", typeof(string));
            }

            // ── Add data rows, applying calibration to capacity cells ──────────
            for (int r = 1; r < rawGrid.Count; r++)
            {
                var row = _table.NewRow();
                for (int c = 0; c < colCount; c++)
                {
                    string val = c < rawGrid[r].Count ? rawGrid[r][c] : "";

                    // Apply calibration to every capacity cell (col>0), keep radius intact
                    if (c > 0 && calibFactor != 1.0 && !string.IsNullOrEmpty(val) &&
                        double.TryParse(val, NumberStyles.Any,
                            CultureInfo.InvariantCulture, out double cap))
                    {
                        val = (cap * calibFactor).ToString("G4", CultureInfo.InvariantCulture);
                    }

                    row[c] = val;
                }
                _table.Rows.Add(row);
            }

            // ── Build DataGrid columns with indexed binding ────────────────────
            PreviewGrid.AutoGenerateColumns = false;
            PreviewGrid.Columns.Clear();

            for (int c = 0; c < colCount; c++)
            {
                var col = new System.Windows.Controls.DataGridTextColumn
                {
                    Header = displayHeaders[c],   // "43.3" not "43.3_2"
                    Binding = new System.Windows.Data.Binding($"[C{c}]")
                    {
                        Mode = System.Windows.Data.BindingMode.TwoWay,
                        UpdateSourceTrigger = System.Windows.Data.UpdateSourceTrigger.LostFocus
                    },
                    Width = c == 0
                        ? new System.Windows.Controls.DataGridLength(80)
                        : new System.Windows.Controls.DataGridLength(55)
                };
                PreviewGrid.Columns.Add(col);
            }

            PreviewGrid.ItemsSource = _table.DefaultView;
            GridEmptyState.Visibility = Visibility.Collapsed;
            PreviewGrid.Visibility = Visibility.Visible;
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Cell edit tracking (for validation after user edits)
        // ─────────────────────────────────────────────────────────────────────

        private void PreviewGrid_CellEditEnding(object? sender,
            System.Windows.Controls.DataGridCellEditEndingEventArgs e)
        {
            // After user edits a cell, re-validate the value is numeric
            Dispatcher.BeginInvoke(new Action(() =>
            {
                if (_table == null) return;
                int row = e.Row.GetIndex();
                int col = e.Column.DisplayIndex;

                if (col == 0) return; // radius column — allow any

                var view = (DataView)PreviewGrid.ItemsSource;
                if (row >= view.Count) return;

                string val = view[row][col]?.ToString()?.Trim() ?? "";
                if (!string.IsNullOrEmpty(val) &&
                    !double.TryParse(val, NumberStyles.Any,
                        CultureInfo.InvariantCulture, out _))
                {
                    // Highlight in status
                    string colName = _table.Columns[col].ColumnName;
                    SetStatus($"⚠  Cell value \"{val}\" at {colName} is not a number — enter a number or leave empty.", isError: true);
                }
                else
                {
                    SetStatus("✅  Data looks good — click Save when ready.");
                }
            }), System.Windows.Threading.DispatcherPriority.Background);
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Save to database
        // ─────────────────────────────────────────────────────────────────────

        private async void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            if (_table == null || !_hasData) return;

            // Convert DataTable → entities
            var (chart, points, errors) = BuildEntitiesFromTable();

            // Deduplicate points by (RadiusM, BoomLengthM) before saving.
            // This prevents the "An item with the same key has already been added" crash
            // that occurs when a CSV column header appears twice (e.g. two "43.3 m" columns).
            points = points
                .GroupBy(p => (p.RadiusM, p.BoomLengthM))
                .Select(g => g.Last())
                .ToList();

            if (errors.Count > 0)
            {
                SetStatus($"❌  Fix these errors first:\n{string.Join("\n", errors)}", isError: true);
                return;
            }

            BtnSave.IsEnabled = false;
            BtnCancel.IsEnabled = false;
            SetStatus("Saving to database...");

            try
            {
                using var db = DatabaseService.Instance.CreateContext();

                // ── Validate user-entered capacity ────────────────────────────
                if (!double.TryParse(
                        TxtMaxCapacity.Text.Replace(" ", "").Replace(",", "."),
                        NumberStyles.Any, CultureInfo.InvariantCulture, out double userCapTonne)
                    || userCapTonne <= 0)
                {
                    SetStatus("❌  Please enter the max capacity in tonnes before saving.", isError: true);
                    BtnSave.IsEnabled = true;
                    BtnCancel.IsEnabled = true;
                    return;
                }

                // ── Feature 06: duplicate detection (new charts only) ─────────
                string model   = chart.CraneModel;
                string chartTy = chart.ChartType;
                string outCfg  = chart.OutriggerConfig;

                if (_editingChartId < 0)
                {
                    var dup = await db.CraneLoadCharts
                        .FirstOrDefaultAsync(c => c.CraneModel == model && c.ChartType == chartTy && c.OutriggerConfig == outCfg);
                    if (dup != null)
                    {
                        var dupResult = MessageBox.Show(
                            $"A chart for '{model}' ({chartTy} / {outCfg}) already exists.\n\nReplace it, or keep both?",
                            "Duplicate Chart", MessageBoxButton.YesNoCancel, MessageBoxImage.Warning);
                        if (dupResult == MessageBoxResult.Cancel)
                        {
                            BtnSave.IsEnabled = true;
                            BtnCancel.IsEnabled = true;
                            SetStatus("Save cancelled.");
                            return;
                        }
                        if (dupResult == MessageBoxResult.Yes)
                        {
                            db.CraneLoadCharts.Remove(dup);
                            await db.SaveChangesAsync();
                        }
                        // No = keep both, just continue saving
                    }
                }

                // ── Edit mode: update existing entity ─────────────────────────
                if (_editingChartId > 0)
                {
                    var existingChart = await db.CraneLoadCharts
                        .Include(c => c.Points)
                        .FirstOrDefaultAsync(c => c.Id == _editingChartId);
                    if (existingChart != null)
                    {
                        existingChart.CraneModel      = chart.CraneModel;
                        existingChart.Manufacturer    = chart.Manufacturer;
                        existingChart.ConfigName      = chart.ConfigName;
                        existingChart.ChartType       = chart.ChartType;
                        existingChart.OutriggerConfig = chart.OutriggerConfig;
                        existingChart.Notes           = chart.Notes;
                        existingChart.DeratingPct     = chart.DeratingPct;
                        existingChart.CraneCategory   = chart.CraneCategory;
                        existingChart.OutriggerSpanMm = chart.OutriggerSpanMm;
                        existingChart.TotalLengthMm   = chart.TotalLengthMm;
                        existingChart.HeightMm        = chart.HeightMm;
                        existingChart.TransportWidthMm = chart.TransportWidthMm;
                        existingChart.SpanToSpanMm    = chart.SpanToSpanMm;
                        existingChart.BoundingRadiusM = chart.BoundingRadiusM;
                        // Replace points
                        db.CraneLoadChartPoints.RemoveRange(existingChart.Points);
                        existingChart.Points.Clear();
                        foreach (var p in points) { p.ChartId = existingChart.Id; existingChart.Points.Add(p); }
                        db.CraneLoadCharts.Update(existingChart);
                        await db.SaveChangesAsync();
                        ImportedChart = existingChart;
                        ImportedPoints = existingChart.Points.ToList();
                        SetStatus($"✅  Chart updated: {existingChart.CraneModel}.");
                        BtnSave.Content = "✓ Close";
                        BtnSave.IsEnabled = true;
                        BtnSave.Click -= BtnSave_Click;
                        BtnSave.Click += (_, __) => DialogResult = true;
                        BtnCancel.IsEnabled = true;
                        return;
                    }
                }

                // ── Optionally save to Crane Catalog ──────────────────────────
                if (ChkSaveToCatalog.IsChecked == true && !string.IsNullOrEmpty(chart.CraneModel))
                {
                    // Use user-entered capacity; get radius from TxtMaxRadius (auto-filled from CSV, editable)
                    double maxCapacity = userCapTonne;
                    double autoReach = points.Any() ? points.Max(p => p.RadiusM) : 0;
                    double maxReach = double.TryParse(
                        TxtMaxRadius.Text.Replace(" ", "").Replace(",", "."),
                        NumberStyles.Any, CultureInfo.InvariantCulture, out double userRad) && userRad > 0
                        ? userRad : autoReach;

                    // Check if catalog entry already exists for this model
                    var existingCatalog = await db.CraneCatalog
                        .FirstOrDefaultAsync(c => c.Model == chart.CraneModel
                                               && c.Manufacturer == chart.Manufacturer);

                    if (existingCatalog == null)
                    {
                        var catalogEntry = new CraneCatalogEntity
                        {
                            CraneType = chart.CraneCategory,   // "Mobile" or "Tower" from radio button
                            Manufacturer = chart.Manufacturer,
                            Model = chart.CraneModel,
                            Description = $"{chart.ChartType} — {chart.OutriggerConfig}",
                            MaxCapacityKg = maxCapacity * 1000,   // tonnes → kg
                            MaxReachM = maxReach,
                            IsSystem = false
                        };
                        await db.CraneCatalog.AddAsync(catalogEntry);
                        await db.SaveChangesAsync();

                        // Link chart to catalog entry
                        chart.CatalogId = catalogEntry.Id;
                    }
                    else
                    {
                        chart.CatalogId = existingCatalog.Id;
                    }
                }

                await db.CraneLoadCharts.AddAsync(chart);
                await db.SaveChangesAsync();

                points.ForEach(p => p.ChartId = chart.Id);
                await db.CraneLoadChartPoints.AddRangeAsync(points);
                await db.SaveChangesAsync();

                // Auto-save CSV backup if requested
                string statusMsg;
                if (ChkSaveCsv.IsChecked == true)
                {
                    try
                    {
                        string csvPath = LoadChartCsvService.GetDefaultExportPath(chart);
                        await Task.Run(() =>
                            LoadChartCsvService.ExportToCsv(chart, points, csvPath));
                        statusMsg = $"✅  Saved {points.Count} points. CSV backup: {csvPath}";
                    }
                    catch
                    {
                        statusMsg = $"✅  Saved {points.Count} points. (CSV backup failed — not critical)";
                    }
                }
                else
                {
                    statusMsg = $"✅  Saved {points.Count} data points for {chart.CraneModel}.";
                }

                if (ChkSaveToCatalog.IsChecked == true)
                    statusMsg += " Added to Crane Catalog.";

                SetStatus(statusMsg);

                ImportedChart = chart;
                ImportedPoints = points;

                BtnSave.Content = "✓ Close";
                BtnSave.IsEnabled = true;
                BtnSave.Click -= BtnSave_Click;
                BtnSave.Click += (_, __) => DialogResult = true;
                BtnCancel.IsEnabled = true;
            }
            catch (Exception ex)
            {
                // Build the full exception chain so the root SQLite error is visible
                var msg = new System.Text.StringBuilder(ex.Message);
                var inner = ex.InnerException;
                while (inner != null)
                {
                    msg.Append($"\n  ➜  {inner.Message}");
                    inner = inner.InnerException;
                }
                SetStatus($"❌  Database error: {msg}", isError: true);
                BtnSave.IsEnabled = true;
                BtnCancel.IsEnabled = true;
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Build entities from the (possibly user-edited) DataTable
        // ─────────────────────────────────────────────────────────────────────

        private (CraneLoadChartEntity chart,
                  List<CraneLoadChartPointEntity> points,
                  List<string> errors)
            BuildEntitiesFromTable()
        {
            var errors = new List<string>();
            var points = new List<CraneLoadChartPointEntity>();

            if (_table == null)
            { errors.Add("No data table."); return (new CraneLoadChartEntity(), points, errors); }

            // ── Read boom lengths from the DataGrid column HEADERS ─────────────
            // (Internal DataTable column names are safe "C0","C1",... — not parseable as numbers.
            //  The actual boom length values live in the DataGridColumn.Header property.)
            var boomLengths = new List<double>();
            int gridColCount = PreviewGrid.Columns.Count;

            for (int c = 1; c < gridColCount; c++) // skip column 0 = radius
            {
                string header = PreviewGrid.Columns[c].Header?.ToString()?.Trim() ?? "";

                // Strip deduplication suffix: "43.3_2" → "43.3", "52_2" → "52"
                string clean = System.Text.RegularExpressions.Regex.Replace(header, @"_\d+$", "");
                clean = clean.TrimEnd('m', ' ').Trim();

                if (!double.TryParse(clean, NumberStyles.Any,
                        CultureInfo.InvariantCulture, out double boom))
                {
                    errors.Add($"Column header \"{header}\" is not a valid boom length number.");
                    continue;
                }
                boomLengths.Add(boom);
            }

            // ── Parse rows using DataTable column index (not name) ─────────────
            var view = (DataView)PreviewGrid.ItemsSource;
            for (int r = 0; r < view.Count; r++)
            {
                string radiusCell = view[r][0]?.ToString()?.Trim() ?? "";
                if (!double.TryParse(radiusCell, NumberStyles.Any,
                        CultureInfo.InvariantCulture, out double radius))
                {
                    errors.Add($"Row {r + 1}: radius value \"{radiusCell}\" is not a number.");
                    continue;
                }

                for (int c = 0; c < boomLengths.Count; c++)
                {
                    string cellVal = (c + 1 < _table.Columns.Count)
                        ? view[r][c + 1]?.ToString()?.Trim() ?? ""
                        : "";

                    double? capacity = null;
                    if (!string.IsNullOrEmpty(cellVal))
                    {
                        if (!double.TryParse(cellVal, NumberStyles.Any,
                                CultureInfo.InvariantCulture, out double cap))
                        {
                            errors.Add($"Row {r + 1} / Column \"{_table.Columns[c + 1].ColumnName}\": " +
                                       $"\"{cellVal}\" is not a valid number.");
                            continue;
                        }
                        capacity = cap;
                    }

                    points.Add(new CraneLoadChartPointEntity
                    {
                        RadiusM = radius,
                        BoomLengthM = boomLengths[c],
                        CapacityTonne = capacity
                    });
                }
            }

            string craneCategory = RboCraneTypeTower?.IsChecked == true ? "Tower" : "Mobile";

            var chart = new CraneLoadChartEntity
            {
                CraneModel = TxtModel.Text.Trim(),
                Manufacturer = TxtManufacturer.Text.Trim(),
                ConfigName = "",
                CraneCategory = craneCategory,
                ChartType = (CboChartType.SelectedItem as System.Windows.Controls.ComboBoxItem)
                                  ?.Content?.ToString() ?? "360°",
                OutriggerConfig = RbOutrigger75.IsChecked == true ? "75%"
                                : RbOutrigger50.IsChecked == true ? "50%"
                                : RbOutriggerRubber.IsChecked == true ? "On Rubber"
                                : "Full",
                SourceImagePath = _selectedFilePath ?? "",
                ImportMethod = _isPdf ? "PDF" : "CSV",
                ImportedAt = DateTime.UtcNow,

                // ── Derating from slider ────────────────────────────────────
                DeratingPct = int.TryParse(TxtCalibration.Text, out int derPct) ? derPct : 100,

                // ── New fields from dialog inputs ───────────────────────────
                CounterweightKg = double.TryParse(
                    TxtCounterweight.Text.Replace(" ", "").Replace(",", "."),
                    NumberStyles.Any, CultureInfo.InvariantCulture, out double cw) ? cw : 0,

                // ── Physical dimensions (from Crane Dimensions overlay) ─────
                OutriggerSpanMm    = _dimWidthMm,
                TotalLengthMm      = _dimTotalLengthMm,
                HeightMm           = _dimHeightMm,
                TransportWidthMm   = _dimTransportWidthMm,
                SpanToSpanMm       = _dimOutriggerBaseMm,
                BoundingRadiusM    = _calcBaseRadiusM + _radiusExtraM,
            };
            return (chart, points, errors);
        }

        // ─────────────────────────────────────────────────────────────────────
        //  CSV template download
        // ─────────────────────────────────────────────────────────────────────

        private void DownloadTemplate_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new SaveFileDialog
            {
                FileName = "LoadChart_Template.csv",
                Filter = "CSV files|*.csv"
            };
            if (dlg.ShowDialog() != true) return;

            const string template =
                "# Crane Load Chart CSV Template\n" +
                "# HOW TO USE:\n" +
                "#   Row 1: First cell = 'Radius', then boom lengths in metres\n" +
                "#   Row 2+: First cell = working radius in metres, then capacity in tonnes\n" +
                "#   Leave a cell EMPTY if the crane cannot lift at that combination\n" +
                "#   Capacity = TONNES   |   Radius and boom lengths = METRES\n" +
                "#\n" +
                "Radius,13.2,17.7,22.2,26.7,31.3,35.8,40.3,44.8\n" +
                "3,200,133.8,124.4,120.5,,,\n" +
                "3.5,142,131.3,125.5,120.7,,,\n" +
                "4,133,123.6,123.6,119,112.8,,\n" +
                "4.5,125,115.4,115.3,115.1,111.4,,\n" +
                "5,117,107.7,107.8,107.6,107.2,89.1,\n" +
                "6,105,98.5,95.4,94.7,94.4,87.1,71.1\n" +
                "7,93,84.5,85.1,84.4,84,82.9,69.5,54.1\n";

            File.WriteAllText(dlg.FileName, template);
            MessageBox.Show(
                $"Template saved to:\n{dlg.FileName}\n\n" +
                "• Open in Excel or Notepad\n" +
                "• Replace the example numbers with your crane's data\n" +
                "• Save as CSV\n" +
                "• Drop the file back into this dialog",
                "Template Saved", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        // ─────────────────────────────────────────────────────────────────────
        //  UI helpers
        // ─────────────────────────────────────────────────────────────────────

        private void ShowFileInfo(string path)
        {
            TxtFileName.Text = Path.GetFileName(path);
            long size = new FileInfo(path).Length;
            TxtFileSize.Text = size > 1024 * 1024
                ? $"{size / 1024.0 / 1024.0:F1} MB"
                : $"{size / 1024.0:F0} KB";
            FileInfoPanel.Visibility = Visibility.Visible;
        }

        private void ShowWarnings(List<string> warnings)
        {
            if (warnings.Count == 0)
            {
                WarningsPanel.Visibility = Visibility.Collapsed;
                return;
            }
            WarningsList.ItemsSource = warnings;
            WarningsPanel.Visibility = Visibility.Visible;
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Crane Dimensions Overlay
        // ─────────────────────────────────────────────────────────────────────

        private void BtnCraneDimensions_Click(object sender, RoutedEventArgs e)
        {
            if (_dimTotalLengthMm > 0) TxtDimTotalLength.Text = _dimTotalLengthMm.ToString("F0");
            if (_dimHeightMm > 0) TxtDimHeight.Text = _dimHeightMm.ToString("F0");
            if (_dimTransportWidthMm > 0) TxtDimTransportWidth.Text = _dimTransportWidthMm.ToString("F0");
            if (_dimWidthMm > 0) TxtDimOutriggerSpan.Text = _dimWidthMm.ToString("F0");
            if (_dimOutriggerBaseMm > 0) TxtDimOutriggerBase.Text = _dimOutriggerBaseMm.ToString("F0");
            UpdateBoundingRadius();
            DimOverlay.Visibility = Visibility.Visible;
        }

        private void DimOverlay_Cancel(object sender, RoutedEventArgs e)
            => DimOverlay.Visibility = Visibility.Collapsed;

        private void DimOverlay_Apply(object sender, RoutedEventArgs e)
        {
            // Require outrigger span (most critical dimension)
            bool ok = double.TryParse(TxtDimOutriggerSpan.Text.Trim(),
                NumberStyles.Any, CultureInfo.InvariantCulture, out _dimWidthMm) && _dimWidthMm > 0;
            if (!ok)
            {
                MessageBox.Show("Please enter at least the Outrigger Span (width).",
                    "Missing Required Field", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            double.TryParse(TxtDimTotalLength.Text.Trim(),
                NumberStyles.Any, CultureInfo.InvariantCulture, out _dimTotalLengthMm);
            double.TryParse(TxtDimHeight.Text.Trim(),
                NumberStyles.Any, CultureInfo.InvariantCulture, out _dimHeightMm);
            double.TryParse(TxtDimTransportWidth.Text.Trim(),
                NumberStyles.Any, CultureInfo.InvariantCulture, out _dimTransportWidthMm);
            double.TryParse(TxtDimOutriggerBase.Text.Trim(),
                NumberStyles.Any, CultureInfo.InvariantCulture, out _dimOutriggerBaseMm);

            // Compute and store bounding radius
            double halfW = _dimWidthMm / 2000.0;
            double halfB = _dimOutriggerBaseMm > 0 ? _dimOutriggerBaseMm / 2000.0 : halfW;
            _calcBaseRadiusM = Math.Sqrt(halfW * halfW + halfB * halfB);
            double totalRadiusM = _calcBaseRadiusM + _radiusExtraM;

            // ── Build summary badge ────────────────────────────────────────────
            var sb = new System.Text.StringBuilder();
            if (_dimTotalLengthMm > 0) sb.AppendLine($"Length:           {_dimTotalLengthMm / 1000:F2} m");
            if (_dimHeightMm > 0) sb.AppendLine($"Height:           {_dimHeightMm / 1000:F2} m");
            if (_dimTransportWidthMm > 0) sb.AppendLine($"Travel width:     {_dimTransportWidthMm / 1000:F2} m  (incl. tires)");
            if (_dimWidthMm > 0) sb.AppendLine($"Outrigger span:   {_dimWidthMm / 1000:F2} m");
            if (_dimOutriggerBaseMm > 0) sb.AppendLine($"Span to span:     {_dimOutriggerBaseMm / 1000:F2} m");
            if (_calcBaseRadiusM > 0)
                sb.AppendLine($"Bounding radius:  {totalRadiusM:F2} m  (base {_calcBaseRadiusM:F2} + extra {_radiusExtraM:F1})");

            DimEnteredBadge.Visibility = Visibility.Visible;
            TxtDimSummary.Text = sb.ToString().TrimEnd();
            DimOverlay.Visibility = Visibility.Collapsed;

            if (_rawGrid != null) RefreshChartSummary(_rawGrid);
        }

        private void DimField_Changed(object sender,
            System.Windows.Controls.TextChangedEventArgs e)
            => UpdateBoundingRadius();

        private void RadiusMinus_Click(object sender, RoutedEventArgs e)
        {
            _radiusExtraM = Math.Max(0, _radiusExtraM - 0.5);
            UpdateBoundingRadius();
        }

        private void RadiusPlus_Click(object sender, RoutedEventArgs e)
        {
            _radiusExtraM = Math.Min(10, _radiusExtraM + 0.5);
            UpdateBoundingRadius();
        }

        private void UpdateBoundingRadius()
        {
            if (TxtBoundingRadius == null) return;

            bool hasOW = double.TryParse(TxtDimOutriggerSpan.Text.Trim(),
                NumberStyles.Any, CultureInfo.InvariantCulture, out double spanMm) && spanMm > 0;
            bool hasOB = double.TryParse(TxtDimOutriggerBase.Text.Trim(),
                NumberStyles.Any, CultureInfo.InvariantCulture, out double baseMm) && baseMm > 0;

            if (hasOW)
            {
                double halfW = spanMm / 2000.0;
                double halfB = hasOB ? baseMm / 2000.0 : halfW;
                _calcBaseRadiusM = Math.Sqrt(halfW * halfW + halfB * halfB);
                double totalM = _calcBaseRadiusM + _radiusExtraM;

                TxtCalcRadius.Text = $"{_calcBaseRadiusM:F2} m";
                TxtBoundingRadius.Text = totalM.ToString("F2");

                string footprint = hasOB
                    ? $"{spanMm / 1000:F2} m × {baseMm / 1000:F2} m"
                    : $"{spanMm / 1000:F2} m (square assumed)";
                TxtRadiusInfo.Text =
                    $"Footprint: {footprint}\n" +
                    $"Base radius: {_calcBaseRadiusM:F2} m  +  extra: {_radiusExtraM:F1} m\n" +
                    $"Total: {totalM:F2} m  |  Clearance ∅: {totalM * 2:F2} m";
            }
            else
            {
                _calcBaseRadiusM = 0;
                TxtCalcRadius.Text = "—";
                TxtBoundingRadius.Text = "—";
                TxtRadiusInfo.Text = "Enter Outrigger Span (W) to calculate.";
            }
        }

        private void ClearGrid_Click(object s, RoutedEventArgs e) => ClearAll();

        private void ClearGrid()
        {
            PreviewGrid.ItemsSource = null;
            PreviewGrid.Columns.Clear();
            _table = null;
            _hasData = false;
            GridEmptyState.Visibility = Visibility.Visible;
            PreviewGrid.Visibility = Visibility.Collapsed;
            WarningsPanel.Visibility = Visibility.Collapsed;
            TxtGridStats.Text = "No data yet — drop a file on the left.";
            BtnSave.IsEnabled = false;
        }

        private void ClearAll()
        {
            ClearGrid();
            _selectedFilePath = null;
            _rawGrid = null;
            AxisPanel.Visibility = Visibility.Collapsed;
            FileInfoPanel.Visibility = Visibility.Collapsed;
            ChartSummaryPanel.Visibility = Visibility.Collapsed;
            DimOverlay.Visibility = Visibility.Collapsed;
            // Clear auto-filled fields so next file gets fresh auto-fill
            TxtMaxRadius.Text = "";
            SetStatus("Drop a file to begin.");
        }

        private void SetStatus(string msg, bool isError = false)
        {
            TxtStatus.Text = msg;
            TxtStatus.Foreground = isError
                ? new SolidColorBrush(Color.FromRgb(0xCC, 0x44, 0x44))
                : new SolidColorBrush(Color.FromRgb(0x33, 0x77, 0x88));
        }

        private void ResetDropBorder()
            => DropZone.BorderBrush =
                new SolidColorBrush(Color.FromRgb(0x15, 0x65, 0xA8));

        private void BtnCancel_Click(object s, RoutedEventArgs e)
            => DialogResult = false;

        // ─────────────────────────────────────────────────────────────────────
        //  Feature 07: Unit conversion handlers
        // ─────────────────────────────────────────────────────────────────────

        private void CboCapacityUnit_Changed(object sender, SelectionChangedEventArgs e)
        {
            if (CboCapacityUnit?.SelectedItem is System.Windows.Controls.ComboBoxItem item && item.Tag is string tagStr
                && double.TryParse(tagStr, System.Globalization.NumberStyles.Any,
                    System.Globalization.CultureInfo.InvariantCulture, out double factor))
            {
                _capacityConversionFactor = factor;
            }
        }

        private void CboDistanceUnit_Changed(object sender, SelectionChangedEventArgs e)
        {
            if (CboDistanceUnit?.SelectedItem is System.Windows.Controls.ComboBoxItem item && item.Tag is string tagStr
                && double.TryParse(tagStr, System.Globalization.NumberStyles.Any,
                    System.Globalization.CultureInfo.InvariantCulture, out double factor))
            {
                _distanceConversionFactor = factor;
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Feature 02: Heatmap toggle
        // ─────────────────────────────────────────────────────────────────────

        private void TglHeatmap_Changed(object sender, RoutedEventArgs e)
        {
            _heatmapEnabled = TglHeatmap?.IsChecked == true;
            if (_hasData && _rawGrid != null)
                ApplyHeatmapToGrid();
        }

        private void ApplyHeatmapToGrid()
        {
            if (PreviewGrid == null) return;

            // Collect all numeric values to find min/max
            var values = new List<double>();
            for (int r = 1; r < (_rawGrid?.Count ?? 0); r++)
                for (int c = 1; c < _rawGrid![r].Count; c++)
                    if (double.TryParse(_rawGrid[r][c], System.Globalization.NumberStyles.Any,
                        System.Globalization.CultureInfo.InvariantCulture, out double v))
                        values.Add(v);

            if (values.Count == 0) return;
            _heatmapMin = values.Min();
            _heatmapMax = values.Max();

            // Apply or clear cell backgrounds
            foreach (var row in PreviewGrid.Items)
            {
                var rowContainer = PreviewGrid.ItemContainerGenerator.ContainerFromItem(row) as System.Windows.Controls.DataGridRow;
                if (rowContainer == null) continue;
                var presenter = FindVisualChild<System.Windows.Controls.Primitives.DataGridCellsPresenter>(rowContainer);
                if (presenter == null) continue;
                for (int c = 1; c < PreviewGrid.Columns.Count; c++)
                {
                    var cell = presenter.ItemContainerGenerator.ContainerFromIndex(c) as System.Windows.Controls.DataGridCell;
                    if (cell == null) continue;
                    var tb = FindVisualChild<TextBlock>(cell);
                    if (tb == null) continue;
                    if (_heatmapEnabled && double.TryParse(tb.Text, System.Globalization.NumberStyles.Any,
                        System.Globalization.CultureInfo.InvariantCulture, out double val))
                        cell.Background = GetHeatmapBrush(val, _heatmapMin, _heatmapMax);
                    else
                        cell.Background = Brushes.Transparent;
                }
            }
        }

        private static System.Windows.Media.SolidColorBrush GetHeatmapBrush(double value, double min, double max)
        {
            double t = max > min ? (value - min) / (max - min) : 0.5;
            t = Math.Max(0, Math.Min(1, t));
            // Green (low) → Yellow → Red (high)
            byte r = (byte)(t < 0.5 ? (t * 2 * 200) : 200);
            byte g = (byte)(t < 0.5 ? 180 : ((1 - t) * 2 * 180));
            byte b = 20;
            return new System.Windows.Media.SolidColorBrush(Color.FromRgb(r, g, b));
        }

        private static T? FindVisualChild<T>(System.Windows.DependencyObject parent) where T : System.Windows.DependencyObject
        {
            for (int i = 0; i < System.Windows.Media.VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = System.Windows.Media.VisualTreeHelper.GetChild(parent, i);
                if (child is T t) return t;
                var result = FindVisualChild<T>(child);
                if (result != null) return result;
            }
            return null;
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Feature 04: Auto-fill manufacturer from model name
        // ─────────────────────────────────────────────────────────────────────

        private void TxtModel_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(TxtManufacturer?.Text))
            {
                string mfg = GuessManufacturer(TxtModel.Text);
                if (!string.IsNullOrEmpty(mfg))
                {
                    TxtManufacturer.Text = mfg;
                    if (TxtAutoDetect != null)
                    {
                        TxtAutoDetect.Text = "(auto-detected)";
                        TxtAutoDetect.Visibility = Visibility.Visible;
                    }
                }
            }
        }

        private static string GuessManufacturer(string modelText)
        {
            if (string.IsNullOrWhiteSpace(modelText)) return "";
            string upper = modelText.ToUpperInvariant();
            if (upper.StartsWith("LTM") || upper.StartsWith("LTC") || upper.StartsWith("LG ") || upper.StartsWith("LG-") || upper.StartsWith("LTR")) return "Liebherr";
            if (upper.StartsWith("GMK") || upper.StartsWith("GTK") || upper.StartsWith("AT ") || upper.StartsWith("RT ") || upper.StartsWith("GROVE")) return "Grove";
            if (upper.StartsWith("AC ") || upper.StartsWith("ATC") || upper.StartsWith("ATF") || upper.StartsWith("TADANO")) return "Tadano";
            if (upper.StartsWith("TG-") || upper.StartsWith("TR-") || upper.StartsWith("TG ") || upper.StartsWith("TR ")) return "Tadano";
            if (upper.StartsWith("CC ") || upper.StartsWith("CC-") || upper.StartsWith("LR ") || upper.StartsWith("LS-") || upper.StartsWith("MANITOWOC")) return "Manitowoc";
            if (upper.StartsWith("LINK-BELT") || upper.StartsWith("LMT") || upper.StartsWith("HTC") || upper.StartsWith("TCC")) return "Link-Belt";
            if (upper.StartsWith("TEREX") || upper.StartsWith("CTL") || upper.StartsWith("CTT")) return "Terex";
            if (upper.StartsWith("LIEBHERR")) return "Liebherr";
            return "";
        }
    }
}