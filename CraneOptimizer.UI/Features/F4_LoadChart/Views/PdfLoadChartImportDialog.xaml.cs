using CraneOptimizer.UI.Features.F4_LoadChart.Services;
using PDFtoImage;
using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using UglyToad.PdfPig;
namespace CraneOptimizer.UI.Features.F4_LoadChart.Views
{
    public partial class PdfLoadChartImportDialog : Window
    {
        private const int SourcePreviewRenderWidth = 1600;
        private const double SourcePreviewMinimumZoom = 0.20;
        private const double SourcePreviewMaximumZoom = 4.00;

        private readonly PdfLoadChartScanResult _scanResult;
        private readonly List<PdfPageCandidateDto> _orderedPages;
        private PdfTableCandidateDto? _currentSourcePreviewTable;
        private PdfTableCandidateDto? _renderedSourcePreviewTable;
        private double _sourcePreviewZoom = 1.0;
        private double _sourcePreviewPageWidth;
        private double _sourcePreviewPageHeight;
        private double _sourcePreviewImagePixelWidth;
        private double _sourcePreviewImagePixelHeight;
        private bool _isSourcePreviewVisible = true;
        private GridLength _sourcePreviewColumnWidth = new GridLength(1, GridUnitType.Star);

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
            InitializeSourcePreviewToggleState();
            ClearSourcePdfPreview("Select a detected table to render the original PDF page.");
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
                ClearSourcePdfPreview("Select a page and detected table to render the original PDF page.");
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
                // UI_SEMANTIC_DIAGNOSTICS_PATCH_V1
                // Selecting a page should default to the adjudicated/recommended table on that page, not the first raw fragment.
                var recommendedTable = _scanResult.GetBestTable();
                var tableToSelect = page.Tables.FirstOrDefault(table => SameTable(table, recommendedTable))
                    ?? page.Tables.First();

                TablesList.SelectedItem = tableToSelect;
            }
            else
            {
                ClearPreviewGrid();
                ClearSourcePdfPreview("No table candidate is available to highlight on this page.");
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
                ClearSourcePdfPreview("Select a detected table to render the original PDF page.");
                TxtTableDetails.Text = string.Empty;
                TxtSelectionSummary.Text = "Select a page and table candidate.";
                TxtWarnings.Text = string.Empty;
                BtnImportSelected.IsEnabled = false;
                return;
            }

            TxtTableDetails.Text = BuildCandidateDetailsText(table);
            TxtSelectionSummary.Text = BuildSelectionSummaryText(table, null, isExactVisualPreview: false);

            ShowParsedImportPreview(table);
            ShowSourcePdfPreview(table);
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

                TxtWarnings.Text = AddSelectionContextNotes(table, BuildImportNotes(table, parsedChart, rawNotes));
                TxtSelectionSummary.Text = BuildSelectionSummaryText(table, parsedChart.VisualSummary, isExactVisualPreview: false);

                TxtBottomStatus.Text = BuildStandardBottomStatus(parsedChart);

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

            var visualSummary = parsedChart.VisualSummary ?? LoadChartVisualChartSummary.Empty;
            int capacityRadiusRows = visualSummary.CapacityRadiusRowCount > 0
                ? visualSummary.CapacityRadiusRowCount
                : radiusCount;
            int visualBoomColumns = visualSummary.VisualBoomColumnCount > 0
                ? visualSummary.VisualBoomColumnCount
                : table.BoomColumnCount;
            int numericRatedCapacityCells = visualSummary.NumericRatedCapacityCellCount > 0
                ? visualSummary.NumericRatedCapacityCellCount
                : table.NonEmptyCapacityCellCount;
            int capacityGridSlots = visualSummary.CapacityGridSlotCount > 0
                ? visualSummary.CapacityGridSlotCount
                : Math.Max(0, table.BoomColumnCount * table.RadiusRowCount);

            bool cleanHighConfidenceImport =
                parsedChart.Points.Count >= numericRatedCapacityCells
                && table.ConfidenceLabel.Contains("High", StringComparison.OrdinalIgnoreCase);

            var lines = new List<string>
            {
                $"Ready to import {parsedChart.Points.Count} generated capacity lookup point(s) from {numericRatedCapacityCells} numeric rated-capacity cell(s).",
                $"Detected {capacityRadiusRows} capacity radius row(s), {visualSummary.LineRequirementRowCount} line-requirement row(s), {visualBoomColumns} visual boom column(s), {boomCount} unique numeric boom length(s), and max capacity {parsedChart.MaxCapacity:0.###} t.",
                $"Visual grid semantics: {capacityGridSlots} capacity grid slot(s), {visualSummary.BlankUnavailableCellCount} blank/unavailable capacity slot(s), and {visualSummary.TotalVisualGridSlotCount} total visual grid slot(s)."
            };

            if (parsedChart.Points.Count != numericRatedCapacityCells)
            {
                lines.Add("Generated capacity lookup point count differs from numeric PDF cells when duplicate visual boom columns, repeated radius/boom lanes, or normalization rules expand the lookup model.");
            }

            if (visualSummary.HasOperationalLineRequirements || visualSummary.LineRequirementRowCount > 0 || table.LineRequirementRowCount > 0)
            {
                lines.Add(BuildLineRequirementPrecisionPhrase(table, visualSummary) + " The row remains visible and saved as operational metadata; it is not treated as a rated-capacity row.");
            }

            foreach (string note in rawNotes)
            {
                if (string.IsNullOrWhiteSpace(note))
                    continue;

                if (note.StartsWith("Row-stream extractor", StringComparison.OrdinalIgnoreCase))
                {
                    lines.Add("Parsed visual PDF rows as capacity radius rows followed by ordered rated-capacity values.");
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

        private static string BuildLineRequirementPrecisionPhrase(PdfTableCandidateDto table, object visualSummary)
        {
            int visualSlotCount = GetLineRequirementCountPropertyValue(visualSummary, "LineRequirementVisualSlotCount");
            int parsedValueCount = GetLineRequirementCountPropertyValue(visualSummary, "ParsedLineRequirementValueCount");
            int mappedColumnCount = GetLineRequirementCountPropertyValue(visualSummary, "MappedLineRequirementColumnCount");

            if (visualSlotCount <= 0 && table.LineRequirementRowCount > 0)
            {
                visualSlotCount = table.BoomColumnCount * Math.Max(1, table.LineRequirementRowCount);
            }

            if (mappedColumnCount <= 0 && parsedValueCount > 0)
            {
                mappedColumnCount = Math.Min(parsedValueCount, Math.Max(parsedValueCount, visualSlotCount));
            }

            return $"Line-requirement row preserved across {visualSlotCount} visual boom-column slot(s); {parsedValueCount} line value(s) parsed; {mappedColumnCount} value(s) mapped to boom columns.";
        }

        private static int GetLineRequirementCountPropertyValue(object source, string propertyName)
        {
            if (source == null || string.IsNullOrWhiteSpace(propertyName))
                return 0;

            try
            {
                var property = source.GetType().GetProperty(propertyName);
                if (property == null)
                    return 0;

                object? value = property.GetValue(source);
                if (value == null)
                    return 0;

                return Convert.ToInt32(value, System.Globalization.CultureInfo.InvariantCulture);
            }
            catch
            {
                return 0;
            }
        }
        private static string BuildLineRequirementPrecisionPhrase(LoadChartVisualChartSummary visualSummary)
        {
            if (visualSummary == null || !visualSummary.HasOperationalLineRequirements)
                return "No line-requirement / parts-of-line row detected.";

            return $"Line-requirement row preserved across {visualSummary.LineRequirementVisualSlotCount:0} visual boom-column slot(s); {visualSummary.ParsedLineRequirementValueCount:0} line value(s) parsed; {visualSummary.MappedLineRequirementColumnCount:0} value(s) mapped to boom columns.";
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
            ParsedLoadChart? exactVisualChart = null;
            var notes = rawNotes.Distinct().ToList();

            try
            {
                importChart = LoadChartCsvService.ParseRawGrid(table.ImportGrid);
            }
            catch (Exception ex)
            {
                notes.Insert(0, "The exact PDF visual grid is displayed below, but the consolidated import grid failed to parse.");
                notes.Insert(1, ex.Message);
                TxtWarnings.Text = AddSelectionContextNotes(table, string.Join(Environment.NewLine, notes));
                TxtBottomStatus.Text = "Exact PDF visual preview only. Import is disabled because the consolidated import grid failed.";
                BtnImportSelected.IsEnabled = false;
                return;
            }

            try
            {
                exactVisualChart = LoadChartCsvService.ParseExactVisualRawGrid(
                    table.RawGrid,
                    $"PDF page {table.PageNumber}, table {table.TableIndex}");
            }
            catch (Exception ex)
            {
                notes.Add("Exact visual semantic summary could not be rebuilt for preview diagnostics: " + ex.Message);
            }

            var visualSummary = exactVisualChart?.VisualSummary ?? LoadChartVisualChartSummary.Empty;
            int exactVisualColumns = visualSummary.VisualBoomColumnCount > 0
                ? visualSummary.VisualBoomColumnCount
                : (table.RawGrid.Count > 0 ? Math.Max(0, table.RawGrid[0].Count - 1) : 0);
            int totalVisualRows = Math.Max(0, table.RawGrid.Count - 1);
            int capacityRadiusRows = visualSummary.CapacityRadiusRowCount > 0
                ? visualSummary.CapacityRadiusRowCount
                : table.RadiusRowCount;
            int lineRequirementRows = visualSummary.LineRequirementRowCount;
            int numericRatedCapacityCells = visualSummary.NumericRatedCapacityCellCount > 0
                ? visualSummary.NumericRatedCapacityCellCount
                : table.NonEmptyCapacityCellCount;
            int blankUnavailableSlots = visualSummary.BlankUnavailableCellCount;
            int totalVisualGridSlots = visualSummary.TotalVisualGridSlotCount > 0
                ? visualSummary.TotalVisualGridSlotCount
                : Math.Max(0, exactVisualColumns * totalVisualRows);
            int importBoomCount = importChart.Points
                .Select(point => point.BoomLength)
                .Distinct()
                .Count();
            int importRadiusCount = importChart.Points
                .Select(point => point.Radius)
                .Distinct()
                .Count();

            notes.Insert(0, $"Exact PDF visual chart is shown below: {exactVisualColumns} visual boom column(s), {capacityRadiusRows} capacity radius row(s), {lineRequirementRows} line-requirement row(s), {totalVisualRows} total visual row(s); {numericRatedCapacityCells} numeric rated-capacity cell(s), {blankUnavailableSlots} blank/unavailable capacity slot(s), {totalVisualGridSlots} total visual grid slot(s).");
            if (visualSummary.HasOperationalLineRequirements || visualSummary.LineRequirementRowCount > 0 || table.LineRequirementRowCount > 0)
            {
                notes.Insert(1, BuildLineRequirementPrecisionPhrase(table, visualSummary) + " The line row remains visible and saved as operational metadata; it is not a rated-capacity row.");
            }
            notes.Add($"Reference 2D envelope remains available as a reference view: {importChart.Points.Count} generated point(s), {importRadiusCount} capacity radius value(s), {importBoomCount} unique numeric boom length(s), max {importChart.MaxCapacity:0.###} t.");
            notes.Add("Repeated PDF boom headers are preserved as separate visual columns. Duplicate headers may be shown with UI suffixes such as (2), (3), etc.");
            notes.Add("For optimization, duplicate same-radius/same-boom visual columns use the conservative minimum capacity policy.");

            TxtWarnings.Text = AddSelectionContextNotes(table, string.Join(Environment.NewLine, notes.Distinct()));
            TxtSelectionSummary.Text = BuildSelectionSummaryText(table, visualSummary, isExactVisualPreview: true);
            TxtBottomStatus.Text =
                $"Exact PDF visual chart - {exactVisualColumns} visual boom columns - {capacityRadiusRows} capacity radius rows - {lineRequirementRows} line-requirement rows - {numericRatedCapacityCells} numeric rated-capacity cells. Import preserves the exact visual chart; reference envelope: {importChart.Points.Count} points, max {importChart.MaxCapacity:0.###} t.";

            BtnImportSelected.IsEnabled = true;
        }

        private string BuildCandidateDetailsText(PdfTableCandidateDto table)
        {
            var lines = new List<string> { table.DisplayDetails };
            lines.AddRange(BuildSelectionContextLines(table));
            return string.Join(Environment.NewLine, lines.Where(line => !string.IsNullOrWhiteSpace(line)).Distinct());
        }

        private string BuildSelectionSummaryText(
            PdfTableCandidateDto table,
            LoadChartVisualChartSummary? visualSummary,
            bool isExactVisualPreview)
        {
            int visualBoomColumns = visualSummary != null && visualSummary.VisualBoomColumnCount > 0
                ? visualSummary.VisualBoomColumnCount
                : table.BoomColumnCount;
            int capacityRadiusRows = visualSummary != null && visualSummary.CapacityRadiusRowCount > 0
                ? visualSummary.CapacityRadiusRowCount
                : table.RadiusRowCount;
            int lineRequirementRows = visualSummary?.LineRequirementRowCount ?? 0;
            int numericRatedCapacityCells = visualSummary != null && visualSummary.NumericRatedCapacityCellCount > 0
                ? visualSummary.NumericRatedCapacityCellCount
                : table.NonEmptyCapacityCellCount;
            string rowSummary = lineRequirementRows > 0
                ? $"{capacityRadiusRows} capacity radius rows + {lineRequirementRows} line row(s)"
                : $"{capacityRadiusRows} capacity radius rows";
            string mode = isExactVisualPreview ? "exact visual" : "parsed";

            return $"Page {table.PageNumber} - Table {table.TableIndex} - {visualBoomColumns} visual boom columns - {rowSummary} - {numericRatedCapacityCells} numeric rated-capacity cells - {mode} - {GetSelectionRecommendationShortLabel(table)}";
        }

        private string BuildStandardBottomStatus(ParsedLoadChart parsedChart)
        {
            var visualSummary = parsedChart.VisualSummary ?? LoadChartVisualChartSummary.Empty;
            int capacityRadiusRows = visualSummary.CapacityRadiusRowCount > 0
                ? visualSummary.CapacityRadiusRowCount
                : parsedChart.Points.Select(point => point.Radius).Distinct().Count();
            int visualBoomColumns = visualSummary.VisualBoomColumnCount > 0
                ? visualSummary.VisualBoomColumnCount
                : parsedChart.Points.Select(point => point.BoomLength).Distinct().Count();

            return $"Ready to import - {parsedChart.Points.Count} generated capacity lookup points - {capacityRadiusRows} capacity radius rows - {visualBoomColumns} visual boom columns - max capacity {parsedChart.MaxCapacity:0.###} t.";
        }

        private string AddSelectionContextNotes(PdfTableCandidateDto table, string notesText)
        {
            var lines = BuildSelectionContextLines(table);
            if (!string.IsNullOrWhiteSpace(notesText))
            {
                lines.AddRange(notesText.Split(new[] { Environment.NewLine }, StringSplitOptions.None));
            }

            return string.Join(Environment.NewLine, lines.Where(line => !string.IsNullOrWhiteSpace(line)).Distinct());
        }

        // UI_PURPOSE_WARNING_ROUTING_PATCH_V2
        // UI_PAGE_BEST_AND_LINE_SPLIT_FIX_V2
        // UI_PAGE_BEST_AND_LINE_SPLIT_PATCH_V1
        private List<string> BuildSelectionContextLines(PdfTableCandidateDto table)
        {
            var lines = new List<string>();
            var recommended = GetRecommendedTable();
            bool isRecommended = SameTable(table, recommended);
            bool isCombinedContinuation = table.IsCombinedContinuationCandidate;
            bool isEnvelopeReference = IsEnvelopeReferenceCandidate(table);

            if (isRecommended)
            {
                if (isCombinedContinuation)
                {
                    lines.Add("Recommended combined primary chart selected: this table is the current adjudicated best candidate.");
                }
                else
                {
                    lines.Add("Recommended import candidate selected: this table is the current adjudicated best candidate.");
                }
            }
            else if (recommended != null)
            {
                if (table.PageNumber == recommended.PageNumber)
                {
                    lines.Add($"Manual override: this is not the recommended table on this page. Recommended import candidate is Table {recommended.TableIndex} ({recommended.CandidateKindLabel}).");
                }
                else
                {
                    lines.Add($"Manual override: current adjudicated recommendation is Page {recommended.PageNumber}, Table {recommended.TableIndex} ({recommended.CandidateKindLabel}).");
                }
            }

            if (isCombinedContinuation)
            {
                lines.Add("Combined continuation candidate: multiple visual table fragments were merged into one preferred import chart.");
            }

            if (isEnvelopeReference)
            {
                lines.Add("Envelope/reference fragment: this table is useful for review, but a combined primary chart should be preferred when available.");
            }

            return lines;
        }

        private static bool IsEnvelopeReferenceCandidate(PdfTableCandidateDto table)
        {
            return table.IsEnvelopeCandidate && !table.IsCombinedContinuationCandidate;
        }
        private string GetSelectionRecommendationShortLabel(PdfTableCandidateDto table)
        {
            var recommended = GetRecommendedTable();
            if (SameTable(table, recommended))
            {
                return table.IsCombinedContinuationCandidate
                    ? "Recommended combined primary chart"
                    : "Recommended import candidate";
            }

            return recommended == null
                ? "Candidate"
                : "Manual override candidate";
        }

        private PdfTableCandidateDto? GetRecommendedTable()
        {
            return _scanResult.GetBestTable();
        }

        private static bool SameTable(PdfTableCandidateDto? left, PdfTableCandidateDto? right)
        {
            if (left == null || right == null)
                return false;

            return left.PageNumber == right.PageNumber && left.TableIndex == right.TableIndex;
        }        private void InitializeSourcePreviewToggleState()
        {
            _isSourcePreviewVisible = true;
            _sourcePreviewColumnWidth = SourcePreviewColumn.Width.Value > 0
                ? SourcePreviewColumn.Width
                : new GridLength(1, GridUnitType.Star);

            SourcePdfPreviewPane.Visibility = Visibility.Visible;
            SourcePreviewSplitter.Visibility = Visibility.Visible;
            SourcePreviewSplitterColumn.Width = new GridLength(8);
            SourcePreviewColumn.MinWidth = 360;
            UpdateSourcePreviewToggleButton();
        }

        private void SourcePreviewVisibilityToggle_Click(object sender, RoutedEventArgs e)
        {
            SetSourcePreviewVisibility(BtnToggleSourcePreview.IsChecked == true, renderIfNeeded: true);
        }

        private void SetSourcePreviewVisibility(bool isVisible, bool renderIfNeeded)
        {
            if (_isSourcePreviewVisible == isVisible)
            {
                UpdateSourcePreviewToggleButton();
                return;
            }

            _isSourcePreviewVisible = isVisible;

            if (!isVisible)
            {
                if (SourcePreviewColumn.Width.Value > 0)
                    _sourcePreviewColumnWidth = SourcePreviewColumn.Width;

                SourcePdfPreviewPane.Visibility = Visibility.Collapsed;
                SourcePreviewSplitter.Visibility = Visibility.Collapsed;
                SourcePreviewSplitterColumn.Width = new GridLength(0);
                SourcePreviewColumn.MinWidth = 0;
                SourcePreviewColumn.Width = new GridLength(0);

                if (TxtSourcePreviewStatus != null && _currentSourcePreviewTable != null)
                {
                    TxtSourcePreviewStatus.Text =
                        $"Source PDF preview hidden for page {_currentSourcePreviewTable.PageNumber}, table {_currentSourcePreviewTable.TableIndex}.";
                }
            }
            else
            {
                SourcePdfPreviewPane.Visibility = Visibility.Visible;
                SourcePreviewSplitter.Visibility = Visibility.Visible;
                SourcePreviewSplitterColumn.Width = new GridLength(8);
                SourcePreviewColumn.MinWidth = 360;
                SourcePreviewColumn.Width = _sourcePreviewColumnWidth.Value > 0
                    ? _sourcePreviewColumnWidth
                    : new GridLength(1, GridUnitType.Star);

                if (renderIfNeeded && TablesList.SelectedItem is PdfTableCandidateDto table)
                {
                    if (!IsCurrentSourcePreviewRenderedFor(table))
                    {
                        ShowSourcePdfPreview(table);
                    }
                    else
                    {
                        ApplySourcePreviewZoom(scrollToTable: true);
                    }
                }
            }

            UpdateSourcePreviewToggleButton();
        }

        private bool IsCurrentSourcePreviewRenderedFor(PdfTableCandidateDto table)
        {
            return SourcePdfImage.Source != null
                && ReferenceEquals(_renderedSourcePreviewTable, table)
                && _sourcePreviewImagePixelWidth > 0
                && _sourcePreviewImagePixelHeight > 0;
        }

        private void UpdateSourcePreviewToggleButton()
        {
            if (BtnToggleSourcePreview == null)
                return;

            BtnToggleSourcePreview.IsChecked = _isSourcePreviewVisible;
            BtnToggleSourcePreview.Content = _isSourcePreviewVisible
                ? "Hide PDF Preview"
                : "Show PDF Preview";
            BtnToggleSourcePreview.ToolTip = _isSourcePreviewVisible
                ? "Hide the original PDF preview pane and expand the parsed grid."
                : "Show the original PDF preview pane beside the parsed grid.";
        }
        private void ShowSourcePdfPreview(PdfTableCandidateDto table)
        {
            _currentSourcePreviewTable = table;

            if (!_isSourcePreviewVisible)
            {
                TxtSourcePreviewStatus.Text = $"Source PDF preview hidden for page {table.PageNumber}, table {table.TableIndex}.";
                return;
            }

            _sourcePreviewZoom = 1.0;
            TxtSourcePreviewStatus.Text = $"Rendering page {table.PageNumber}, table {table.TableIndex}...";

            string? temporaryPngPath = null;

            try
            {
                (_sourcePreviewPageWidth, _sourcePreviewPageHeight) = GetPdfPageSize(table.PageNumber);

                temporaryPngPath = Path.Combine(
                    Path.GetTempPath(),
                    $"CraneOptimizer_SourcePdfPreview_{Guid.NewGuid():N}.png");

                RenderPdfPageToPng(table.PageNumber, temporaryPngPath);

                BitmapImage bitmap = LoadBitmapFromFile(temporaryPngPath);
                _sourcePreviewImagePixelWidth = bitmap.PixelWidth;
                _sourcePreviewImagePixelHeight = bitmap.PixelHeight;

                SourcePdfImage.Source = bitmap;
                _renderedSourcePreviewTable = table;
                ApplySourcePreviewZoom(scrollToTable: true);

                TxtSourcePreviewStatus.Text =
                    $"Original PDF page {table.PageNumber}; highlighted table bounds are from the selected detected candidate.";
            }
            catch (Exception ex)
            {
                ClearSourcePdfPreview("Could not render the source PDF page: " + ex.Message);
            }
            finally
            {
                if (!string.IsNullOrWhiteSpace(temporaryPngPath))
                {
                    try
                    {
                        if (File.Exists(temporaryPngPath))
                            File.Delete(temporaryPngPath);
                    }
                    catch
                    {
                        // Non-critical: the image has already been loaded into memory.
                    }
                }
            }
        }

        private (double Width, double Height) GetPdfPageSize(int pageNumber)
        {
            using var document = PdfDocument.Open(_scanResult.PdfPath);
            var page = document.GetPage(pageNumber);
            return (page.Width, page.Height);
        }

        private void RenderPdfPageToPng(int pageNumber, string outputPath)
        {
            using var pdfStream = File.OpenRead(_scanResult.PdfPath);
            int zeroBasedPageNumber = Math.Max(0, pageNumber - 1);

            Conversion.SavePng(
                outputPath,
                pdfStream,
                zeroBasedPageNumber,
                options: new RenderOptions(Width: SourcePreviewRenderWidth));
        }

        private static BitmapImage LoadBitmapFromFile(string imagePath)
        {
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.UriSource = new Uri(imagePath, UriKind.Absolute);
            bitmap.EndInit();
            bitmap.Freeze();
            return bitmap;
        }

        private void ClearSourcePdfPreview(string message)
        {
            _currentSourcePreviewTable = null;
            _renderedSourcePreviewTable = null;
            _sourcePreviewPageWidth = 0;
            _sourcePreviewPageHeight = 0;
            _sourcePreviewImagePixelWidth = 0;
            _sourcePreviewImagePixelHeight = 0;
            _sourcePreviewZoom = 1.0;

            if (SourcePdfImage != null)
            {
                SourcePdfImage.Source = null;
                SourcePdfImage.Width = 0;
                SourcePdfImage.Height = 0;
            }

            if (SourcePdfCanvas != null)
            {
                SourcePdfCanvas.Width = 1;
                SourcePdfCanvas.Height = 1;
            }

            if (SourcePdfTableHighlight != null)
                SourcePdfTableHighlight.Visibility = Visibility.Collapsed;

            if (TxtSourcePreviewStatus != null)
                TxtSourcePreviewStatus.Text = message;
        }

        private void ApplySourcePreviewZoom(bool scrollToTable)
        {
            if (SourcePdfImage.Source == null || _sourcePreviewImagePixelWidth <= 0 || _sourcePreviewImagePixelHeight <= 0)
                return;

            _sourcePreviewZoom = Math.Max(SourcePreviewMinimumZoom, Math.Min(SourcePreviewMaximumZoom, _sourcePreviewZoom));

            double displayWidth = _sourcePreviewImagePixelWidth * _sourcePreviewZoom;
            double displayHeight = _sourcePreviewImagePixelHeight * _sourcePreviewZoom;

            SourcePdfImage.Width = displayWidth;
            SourcePdfImage.Height = displayHeight;
            SourcePdfCanvas.Width = displayWidth;
            SourcePdfCanvas.Height = displayHeight;

            UpdateSourceTableHighlight();

            if (scrollToTable)
            {
                SourcePdfScrollViewer.Dispatcher.BeginInvoke(
                    new Action(ScrollSourcePreviewToTable),
                    DispatcherPriority.Background);
            }
        }

        private void UpdateSourceTableHighlight()
        {
            if (_currentSourcePreviewTable == null
                || SourcePdfImage.Source == null
                || _sourcePreviewPageWidth <= 0
                || _sourcePreviewPageHeight <= 0
                || SourcePdfImage.Width <= 0
                || SourcePdfImage.Height <= 0)
            {
                SourcePdfTableHighlight.Visibility = Visibility.Collapsed;
                return;
            }

            var table = _currentSourcePreviewTable;
            double scaleX = SourcePdfImage.Width / _sourcePreviewPageWidth;
            double scaleY = SourcePdfImage.Height / _sourcePreviewPageHeight;

            double left = Math.Max(0, table.Left * scaleX);
            double top = Math.Max(0, (_sourcePreviewPageHeight - table.Top) * scaleY);
            double width = Math.Max(2, (table.Right - table.Left) * scaleX);
            double height = Math.Max(2, (table.Top - table.Bottom) * scaleY);

            SourcePdfTableHighlight.Width = width;
            SourcePdfTableHighlight.Height = height;
            Canvas.SetLeft(SourcePdfTableHighlight, left);
            Canvas.SetTop(SourcePdfTableHighlight, top);
            SourcePdfTableHighlight.Visibility = Visibility.Visible;
        }

        private void ScrollSourcePreviewToTable()
        {
            if (SourcePdfTableHighlight.Visibility != Visibility.Visible)
                return;

            double left = Canvas.GetLeft(SourcePdfTableHighlight);
            double top = Canvas.GetTop(SourcePdfTableHighlight);

            SourcePdfScrollViewer.ScrollToHorizontalOffset(Math.Max(0, left - SourcePdfScrollViewer.ViewportWidth * 0.25));
            SourcePdfScrollViewer.ScrollToVerticalOffset(Math.Max(0, top - SourcePdfScrollViewer.ViewportHeight * 0.25));
        }

        private void SourcePreviewFitPage_Click(object sender, RoutedEventArgs e)
        {
            if (!_isSourcePreviewVisible)
                return;

            if (SourcePdfImage.Source == null || _sourcePreviewImagePixelWidth <= 0 || _sourcePreviewImagePixelHeight <= 0)
                return;

            double viewportWidth = Math.Max(1, SourcePdfScrollViewer.ViewportWidth - 24);
            double viewportHeight = Math.Max(1, SourcePdfScrollViewer.ViewportHeight - 24);
            double widthZoom = viewportWidth / _sourcePreviewImagePixelWidth;
            double heightZoom = viewportHeight / _sourcePreviewImagePixelHeight;

            _sourcePreviewZoom = Math.Min(widthZoom, heightZoom);
            ApplySourcePreviewZoom(scrollToTable: false);
            SourcePdfScrollViewer.ScrollToHorizontalOffset(0);
            SourcePdfScrollViewer.ScrollToVerticalOffset(0);
        }

        private void SourcePreviewFindTable_Click(object sender, RoutedEventArgs e)
        {
            if (!_isSourcePreviewVisible)
                return;

            ScrollSourcePreviewToTable();
        }

        private void SourcePreviewZoomOut_Click(object sender, RoutedEventArgs e)
        {
            if (!_isSourcePreviewVisible)
                return;

            _sourcePreviewZoom /= 1.20;
            ApplySourcePreviewZoom(scrollToTable: false);
        }

        private void SourcePreviewZoomIn_Click(object sender, RoutedEventArgs e)
        {
            if (!_isSourcePreviewVisible)
                return;

            _sourcePreviewZoom *= 1.20;
            ApplySourcePreviewZoom(scrollToTable: false);
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

            if (RequiresManualOverrideImportReview(table) && !ConfirmManualOverrideImport(table))
                return;

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

        private bool ConfirmManualOverrideImport(PdfTableCandidateDto table)
        {
            var recommended = GetRecommendedTable();
            string recommendedText = recommended == null
                ? "No adjudicated recommendation is currently available."
                : $"Recommended candidate: Page {recommended.PageNumber}, Table {recommended.TableIndex} ({recommended.CandidateKindLabel}).";

            string selectedKind = IsEnvelopeReferenceCandidate(table)
                ? "manual override envelope/reference fragment"
                : "manual override candidate";

            string lineRowText = table.LineRequirementRowCount > 0
                ? $"{table.LineRequirementRowCount} line-requirement row(s)"
                : "no line-requirement row detected";

            string message =
                "You are importing a manual override candidate." +
                Environment.NewLine + Environment.NewLine +
                "This table is not the adjudicated recommended import candidate. Manual override imports can intentionally save partial, envelope, reference, or secondary charts, but they can also omit manufacturer capacity data that is present in the recommended combined chart." +
                Environment.NewLine + Environment.NewLine +
                recommendedText +
                Environment.NewLine + Environment.NewLine +
                "Selected candidate:" +
                Environment.NewLine +
                $"- Page {table.PageNumber}, Table {table.TableIndex}: {selectedKind}" +
                Environment.NewLine +
                $"- {table.BoomColumnCount} visual boom column(s)" +
                Environment.NewLine +
                $"- {table.RadiusRowCount} capacity radius row(s), {lineRowText}" +
                Environment.NewLine +
                $"- {table.NonEmptyCapacityCellCount} numeric rated-capacity cell(s)" +
                Environment.NewLine + Environment.NewLine +
                "Continue importing this non-recommended candidate?";

            return MessageBox.Show(
                this,
                message,
                "Confirm Manual Override Load Chart Import",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning,
                MessageBoxResult.No) == MessageBoxResult.Yes;
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
                "This recommended table has repeated visual boom columns." +
                Environment.NewLine + Environment.NewLine +
                "The import will preserve and save the exact PDF visual chart, including duplicate visual columns such as 39.1, 39.1 (2), and 39.1 (3)." +
                Environment.NewLine + Environment.NewLine +
                "For optimization, duplicate same-radius/same-boom columns will use the conservative minimum capacity, not an optimistic maximum envelope." +
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
                MessageBoxResult.No) == MessageBoxResult.Yes;
        }

        private bool RequiresManualOverrideImportReview(PdfTableCandidateDto table)
        {
            return !SameTable(table, GetRecommendedTable());
        }

        private bool RequiresEnvelopeImportReview(PdfTableCandidateDto table)
        {
            return SameTable(table, GetRecommendedTable())
                && !table.IsCombinedContinuationCandidate
                && (IsEnvelopeReferenceCandidate(table) || table.Warnings.Any(IsEnvelopeWarning));
        }

        private static bool IsEnvelopeWarning(string warning)
        {
            if (string.IsNullOrWhiteSpace(warning))
                return false;

            return warning.Contains("ENVELOPE_IMPORT_REVIEW_REQUIRED", StringComparison.OrdinalIgnoreCase)
                || warning.Contains("maximum-capacity envelope", StringComparison.OrdinalIgnoreCase)
                || warning.Contains("consolidated into unique boom columns", StringComparison.OrdinalIgnoreCase)
                || warning.Contains("Full manufacturer visual columns detected", StringComparison.OrdinalIgnoreCase);
        }        private void Cancel_Click(object sender, RoutedEventArgs e)
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
