using CraneOptimizer.UI.Features.F4_LoadChart.Services;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

using System.Windows.Threading;
namespace CraneOptimizer.UI.Features.F4_LoadChart.Views
{
    public partial class PdfLoadChartImportDialog : Window
    {
        private readonly PdfLoadChartScanResult _scanResult;
        private readonly List<PdfPageCandidateDto> _orderedPages;

        internal LoadChartImportResult? ImportResult { get; private set; }

        internal PdfLoadChartImportDialog(PdfLoadChartScanResult scanResult)
        {
            InitializeComponent();
            _scanResult = scanResult ?? throw new ArgumentNullException(nameof(scanResult));
            _orderedPages = _scanResult.Pages
                .OrderBy(page => page.PageNumber)
                .ToList();
            PagesList.ItemsSource = _orderedPages;

            TxtDocumentSummary.Text =
                $"{System.IO.Path.GetFileName(_scanResult.PdfPath)}" +
                Environment.NewLine +
                $"{_scanResult.PageCount} page(s) scanned - {_scanResult.AllTables.Count} table candidate(s) detected";

            BtnImportSelected.IsEnabled = false;
            Loaded += PdfLoadChartImportDialog_Loaded;
        }

        private void PdfLoadChartImportDialog_Loaded(object sender, RoutedEventArgs e)
        {
            var bestTable = _scanResult.GetBestTable();
            var bestPage = bestTable == null
                ? _orderedPages.FirstOrDefault()
                : _orderedPages.FirstOrDefault(page => page.PageNumber == bestTable.PageNumber);

            if (bestPage != null)
            {
                PagesList.SelectedItem = bestPage;

                if (bestTable != null)
                    TablesList.SelectedItem = bestTable;
            }

            TxtBottomStatus.Text = _scanResult.Warnings.Count > 0
                ? string.Join(" ", _scanResult.Warnings)
                : "Select the correct page/table, review the parsed import preview, then import.";
        }

        private void PagesList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (PagesList.SelectedItem is not PdfPageCandidateDto page)
            {
                TablesList.ItemsSource = null;
                ClearPreviewGrid();
                TxtPageDetails.Text = string.Empty;
                TxtTableDetails.Text = string.Empty;
                TxtSelectionSummary.Text = "Select a page and table candidate.";
                TxtWarnings.Text = string.Empty;
                BtnImportSelected.IsEnabled = false;
                return;
            }

            TxtPageDetails.Text = page.DisplayDetails;
            ScrollSelectedPageIntoView();
            TablesList.ItemsSource = page.Tables;

            if (page.Tables.Count > 0)
            {
                TablesList.SelectedItem = page.Tables.First();
            }
            else
            {
                ClearPreviewGrid();
                TxtTableDetails.Text = "No table candidates detected on this page.";
                TxtSelectionSummary.Text = $"Page {page.PageNumber} - no importable table detected";
                TxtWarnings.Text = string.IsNullOrWhiteSpace(page.TextPreview)
                    ? "No selectable text preview is available for this page."
                    : "Text preview:" + Environment.NewLine + page.TextPreview;
                BtnImportSelected.IsEnabled = false;
            }
        }


        private void ScrollSelectedPageIntoView()
        {
            if (PagesList.SelectedItem is not PdfPageCandidateDto selectedPage)
                return;

            PagesList.Dispatcher.BeginInvoke(
                new Action(() =>
                {
                    if (!ReferenceEquals(PagesList.SelectedItem, selectedPage))
                        return;

                    PagesList.UpdateLayout();
                    PagesList.ScrollIntoView(selectedPage);
                }),
                DispatcherPriority.Loaded);
        }
        private void TablesList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (TablesList.SelectedItem is not PdfTableCandidateDto table)
            {
                ClearPreviewGrid();
                TxtTableDetails.Text = string.Empty;
                TxtSelectionSummary.Text = "Select a page and table candidate.";
                TxtWarnings.Text = string.Empty;
                BtnImportSelected.IsEnabled = false;
                return;
            }

            TxtTableDetails.Text = table.DisplayDetails;
            TxtSelectionSummary.Text =
                $"Page {table.PageNumber} - Table {table.TableIndex} - {table.BoomColumnCount} boom columns - {table.RadiusRowCount} radius rows - {table.NonEmptyCapacityCellCount} cells - {table.ConfidenceLabel}";

            ShowParsedImportPreview(table);
        }

        private void ShowParsedImportPreview(PdfTableCandidateDto table)
        {
            var rawNotes = table.Warnings.Distinct().ToList();

            if (table.HasSeparateImportGrid)
            {
                ShowExactVisualEnvelopePreview(table, rawNotes);
                return;
            }

            try
            {
                // This is intentionally the same parser used by ImportSelectedTable(...).
                // The preview should show what will be imported, not a separate raw/intermediate grid.
                var parsedChart = LoadChartCsvService.ParseRawGrid(table.RawGrid);

                ConfigurePreviewGrid(parsedChart.PreviewTable, parsedChart.PreviewHeaders);
                PreviewGrid.ItemsSource = parsedChart.PreviewTable.DefaultView;

                int radiusCount = parsedChart.Points
                    .Select(point => point.Radius)
                    .Distinct()
                    .Count();

                int boomCount = parsedChart.Points
                    .Select(point => point.BoomLength)
                    .Distinct()
                    .Count();

                TxtWarnings.Text = BuildImportNotes(table, parsedChart, rawNotes);

                TxtBottomStatus.Text =
                    $"Ready to import - {parsedChart.Points.Count} real capacity points - {radiusCount} radii - {boomCount} boom lengths - max capacity {parsedChart.MaxCapacity:0.###} t.";

                BtnImportSelected.IsEnabled = true;
            }
            catch (Exception ex)
            {
                var fallbackHeaders = new List<string>();
                var fallbackTable = BuildSafeRawPreviewTable(table.RawGrid, fallbackHeaders);

                ConfigurePreviewGrid(fallbackTable, fallbackHeaders);
                PreviewGrid.ItemsSource = fallbackTable.DefaultView;

                rawNotes.Insert(0, "This candidate cannot currently be parsed into the import model.");
                rawNotes.Insert(1, ex.Message);

                TxtWarnings.Text = string.Join(Environment.NewLine, rawNotes);
                TxtBottomStatus.Text = "Raw candidate preview only. Import is disabled for this candidate because parsing failed.";
                BtnImportSelected.IsEnabled = false;
            }
        }

        private static string BuildImportNotes(
            PdfTableCandidateDto table,
            ParsedLoadChart parsedChart,
            IReadOnlyList<string> rawNotes)
        {
            int radiusCount = parsedChart.Points
                .Select(point => point.Radius)
                .Distinct()
                .Count();

            int boomCount = parsedChart.Points
                .Select(point => point.BoomLength)
                .Distinct()
                .Count();

            bool cleanHighConfidenceImport =
                parsedChart.Points.Count == table.NonEmptyCapacityCellCount
                && table.ConfidenceLabel.Contains("High", StringComparison.OrdinalIgnoreCase);

            var lines = new List<string>
            {
                $"Ready to import {parsedChart.Points.Count} real capacity point(s) from {table.NonEmptyCapacityCellCount} non-empty PDF capacity cell(s).",
                $"Detected {radiusCount} radius row(s), {boomCount} unique boom length(s), and max capacity {parsedChart.MaxCapacity:0.###} t."
            };

            foreach (string note in rawNotes)
            {
                if (string.IsNullOrWhiteSpace(note))
                    continue;

                if (note.StartsWith("Row-stream extractor", StringComparison.OrdinalIgnoreCase))
                {
                    lines.Add("Parsed visual PDF rows as radius values followed by ordered capacity values.");
                    continue;
                }

                if (note.StartsWith("X positions were used", StringComparison.OrdinalIgnoreCase))
                {
                    lines.Add("Column alignment was inferred from PDF word positions. Review the grid before saving.");
                    continue;
                }

                if (note.StartsWith("No load-chart keywords", StringComparison.OrdinalIgnoreCase)
                    || note.StartsWith("Nearby load-chart keywords", StringComparison.OrdinalIgnoreCase))
                {
                    if (!cleanHighConfidenceImport)
                        lines.Add("Nearby load-chart keywords were not detected. Verify the parsed grid before saving.");

                    continue;
                }

                lines.Add(note);
            }

            return string.Join(Environment.NewLine, lines.Distinct());
        }

        private void ShowExactVisualEnvelopePreview(
            PdfTableCandidateDto table,
            IReadOnlyList<string> rawNotes)
        {
            var exactHeaders = new List<string>();
            var exactTable = BuildSafeRawPreviewTable(table.RawGrid, exactHeaders);

            ConfigurePreviewGrid(exactTable, exactHeaders);
            PreviewGrid.ItemsSource = exactTable.DefaultView;

            ParsedLoadChart? importChart = null;
            var notes = rawNotes.Distinct().ToList();

            try
            {
                importChart = LoadChartCsvService.ParseRawGrid(table.ImportGrid);
            }
            catch (Exception ex)
            {
                notes.Insert(0, "The exact PDF visual grid is displayed below, but the consolidated import grid failed to parse.");
                notes.Insert(1, ex.Message);
                TxtWarnings.Text = string.Join(Environment.NewLine, notes);
                TxtBottomStatus.Text = "Exact PDF visual preview only. Import is disabled because the consolidated import grid failed.";
                BtnImportSelected.IsEnabled = false;
                return;
            }

            int exactVisualColumns = table.RawGrid.Count > 0 ? Math.Max(0, table.RawGrid[0].Count - 1) : 0;
            int exactVisualRows = Math.Max(0, table.RawGrid.Count - 1);
            int importBoomCount = importChart.Points
                .Select(point => point.BoomLength)
                .Distinct()
                .Count();
            int importRadiusCount = importChart.Points
                .Select(point => point.Radius)
                .Distinct()
                .Count();

            notes.Insert(0, $"Exact PDF visual chart is shown below: {exactVisualColumns} visual boom columns x {exactVisualRows} radius rows.");
            notes.Insert(1, $"Save Crane will save the exact visual chart, including repeated PDF boom columns. The {importBoomCount}-boom 2D envelope remains available later only as a reference view.");
            notes.Insert(2, "Repeated PDF boom headers are preserved as separate visual columns. Duplicate headers may be shown with UI suffixes such as (2), (3), etc.");
            notes.Insert(3, "For optimization, duplicate same-radius/same-boom visual columns use the conservative minimum capacity policy.");

            TxtWarnings.Text = string.Join(Environment.NewLine, notes.Distinct());
            TxtBottomStatus.Text =
                $"Exact PDF visual chart - {exactVisualColumns} visual boom columns - {exactVisualRows} radii. Import will preserve and save the exact visual chart; reference envelope: {importChart.Points.Count} points, max {importChart.MaxCapacity:0.###} t.";

            BtnImportSelected.IsEnabled = true;
        }
        private void ImportSelected_Click(object sender, RoutedEventArgs e)
        {
            if (TablesList.SelectedItem is not PdfTableCandidateDto table)
            {
                MessageBox.Show(
                    this,
                    "Please select a table candidate before importing.",
                    "PDF Load Chart Import",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                return;
            }

            if (RequiresEnvelopeImportReview(table) && !ConfirmEnvelopeImport(table))
                return;

            try
            {
                ImportResult = LoadChartPdfService.ImportSelectedTable(_scanResult, table);
                DialogResult = true;
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    this,
                    ex.Message,
                    "Selected Table Cannot Be Imported",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
        }

        private bool ConfirmEnvelopeImport(PdfTableCandidateDto table)
        {
            var envelopeNotes = table.Warnings
                .Where(IsEnvelopeWarning)
                .Take(6)
                .ToList();

            string noteText = envelopeNotes.Count == 0
                ? "This candidate was generated by a repeated-header geometry fallback. The exact visual columns will now be preserved and saved."
                : string.Join(Environment.NewLine + "- ", envelopeNotes);

            string message =
                "This table has repeated visual boom columns." +
                Environment.NewLine + Environment.NewLine +
                "The import will now preserve and save the exact PDF visual chart, including duplicate visual columns such as 39.1, 39.1 (2), and 39.1 (3)." +
                Environment.NewLine + Environment.NewLine +
                "For optimization, duplicate same-radius/same-boom columns will use the conservative minimum capacity, not the optimistic maximum envelope." +
                Environment.NewLine + Environment.NewLine +
                "Import notes:" +
                Environment.NewLine +
                "- " + noteText +
                Environment.NewLine + Environment.NewLine +
                "Continue importing this exact visual chart?";

            return MessageBox.Show(
                this,
                message,
                "Confirm Exact Visual Load Chart Import",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning,
                MessageBoxResult.Yes) == MessageBoxResult.Yes;
        }

        private static bool RequiresEnvelopeImportReview(PdfTableCandidateDto table)
        {
            return table.IsEnvelopeCandidate || table.Warnings.Any(IsEnvelopeWarning);
        }

        private static bool IsEnvelopeWarning(string warning)
        {
            if (string.IsNullOrWhiteSpace(warning))
                return false;

            return warning.Contains("ENVELOPE_IMPORT_REVIEW_REQUIRED", StringComparison.OrdinalIgnoreCase)
                || warning.Contains("maximum-capacity envelope", StringComparison.OrdinalIgnoreCase)
                || warning.Contains("consolidated into unique boom columns", StringComparison.OrdinalIgnoreCase)
                || warning.Contains("Full manufacturer visual columns detected", StringComparison.OrdinalIgnoreCase);
        }
        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void ClearPreviewGrid()
        {
            PreviewGrid.ItemsSource = null;
            PreviewGrid.Columns.Clear();
        }

        private void ConfigurePreviewGrid(DataTable table, IReadOnlyList<string> headers)
        {
            PreviewGrid.AutoGenerateColumns = false;
            PreviewGrid.Columns.Clear();

            for (int columnIndex = 0; columnIndex < table.Columns.Count; columnIndex++)
            {
                string header = columnIndex < headers.Count
                    ? headers[columnIndex]
                    : table.Columns[columnIndex].ColumnName;

                PreviewGrid.Columns.Add(new DataGridTextColumn
                {
                    Header = header,
                    Binding = new Binding($"[{table.Columns[columnIndex].ColumnName}]")
                    {
                        Mode = BindingMode.OneWay
                    },
                    Width = columnIndex == 0
                        ? new DataGridLength(88)
                        : new DataGridLength(60),
                    MinWidth = columnIndex == 0 ? 76 : 52
                });
            }
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
    }
}