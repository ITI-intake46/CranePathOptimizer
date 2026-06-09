using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.DocumentLayoutAnalysis.WordExtractor;

namespace CraneOptimizer.UI.Features.F4_LoadChart.Services
{
    internal sealed class PdfLoadChartScanResult
    {
        public string PdfPath { get; init; } = string.Empty;
        public int PageCount { get; init; }
        public List<PdfPageCandidateDto> Pages { get; init; } = new();
        public List<string> Warnings { get; init; } = new();

        public IReadOnlyList<PdfTableCandidateDto> AllTables =>
            Pages.SelectMany(page => page.Tables).OrderByDescending(table => table.Score).ToList();

        public PdfTableCandidateDto? GetBestTable()
        {
            return Pages
                .SelectMany(page => page.Tables)
                .OrderByDescending(table => table.Score)
                .FirstOrDefault();
        }
    }

    internal sealed class PdfPageCandidateDto
    {
        public int PageNumber { get; init; }
        public int WordCount { get; init; }
        public int NumericTokenCount { get; init; }
        public double Score { get; init; }
        public string ConfidenceLabel { get; init; } = "Low";
        public string Summary { get; init; } = string.Empty;
        public string TextPreview { get; init; } = string.Empty;
        public List<PdfTableCandidateDto> Tables { get; init; } = new();

        public string DisplayTitle =>
            $"Page {PageNumber} - {ConfidenceLabel} - {Tables.Count} table(s)";

        public string DisplayDetails =>
            $"{Summary} | Words: {WordCount}, Numbers: {NumericTokenCount}, Score: {Score:0.0}";
    }

    internal sealed class PdfTableCandidateDto
    {
        public int PageNumber { get; init; }
        public int TableIndex { get; init; }
        public double Left { get; init; }
        public double Right { get; init; }
        public double Top { get; init; }
        public double Bottom { get; init; }
        public int BoomColumnCount { get; init; }
        public int RadiusRowCount { get; init; }
        public int NonEmptyCapacityCellCount { get; init; }
        public double Score { get; init; }
        public string ConfidenceLabel { get; init; } = "Low";
        public string Summary { get; init; } = string.Empty;
        public List<List<string>> RawGrid { get; init; } = new();
        public List<List<string>> ImportGrid { get; init; } = new();
        public bool HasSeparateImportGrid => ImportGrid.Count > 0;
        public List<string> Warnings { get; init; } = new();

        public bool IsEnvelopeCandidate =>
            Warnings.Any(warning =>
                warning.Contains("ENVELOPE_IMPORT_REVIEW_REQUIRED", StringComparison.OrdinalIgnoreCase) ||
                warning.Contains("maximum-capacity envelope", StringComparison.OrdinalIgnoreCase) ||
                warning.Contains("consolidated into unique boom columns", StringComparison.OrdinalIgnoreCase));

        public string CandidateKindLabel =>
            IsEnvelopeCandidate ? "Envelope review required" : ConfidenceLabel;

        public string DisplayTitle =>
            IsEnvelopeCandidate
                ? $"Table {TableIndex} - Envelope review - {BoomColumnCount} x {RadiusRowCount}"
                : $"Table {TableIndex} - {ConfidenceLabel} - {BoomColumnCount} x {RadiusRowCount}";

        public string DisplayDetails =>
            IsEnvelopeCandidate
                ? $"{Summary} | ENVELOPE candidate: duplicate visual columns were consolidated. Cells: {NonEmptyCapacityCellCount}, Score: {Score:0.0}"
                : $"{Summary} | Cells: {NonEmptyCapacityCellCount}, Score: {Score:0.0}";
    }

    internal static class LoadChartPdfService
    {
        private const int MinimumBoomColumnCount = 3;
        private const int MinimumRadiusRowCount = 2;

        public static LoadChartImportResult ImportPdf(string pdfFilePath)
        {
            var scanResult = ScanPdf(pdfFilePath);
            var bestTable = scanResult.GetBestTable()
                ?? throw new FormatException("No load-chart table candidates were detected in the selected PDF.");

            return ImportSelectedTable(scanResult, bestTable);
        }

        public static LoadChartImportResult ImportSelectedTable(
            PdfLoadChartScanResult scanResult,
            PdfTableCandidateDto selectedTable)
        {
            if (scanResult == null)
                throw new ArgumentNullException(nameof(scanResult));

            if (selectedTable == null)
                throw new ArgumentNullException(nameof(selectedTable));

            bool hasSeparateImportGrid = selectedTable.ImportGrid.Count > 0;
            string sourceDescription = $"PDF page {selectedTable.PageNumber}, table {selectedTable.TableIndex}";

            var envelopeChart = hasSeparateImportGrid
                ? LoadChartCsvService.ParseRawGrid(selectedTable.ImportGrid)
                : null;

            var chart = hasSeparateImportGrid
                ? LoadChartCsvService.ParseExactVisualRawGrid(selectedTable.RawGrid, sourceDescription)
                : LoadChartCsvService.ParseRawGrid(selectedTable.RawGrid);

            if (chart.Points.Count == 0 || chart.MaxCapacity <= 0)
                throw new FormatException("The selected PDF table did not produce a valid crane load chart.");

            int numberOfLinesColumns = chart.Points
                .Where(point => point.NumberOfLines.HasValue)
                .Select(point => point.VisualColumnIndex ?? -1)
                .Distinct()
                .Count(index => index > 0);

            var warnings = new List<string>
            {
                $"PDF import used page {selectedTable.PageNumber}, table {selectedTable.TableIndex}."
            };

            if (hasSeparateImportGrid && envelopeChart != null)
            {
                warnings.Add($"Imported exact PDF visual load chart: {chart.Points.Count} visual capacity cell(s) from {selectedTable.NonEmptyCapacityCellCount} non-empty PDF capacity cell(s).");
                warnings.Add($"The exact chart preserves {selectedTable.BoomColumnCount} visual boom column(s), including repeated manufacturer headers, across {selectedTable.RadiusRowCount} radius row(s).");
                warnings.Add($"For optimization capacity lookups, duplicate visual columns with the same radius and numeric boom length use the conservative minimum capacity policy. Reference envelope has {envelopeChart.Points.Count} point(s).");
            }
            else
            {
                warnings.Add($"Imported {chart.Points.Count} real capacity point(s) from {selectedTable.NonEmptyCapacityCellCount} non-empty PDF capacity cell(s).");
                warnings.Add($"Detected {selectedTable.BoomColumnCount} boom-length column(s), {selectedTable.RadiusRowCount} radius row(s), and max capacity {chart.MaxCapacity:0.###} t.");
            }

            warnings.Add(LoadChartCsvService.GetCapacityUnitSummary(chart));

            if (numberOfLinesColumns > 0)
            {
                warnings.Add($"Detected and preserved Number of lines / parts-of-line metadata for {numberOfLinesColumns} visual boom column(s). This row is not treated as capacity data.");
            }

            warnings.AddRange(hasSeparateImportGrid
                ? NormalizeExactVisualImportWarnings(selectedTable.Warnings)
                : selectedTable.Warnings);
            warnings.AddRange(scanResult.Warnings);

            return new LoadChartImportResult
            {
                Chart = chart,
                ImportableEnvelopeChart = envelopeChart,
                SourceKind = "PDF",
                SourceDescription = sourceDescription,
                Warnings = warnings.Distinct().ToList(),
                ExactVisualPreviewGrid = hasSeparateImportGrid
                    ? selectedTable.RawGrid.Select(row => row.ToList()).ToList()
                    : new List<List<string>>(),
                ExactVisualPreviewTitle = hasSeparateImportGrid
                    ? "Exact PDF Visual Load Chart"
                    : string.Empty,
                ExactVisualPreviewDescription = hasSeparateImportGrid
                    ? $"Full PDF visual table: {selectedTable.BoomColumnCount} visual boom column(s) x {selectedTable.RadiusRowCount} radius row(s). Save Crane now saves this exact visual chart. Duplicate numeric boom columns are preserved with visual column metadata; optimization uses the conservative minimum capacity across duplicates." +
                      (numberOfLinesColumns > 0 ? $" Number of lines metadata is preserved for {numberOfLinesColumns} visual boom column(s)." : string.Empty)
                    : string.Empty
            };
        }

        private static IEnumerable<string> NormalizeExactVisualImportWarnings(IEnumerable<string> warnings)
        {
            foreach (string warning in warnings)
            {
                if (string.IsNullOrWhiteSpace(warning))
                    continue;

                if (warning.Contains("ENVELOPE_IMPORT_REVIEW_REQUIRED", StringComparison.OrdinalIgnoreCase)
                    || warning.Contains("maximum-capacity envelope", StringComparison.OrdinalIgnoreCase)
                    || warning.Contains("consolidated into unique boom columns", StringComparison.OrdinalIgnoreCase))
                {
                    yield return "Repeated visual boom columns were detected. The exact PDF visual chart is preserved and saved; optimization uses the conservative minimum capacity across duplicate same-radius/same-boom columns.";
                    continue;
                }

                yield return warning;
            }
        }

        public static PdfLoadChartScanResult ScanPdf(string pdfFilePath)
        {
            if (!File.Exists(pdfFilePath))
                throw new FileNotFoundException("PDF file not found.", pdfFilePath);

            var pages = new List<PdfPageCandidateDto>();
            var warnings = new List<string>();
            int pageCount = 0;
            int pagesWithSelectableWords = 0;

            using (var document = PdfDocument.Open(pdfFilePath))
            {
                foreach (Page page in document.GetPages())
                {
                    pageCount++;

                    var pageCandidate = ExtractPageCandidate(page, page.Number);
                    pages.Add(pageCandidate);

                    if (pageCandidate.WordCount > 0)
                        pagesWithSelectableWords++;
                }
            }

            if (pagesWithSelectableWords == 0)
            {
                throw new FormatException(
                    "No selectable PDF text was found. This importer currently supports text-based PDFs only, not scanned/image PDFs.");
            }

            if (pages.Sum(page => page.Tables.Count) == 0)
            {
                warnings.Add("Selectable PDF text was found, but no reliable 2D load-chart tables were detected. The current importer supports simple Radius + Boom Length -> Capacity charts.");
            }

            return new PdfLoadChartScanResult
            {
                PdfPath = pdfFilePath,
                PageCount = pageCount,
                Pages = pages
                    .OrderByDescending(page => page.Score)
                    .ThenBy(page => page.PageNumber)
                    .ToList(),
                Warnings = warnings
            };
        }

        private static PdfPageCandidateDto ExtractPageCandidate(Page page, int pageNumber)
        {
            var words = page
                .GetWords(NearestNeighbourWordExtractor.Instance)
                .Select(ToWordInfo)
                .Where(word => !string.IsNullOrWhiteSpace(word.Text))
                .ToList();

            int numericTokenCount = words.Count(word => TryExtractNumber(word.Text, out _));
            string textPreview = string.Join(" ", words.Take(80).Select(word => word.Text));

            if (words.Count == 0)
            {
                return new PdfPageCandidateDto
                {
                    PageNumber = pageNumber,
                    WordCount = 0,
                    NumericTokenCount = 0,
                    Score = 0,
                    ConfidenceLabel = "No text",
                    Summary = "No selectable text found on this page.",
                    TextPreview = string.Empty
                };
            }

            var rows = ClusterRows(words);
            var tableCandidates = ExtractTableCandidates(rows, pageNumber, words);

            double keywordScore = ScoreKeywords(words);
            double score = tableCandidates.Count == 0
                ? numericTokenCount * 0.1 + keywordScore
                : tableCandidates.Max(table => table.Score) + keywordScore;

            string confidence = tableCandidates.Count == 0
                ? "No table"
                : GetConfidenceLabel(score);

            string summary = tableCandidates.Count == 0
                ? "No reliable 2D load-chart table candidate detected."
                : $"{tableCandidates.Count} table candidate(s). Best: {tableCandidates.Max(t => t.BoomColumnCount)} boom columns, {tableCandidates.Max(t => t.RadiusRowCount)} radius rows.";

            return new PdfPageCandidateDto
            {
                PageNumber = pageNumber,
                WordCount = words.Count,
                NumericTokenCount = numericTokenCount,
                Score = score,
                ConfidenceLabel = confidence,
                Summary = summary,
                TextPreview = textPreview,
                Tables = tableCandidates
                    .OrderByDescending(table => table.Score)
                    .ThenBy(table => table.TableIndex)
                    .ToList()
            };
        }

                private static List<PdfTableCandidateDto> ExtractTableCandidates(
            List<List<PdfWordInfo>> rows,
            int pageNumber,
            List<PdfWordInfo> allWords)
        {
            var candidates = new List<PdfTableCandidateDto>();

            foreach (var header in FindHeaderCandidates(rows).Take(24))
            {
                var streamSegments = BuildRowStreamSegments(rows, header);
                if (streamSegments.Count == 0)
                    continue;

                for (int segmentIndex = 0; segmentIndex < streamSegments.Count; segmentIndex++)
                {
                    var segmentRows = streamSegments[segmentIndex];
                    var candidate = TryBuildRowStreamCandidate(
                        rows,
                        header,
                        pageNumber,
                        candidates.Count + 1,
                        allWords,
                        segmentRows,
                        includeHeaderInBounds: segmentIndex == 0);

                    if (candidate == null)
                        continue;

                    if (IsLikelyMetadataCandidate(candidate))
                        continue;

                    if (IsLikelyBodyFragmentCandidate(candidate))
                        continue;

                    if (IsDuplicateCandidate(candidates, candidate))
                        continue;

                    candidates.Add(candidate);
                }
            }

            if (!candidates.Any(IsStrongTableCandidate))
            {
                foreach (var fallbackCandidate in BuildUniversalGeometryFallbackCandidates(rows, pageNumber, allWords, candidates.Count + 1))
                {
                    if (IsLikelyMetadataCandidate(fallbackCandidate))
                        continue;

                    if (IsLikelyBodyFragmentCandidate(fallbackCandidate))
                        continue;

                    if (IsDuplicateCandidate(candidates, fallbackCandidate))
                        continue;

                    candidates.Add(fallbackCandidate);
                }
            }

            candidates = candidates
                .OrderByDescending(candidate => candidate.Score)
                .ThenByDescending(candidate => candidate.Top)
                .ToList();

            for (int index = 0; index < candidates.Count; index++)
            {
                candidates[index] = ReindexCandidate(candidates[index], index + 1);
            }

            return candidates;
        }
        private static List<HeaderCandidate> FindHeaderCandidates(List<List<PdfWordInfo>> rows)
        {
            var candidates = new List<HeaderCandidate>();

            for (int rowIndex = 0; rowIndex < rows.Count; rowIndex++)
            {
                var numbers = GetNumericTokens(rows[rowIndex])
                    .Where(token => token.Value > 0 && token.Value < 500)
                    .OrderBy(token => token.Word.CenterX)
                    .ToList();

                numbers = RemoveNearDuplicateNumbers(numbers);

                if (numbers.Count < MinimumBoomColumnCount)
                    continue;

                foreach (var segment in SplitHeaderNumbersForIndependentTableRegions(numbers))
                {
                    if (segment.Count < MinimumBoomColumnCount)
                        continue;

                    double increasingRatio = GetIncreasingRatio(segment.Select(token => token.Value).ToList());
                    double xSpan = segment.Last().Word.CenterX - segment.First().Word.CenterX;
                    double spacingRegularity = GetSpacingRegularity(segment.Select(token => token.Word.CenterX).ToList());

                    if (increasingRatio < 0.50 || xSpan < 60)
                        continue;

                    double score =
                        segment.Count * 4 +
                        increasingRatio * 48 +
                        Math.Min(xSpan / 20d, 34) +
                        spacingRegularity * 18;

                    if (segment.Count < numbers.Count)
                    {
                        score += 22;
                    }

                    candidates.Add(new HeaderCandidate(rowIndex, rows[rowIndex], segment, score));
                }
            }

            return candidates
                .OrderByDescending(candidate => candidate.Score)
                .ThenBy(candidate => candidate.RowIndex)
                .ToList();
        }

        private static List<List<NumericToken>> SplitHeaderNumbersForIndependentTableRegions(List<NumericToken> orderedNumbers)
        {
            if (orderedNumbers.Count < MinimumBoomColumnCount)
                return new List<List<NumericToken>> { orderedNumbers };

            double medianGap = GetMedianPositiveGap(orderedNumbers.Select(token => token.Word.CenterX).ToList());
            if (medianGap <= 0)
                return new List<List<NumericToken>> { orderedNumbers };

            double largeGapThreshold = Math.Max(medianGap * 2.40, medianGap + 80.0);
            var splitStarts = new List<int>();

            for (int index = 0; index < orderedNumbers.Count - 1; index++)
            {
                int leftCount = index + 1;
                int rightCount = orderedNumbers.Count - leftCount;

                if (leftCount < MinimumBoomColumnCount || rightCount < 2)
                    continue;

                double gap = orderedNumbers[index + 1].Word.CenterX - orderedNumbers[index].Word.CenterX;
                if (gap < largeGapThreshold)
                    continue;

                bool numericReset = orderedNumbers[index + 1].Value < orderedNumbers[index].Value - 0.001;
                bool majorPhysicalSeparation = gap >= Math.Max(120.0, medianGap * 3.0);

                if (numericReset || majorPhysicalSeparation)
                    splitStarts.Add(index + 1);
            }

            if (splitStarts.Count == 0)
                return new List<List<NumericToken>> { orderedNumbers };

            var segments = new List<List<NumericToken>>();
            int start = 0;

            foreach (int splitStart in splitStarts.Distinct().OrderBy(value => value))
            {
                if (splitStart > start)
                    segments.Add(orderedNumbers.Skip(start).Take(splitStart - start).ToList());

                start = splitStart;
            }

            if (start < orderedNumbers.Count)
                segments.Add(orderedNumbers.Skip(start).ToList());

            var usableSegments = segments
                .Where(segment => segment.Count >= MinimumBoomColumnCount)
                .ToList();

            return usableSegments.Count == 0
                ? new List<List<NumericToken>> { orderedNumbers }
                : usableSegments;
        }

        private static List<List<double>> SplitHeaderValuesForTesting(
            IReadOnlyList<double> values,
            IReadOnlyList<double> centerXs)
        {
            if (values.Count != centerXs.Count)
                throw new ArgumentException("Header values and X coordinates must have the same count.");

            var syntheticTokens = new List<NumericToken>();

            for (int index = 0; index < values.Count; index++)
            {
                syntheticTokens.Add(new NumericToken(
                    new PdfWordInfo
                    {
                        Text = FormatNumber(values[index]),
                        Left = centerXs[index] - 1,
                        Right = centerXs[index] + 1,
                        Bottom = 0,
                        Top = 1
                    },
                    values[index]));
            }

            return SplitHeaderNumbersForIndependentTableRegions(syntheticTokens)
                .Select(segment => segment.Select(token => token.Value).ToList())
                .ToList();
        }

        private static double GetOpenEndedBodyRightLimit(double lastHeaderX, double medianHeaderGap)
        {
            double safeGap = medianHeaderGap > 0 ? medianHeaderGap : 40.0;
            return lastHeaderX + Math.Max(18.0, safeGap * 1.35);
        }

        private static double GetOpenEndedBodyRightLimitForTesting(double lastHeaderX, double medianHeaderGap)
        {
            return GetOpenEndedBodyRightLimit(lastHeaderX, medianHeaderGap);
        }
        private static int ResolveSequentialOrGeometryColumnForBodyValue(
            IReadOnlyList<NumericToken> orderedHeaderTokens,
            int columnCount,
            int sequentialColumn,
            double bodyCenterX)
        {
            if (orderedHeaderTokens.Count == 0 || columnCount <= 0)
                return sequentialColumn;

            var headerCenters = orderedHeaderTokens
                .Take(columnCount)
                .Select(token => token.Word.CenterX)
                .ToList();

            return ResolveSequentialOrGeometryColumnForTesting(
                headerCenters,
                sequentialColumn,
                bodyCenterX);
        }

        private static int ResolveSequentialOrGeometryColumnForTesting(
            IReadOnlyList<double> headerCenters,
            int sequentialColumn,
            double bodyCenterX)
        {
            if (headerCenters == null || headerCenters.Count == 0)
                return sequentialColumn;

            int normalizedSequentialColumn = Math.Max(0, Math.Min(sequentialColumn, headerCenters.Count - 1));
            int geometryColumn = FindNearestLaneIndex(headerCenters.ToList(), bodyCenterX);

            if (geometryColumn < 0)
                return normalizedSequentialColumn;

            if (!IsWithinHeaderColumnAssignmentRegion(headerCenters, geometryColumn, bodyCenterX))
                return normalizedSequentialColumn;

            return geometryColumn;
        }

        private static bool IsWithinHeaderColumnAssignmentRegion(
            IReadOnlyList<double> headerCenters,
            int columnIndex,
            double bodyCenterX)
        {
            if (headerCenters.Count == 0 || columnIndex < 0 || columnIndex >= headerCenters.Count)
                return false;

            var orderedCenters = headerCenters.OrderBy(value => value).ToList();
            double medianGap = GetMedianPositiveGap(orderedCenters);
            double edgeMargin = Math.Max(10.0, medianGap > 0 ? medianGap * 0.75 : 18.0);

            double leftBoundary = columnIndex == 0
                ? orderedCenters[columnIndex] - edgeMargin
                : (orderedCenters[columnIndex - 1] + orderedCenters[columnIndex]) / 2.0;

            double rightBoundary = columnIndex == orderedCenters.Count - 1
                ? orderedCenters[columnIndex] + edgeMargin
                : (orderedCenters[columnIndex] + orderedCenters[columnIndex + 1]) / 2.0;

            return bodyCenterX >= leftBoundary - 0.001
                && bodyCenterX <= rightBoundary + 0.001;
        }
        private static PdfTableCandidateDto? TryBuildRowStreamCandidate(
            List<List<PdfWordInfo>> rows,
            HeaderCandidate header,
            int pageNumber,
            int tableIndex,
            List<PdfWordInfo> allWords,
            List<RowStreamData>? streamRowsOverride = null,
            bool includeHeaderInBounds = true)
        {
            var streamRows = streamRowsOverride ?? BuildRowStreamRows(rows, header);
            if (streamRows.Count < MinimumRadiusRowCount)
                return null;

            var candidateGrid = BuildBestGrid(header, streamRows, allWords);
            if (candidateGrid == null)
                return null;

            if (candidateGrid.RadiusRowCount < MinimumRadiusRowCount)
                return null;

            if (candidateGrid.Density < 0.08)
                return null;

            bool usesPhysicalLaneReconstruction = HasDuplicateNumericValues(candidateGrid.HeaderValues);
            var columnCenters = ResolveCandidateColumnCenters(header, candidateGrid, usesPhysicalLaneReconstruction);
            var rawGridWithNumberOfLines = AppendNumberOfLinesRowIfPresent(candidateGrid.RawGrid, rows, header, columnCenters);
            int numberOfLinesMetadataColumns = CountNumberOfLinesMetadataCells(rawGridWithNumberOfLines);
            var importGrid = usesPhysicalLaneReconstruction
                ? BuildConservativeImportGrid(rawGridWithNumberOfLines)
                : new List<List<string>>();

            var boundsWords = includeHeaderInBounds
                ? header.RowWords
                    .Concat(candidateGrid.UsedRows.SelectMany(row => row.OriginalRow))
                    .ToList()
                : candidateGrid.UsedRows
                    .SelectMany(row => row.OriginalRow)
                    .ToList();

            if (boundsWords.Count == 0)
                boundsWords = header.RowWords.ToList();

            var warnings = new List<string>
            {
                "Row-stream extractor parsed visual rows as radius plus ordered capacity values."
            };

            if (usesPhysicalLaneReconstruction)
            {
                warnings.Add("PHYSICAL_LANE_RECONSTRUCTION: Capacity columns were reconstructed from PDF body-cell X positions because at least one boom header spans multiple physical lanes.");
                warnings.Add("The exact PDF visual table is preserved with repeated boom headers; optimization uses the conservative minimum capacity across duplicate same-radius/same-boom columns.");
            }
            else
            {
                warnings.Add("X positions were used only to estimate the first capacity column. Review the preview before saving.");
            }

            if (candidateGrid.SkippedBodyValues > 0)
                warnings.Add($"{candidateGrid.SkippedBodyValues} body value(s) were outside the supported boom-column range and were skipped.");

            if (candidateGrid.EmptyTopLeftPenalty)
                warnings.Add("The upper-left chart region is sparse. This can indicate a wrong header or unsupported table layout.");

            if (candidateGrid.RadiusIncreasingRatio < 0.90)
                warnings.Add("Radius values are not strictly increasing. Review this table carefully.");

            if (candidateGrid.CapacityTrendRatio < 0.45)
                warnings.Add("Capacity values do not show a strong decreasing trend. This may be normal for special charts, but review carefully.");

            if (candidateGrid.ColumnCount < 4)
                warnings.Add("This candidate has very few boom columns and may not be a normal load chart.");

            if (numberOfLinesMetadataColumns > 0)
                warnings.Add($"Detected Number of lines / parts-of-line metadata for {numberOfLinesMetadataColumns} visual boom column(s); this metadata row is preserved but excluded from capacity parsing.");

            if (!HasLoadChartKeywords(allWords))
                warnings.Add("Nearby load-chart keywords were not detected. This can be normal in multilingual manufacturer PDFs; verify the parsed grid before saving.");

            string summary =
                usesPhysicalLaneReconstruction
                    ? $"{candidateGrid.ColumnCount} visual boom columns, {candidateGrid.RadiusRowCount} radius rows, density {candidateGrid.Density:P0}, top-row density {candidateGrid.TopRowsDensity:P0}. Physical lane reconstruction detected spanning header(s)."
                    : $"{candidateGrid.ColumnCount} boom columns, {candidateGrid.RadiusRowCount} radius rows, density {candidateGrid.Density:P0}, top-row density {candidateGrid.TopRowsDensity:P0}.";

            return new PdfTableCandidateDto
            {
                PageNumber = pageNumber,
                TableIndex = tableIndex,
                Left = boundsWords.Count == 0 ? 0 : boundsWords.Min(word => word.Left),
                Right = boundsWords.Count == 0 ? 0 : boundsWords.Max(word => word.Right),
                Top = boundsWords.Count == 0 ? 0 : boundsWords.Max(word => word.Top),
                Bottom = boundsWords.Count == 0 ? 0 : boundsWords.Min(word => word.Bottom),
                BoomColumnCount = candidateGrid.ColumnCount,
                RadiusRowCount = candidateGrid.RadiusRowCount,
                NonEmptyCapacityCellCount = candidateGrid.NonEmptyCellCount,
                Score = candidateGrid.Score,
                ConfidenceLabel = GetConfidenceLabel(candidateGrid.Score),
                Summary = summary,
                RawGrid = rawGridWithNumberOfLines,
                ImportGrid = importGrid,
                Warnings = warnings.Distinct().ToList()
            };
        }

        private static List<List<RowStreamData>> BuildRowStreamSegments(
            List<List<PdfWordInfo>> rows,
            HeaderCandidate header)
        {
            var segments = new List<List<RowStreamData>>();
            var currentSegment = new List<RowStreamData>();

            double firstHeaderX = header.Numbers.First().Word.CenterX;
            double lastHeaderX = header.Numbers.Last().Word.CenterX;
            double medianHeaderGap = GetMedianGap(header.Numbers.Select(token => token.Word.CenterX).ToList());
            if (medianHeaderGap <= 0)
                medianHeaderGap = 40;

            double? previousRadius = null;
            int skippedRowsAfterStart = 0;

            for (int rowIndex = header.RowIndex + 1; rowIndex < rows.Count; rowIndex++)
            {
                var row = rows[rowIndex];

                var numbers = GetNumericTokens(row)
                    .OrderBy(token => token.Word.CenterX)
                    .ToList();

                if (numbers.Count < 2)
                {
                    CloseCurrentSegmentAfterGap();
                    continue;
                }

                NumericToken? radiusToken = FindLeftRadius(numbers, firstHeaderX, medianHeaderGap);
                if (radiusToken == null)
                {
                    CloseCurrentSegmentAfterGap();
                    continue;
                }

                double radius = radiusToken.Value.Value;
                if (radius <= 0 || radius > 500)
                {
                    CloseCurrentSegmentAfterGap();
                    continue;
                }

                if (previousRadius.HasValue && radius < previousRadius.Value - 0.001)
                {
                    AddCurrentSegmentIfUseful();
                    currentSegment = new List<RowStreamData>();
                    previousRadius = null;
                    skippedRowsAfterStart = 0;
                }

                NumericToken? rightDuplicateRadius = FindRightDuplicateRadius(numbers, radiusToken.Value, lastHeaderX);

                double openEndedBodyRightLimit = GetOpenEndedBodyRightLimit(lastHeaderX, medianHeaderGap);

                var bodyNumbers = numbers
                    .Where(token => !ReferenceEquals(token.Word, radiusToken.Value.Word))
                    .Where(token => rightDuplicateRadius == null || !ReferenceEquals(token.Word, rightDuplicateRadius.Value.Word))
                    .Where(token => token.Word.CenterX > radiusToken.Value.Word.CenterX + 2)
                    .Where(token => rightDuplicateRadius == null || token.Word.CenterX < rightDuplicateRadius.Value.Word.CenterX - 2)
                    .Where(token => rightDuplicateRadius != null || token.Word.CenterX <= openEndedBodyRightLimit)
                    .OrderBy(token => token.Word.CenterX)
                    .ToList();

                if (bodyNumbers.Count == 0)
                {
                    CloseCurrentSegmentAfterGap();
                    continue;
                }

                int startColumn = EstimateStartColumn(header.Numbers, bodyNumbers.First().Word.CenterX);

                currentSegment.Add(new RowStreamData(radius, radiusToken.Value, rightDuplicateRadius, bodyNumbers, startColumn, row));
                previousRadius = radius;
                skippedRowsAfterStart = 0;
            }

            AddCurrentSegmentIfUseful();
            return segments;

            void CloseCurrentSegmentAfterGap()
            {
                if (currentSegment.Count == 0)
                    return;

                skippedRowsAfterStart++;
                if (skippedRowsAfterStart < 8)
                    return;

                AddCurrentSegmentIfUseful();
                currentSegment = new List<RowStreamData>();
                previousRadius = null;
                skippedRowsAfterStart = 0;
            }

            void AddCurrentSegmentIfUseful()
            {
                if (currentSegment.Count < MinimumRadiusRowCount)
                    return;

                segments.Add(currentSegment);
            }
        }
        private static List<RowStreamData> BuildRowStreamRows(
            List<List<PdfWordInfo>> rows,
            HeaderCandidate header)
        {
            var result = new List<RowStreamData>();

            double firstHeaderX = header.Numbers.First().Word.CenterX;
            double lastHeaderX = header.Numbers.Last().Word.CenterX;
            double medianHeaderGap = GetMedianGap(header.Numbers.Select(token => token.Word.CenterX).ToList());
            if (medianHeaderGap <= 0)
                medianHeaderGap = 40;

            double? previousRadius = null;
            int skippedRowsAfterStart = 0;

            for (int rowIndex = header.RowIndex + 1; rowIndex < rows.Count; rowIndex++)
            {
                var row = rows[rowIndex];

                var numbers = GetNumericTokens(row)
                    .OrderBy(token => token.Word.CenterX)
                    .ToList();

                if (numbers.Count < 2)
                {
                    if (result.Count > 0 && ++skippedRowsAfterStart >= 8)
                        break;

                    continue;
                }

                NumericToken? radiusToken = FindLeftRadius(numbers, firstHeaderX, medianHeaderGap);
                if (radiusToken == null)
                    continue;

                double radius = radiusToken.Value.Value;
                if (radius <= 0 || radius > 500)
                    continue;

                if (previousRadius.HasValue && radius < previousRadius.Value - 0.001)
                {
                    if (result.Count >= MinimumRadiusRowCount)
                        break;

                    continue;
                }

                NumericToken? rightDuplicateRadius = FindRightDuplicateRadius(numbers, radiusToken.Value, lastHeaderX);

                double openEndedBodyRightLimit = GetOpenEndedBodyRightLimit(lastHeaderX, medianHeaderGap);

                var bodyNumbers = numbers
                    .Where(token => !ReferenceEquals(token.Word, radiusToken.Value.Word))
                    .Where(token => rightDuplicateRadius == null || !ReferenceEquals(token.Word, rightDuplicateRadius.Value.Word))
                    .Where(token => token.Word.CenterX > radiusToken.Value.Word.CenterX + 2)
                    .Where(token => rightDuplicateRadius == null || token.Word.CenterX < rightDuplicateRadius.Value.Word.CenterX - 2)
                    .Where(token => rightDuplicateRadius != null || token.Word.CenterX <= openEndedBodyRightLimit)
                    .OrderBy(token => token.Word.CenterX)
                    .ToList();

                if (bodyNumbers.Count == 0)
                {
                    if (result.Count > 0 && ++skippedRowsAfterStart >= 8)
                        break;

                    continue;
                }

                int startColumn = EstimateStartColumn(header.Numbers, bodyNumbers.First().Word.CenterX);

                result.Add(new RowStreamData(radius, radiusToken.Value, rightDuplicateRadius, bodyNumbers, startColumn, row));
                previousRadius = radius;
                skippedRowsAfterStart = 0;
            }

            return result;
        }

        private static bool HasDuplicateNumericValues(IReadOnlyList<double> values)
        {
            for (int i = 0; i < values.Count; i++)
            {
                for (int j = i + 1; j < values.Count; j++)
                {
                    if (Math.Abs(values[i] - values[j]) < 0.001)
                        return true;
                }
            }

            return false;
        }
        private static CandidateGrid? BuildBestGrid(
            HeaderCandidate header,
            List<RowStreamData> streamRows,
            List<PdfWordInfo> allWords)
        {
            var orderedHeaderTokens = header.Numbers
                .OrderBy(token => token.Word.CenterX)
                .ToList();

            var fullHeaderValues = orderedHeaderTokens
                .Select(token => token.Value)
                .ToList();

            // Explicit repeated numeric header tokens usually indicate an extra chart
            // dimension such as mode, counterweight, jib, or line count. The row-stream
            // path below is for single-header rows, including headers that visually span
            // several physical body lanes.
            if (HasDuplicateNumericValues(fullHeaderValues))
                return null;

            int headerCount = orderedHeaderTokens.Count;
            var gridOptions = new List<CandidateGrid>();

            var physicalLaneCenters = InferPhysicalCapacityLaneCenters(streamRows, orderedHeaderTokens);
            var physicalHeaderValues = MapHeaderValuesToPhysicalLanes(orderedHeaderTokens, physicalLaneCenters);

            if (physicalHeaderValues.Count >= MinimumBoomColumnCount
                && physicalLaneCenters.Count == physicalHeaderValues.Count
                && physicalHeaderValues.Count > headerCount
                && HasDuplicateNumericValues(physicalHeaderValues))
            {
                var physicalGrid = BuildGrid(
                    header,
                    streamRows,
                    physicalHeaderValues,
                    allWords,
                    physicalLaneCenters);

                if (physicalGrid != null)
                    gridOptions.Add(physicalGrid);
            }

            var columnCountOptions = new HashSet<int> { headerCount };

            if (headerCount > 4)
            {
                columnCountOptions.Add(headerCount - 1);
                columnCountOptions.Add(headerCount - 2);
            }

            foreach (int columnCount in columnCountOptions.Where(count => count >= MinimumBoomColumnCount))
            {
                var headerValues = fullHeaderValues.Take(columnCount).ToList();
                var grid = BuildGrid(
                    header,
                    streamRows,
                    headerValues,
                    allWords,
                    physicalLaneCenters: null);

                if (grid != null)
                    gridOptions.Add(grid);
            }

            return gridOptions
                .OrderByDescending(grid => grid.Score)
                .FirstOrDefault();
        }

        private static CandidateGrid? BuildGrid(
            HeaderCandidate header,
            List<RowStreamData> streamRows,
            List<double> headerValues,
            List<PdfWordInfo> allWords,
            List<double>? physicalLaneCenters)
        {
            int columnCount = headerValues.Count;

            if (headerValues.Count < MinimumBoomColumnCount)
                return null;

            var headerTokensForColumnAssignment = header.Numbers
                .OrderBy(token => token.Word.CenterX)
                .Take(columnCount)
                .ToList();

            bool usesPhysicalLaneReconstruction = physicalLaneCenters != null
                && physicalLaneCenters.Count == headerValues.Count;

            if (!usesPhysicalLaneReconstruction && HasDuplicateNumericValues(headerValues))
                return null;

            var displayHeaderLabels = BuildVisualHeaderLabels(headerValues, allWords, physicalLaneCenters);

            var rawGrid = new List<List<string>>
            {
                new List<string> { "Radius (m)" }
                    .Concat(displayHeaderLabels)
                    .ToList()
            };

            int nonEmptyCells = 0;
            int skippedBodyValues = 0;
            var usedRows = new List<RowStreamData>();

            foreach (var streamRow in streamRows)
            {
                var row = Enumerable.Repeat(string.Empty, columnCount + 1).ToList();
                row[0] = FormatNumber(streamRow.Radius);

                int assigned = 0;

                for (int i = 0; i < streamRow.BodyNumbers.Count; i++)
                {
                    var bodyNumber = streamRow.BodyNumbers[i];
                    int sequentialColumn = streamRow.StartColumn + i;
                    int column = usesPhysicalLaneReconstruction && physicalLaneCenters != null
                        ? FindNearestLaneIndex(physicalLaneCenters, bodyNumber.Word.CenterX)
                        : ResolveSequentialOrGeometryColumnForBodyValue(
                            headerTokensForColumnAssignment,
                            columnCount,
                            sequentialColumn,
                            bodyNumber.Word.CenterX);

                    if (column < 0 || column >= columnCount)
                    {
                        skippedBodyValues++;
                        continue;
                    }

                    if (usesPhysicalLaneReconstruction
                        && physicalLaneCenters != null
                        && !IsWithinLaneTolerance(physicalLaneCenters, column, bodyNumber.Word.CenterX))
                    {
                        skippedBodyValues++;
                        continue;
                    }

                    if (!string.IsNullOrWhiteSpace(row[column + 1]))
                    {
                        skippedBodyValues++;
                        continue;
                    }

                    row[column + 1] = FormatNumber(bodyNumber.Value);
                    assigned++;
                    nonEmptyCells++;
                }

                if (assigned == 0)
                    continue;

                rawGrid.Add(row);
                usedRows.Add(streamRow);
            }

            int radiusRows = rawGrid.Count - 1;
            if (radiusRows < MinimumRadiusRowCount)
                return null;

            double density = (double)nonEmptyCells / Math.Max(1, radiusRows * columnCount);
            double topRowsDensity = GetTopRowsDensity(rawGrid, Math.Min(8, radiusRows));
            double firstColumnDensity = GetColumnDensity(rawGrid, 1);
            double columnCoverage = GetColumnCoverage(rawGrid);
            double radiusIncreasingRatio = GetRadiusIncreasingRatio(rawGrid);
            double capacityTrendRatio = GetCapacityTrendRatio(rawGrid);
            double headerIncreasingRatio = GetNonDecreasingRatio(headerValues);
            double keywordScore = ScoreKeywords(allWords);

            bool emptyTopLeftPenalty = topRowsDensity < 0.25 || firstColumnDensity < 0.25;

            double score =
                columnCount * 18 +
                radiusRows * 12 +
                nonEmptyCells * 3.8 +
                density * 150 +
                topRowsDensity * 180 +
                firstColumnDensity * 110 +
                columnCoverage * 90 +
                radiusIncreasingRatio * 70 +
                capacityTrendRatio * 45 +
                headerIncreasingRatio * 45 +
                keywordScore;

            if (usesPhysicalLaneReconstruction)
                score += 180;

            if (columnCount == header.Numbers.Count && columnCount >= 8)
                score += 90;

            if (columnCount <= 4)
                score -= 260;

            if (emptyTopLeftPenalty)
                score -= 120;

            if (radiusIncreasingRatio < 0.85)
                score -= 90;

            if (columnCoverage < 0.70)
                score -= 70;

            if (density < 0.12)
                score -= 80;

            if (skippedBodyValues > nonEmptyCells * 0.4)
                score -= 80;

            return new CandidateGrid(
                rawGrid,
                headerValues,
                columnCount,
                usedRows,
                nonEmptyCells,
                skippedBodyValues,
                density,
                topRowsDensity,
                firstColumnDensity,
                columnCoverage,
                radiusIncreasingRatio,
                capacityTrendRatio,
                emptyTopLeftPenalty,
                score);
        }

        private static List<string> BuildVisualHeaderLabels(
            IReadOnlyList<double> headerValues,
            IReadOnlyList<PdfWordInfo> allWords,
            IReadOnlyList<double>? physicalLaneCenters)
        {
            if (physicalLaneCenters == null)
                return headerValues.Select(FormatNumber).ToList();

            var markerWords = allWords
                .Where(IsConditionMarkerWord)
                .Where(word => IsWithinVisualLabelHorizontalRange(word.CenterX, physicalLaneCenters))
                .ToList();

            if (TryInferLocalHeaderLabelBand(
                    headerValues,
                    physicalLaneCenters,
                    allWords,
                    out double localBandUpperY,
                    out double localBandLowerY))
            {
                return BuildVisualHeaderLabelsForTesting(
                    headerValues,
                    physicalLaneCenters,
                    markerWords.Select(word => word.Text).ToList(),
                    markerWords.Select(word => word.CenterX).ToList(),
                    markerWords.Select(word => word.CenterY).ToList(),
                    localBandUpperY,
                    localBandLowerY);
            }

            // Conservative fallback: if the table-local subheader band cannot be inferred,
            // only direct symbol markers can label duplicate visual lanes. Textual/global
            // operation markers such as 360 degrees are intentionally ignored.
            var symbolMarkerWords = markerWords
                .Where(IsConditionSymbolMarkerWord)
                .ToList();

            return BuildVisualHeaderLabelsForTesting(
                headerValues,
                physicalLaneCenters,
                symbolMarkerWords.Select(word => word.Text).ToList(),
                symbolMarkerWords.Select(word => word.CenterX).ToList(),
                symbolMarkerWords.Select(word => word.CenterY).ToList());
        }

        private static List<string> BuildVisualHeaderLabelsForTesting(
            IReadOnlyList<double> headerValues,
            IReadOnlyList<double> laneCenters,
            IReadOnlyList<string> markerTexts,
            IReadOnlyList<double> markerCenters,
            IReadOnlyList<double>? markerCenterYs = null,
            double? localBandUpperY = null,
            double? localBandLowerY = null)
        {
            var labels = headerValues.Select(FormatNumber).ToList();

            if (headerValues.Count == 0
                || headerValues.Count != laneCenters.Count
                || markerTexts.Count != markerCenters.Count
                || (markerCenterYs != null && markerCenterYs.Count != markerTexts.Count)
                || !HasDuplicateNumericValues(headerValues))
            {
                return labels;
            }

            var duplicateIndexes = Enumerable.Range(0, headerValues.Count)
                .GroupBy(index => headerValues[index])
                .Where(group => group.Count() > 1)
                .SelectMany(group => group)
                .ToHashSet();

            if (duplicateIndexes.Count == 0)
                return labels;

            var candidateMarkers = new List<ConditionMarkerCandidate>();

            for (int i = 0; i < markerTexts.Count; i++)
            {
                if (markerCenterYs != null && localBandUpperY.HasValue && localBandLowerY.HasValue)
                {
                    double y = markerCenterYs[i];
                    if (y > localBandUpperY.Value + 0.001 || y < localBandLowerY.Value - 0.001)
                        continue;
                }

                string marker = NormalizeConditionMarker(markerTexts[i]);
                if (string.IsNullOrWhiteSpace(marker))
                    continue;

                int laneIndex = GetExclusiveLaneIndex(markerCenters[i], laneCenters);
                if (laneIndex < 0 || !duplicateIndexes.Contains(laneIndex))
                    continue;

                candidateMarkers.Add(new ConditionMarkerCandidate(
                    Marker: marker,
                    CenterX: markerCenters[i],
                    LaneIndex: laneIndex,
                    DistanceToLaneCenter: Math.Abs(markerCenters[i] - laneCenters[laneIndex]),
                    Priority: GetConditionMarkerPriority(marker)));
            }

            if (candidateMarkers.Count == 0)
                return labels;

            foreach (var laneGroup in candidateMarkers.GroupBy(marker => marker.LaneIndex))
            {
                var selected = laneGroup
                    .OrderByDescending(marker => marker.Priority)
                    .ThenBy(marker => marker.DistanceToLaneCenter)
                    .First();

                labels[selected.LaneIndex] = $"{FormatNumber(headerValues[selected.LaneIndex])} {selected.Marker}";
            }

            return labels;
        }

        private static int GetExclusiveLaneIndex(
            double centerX,
            IReadOnlyList<double> laneCenters)
        {
            if (laneCenters.Count == 0)
                return -1;

            for (int index = 0; index < laneCenters.Count; index++)
            {
                double leftBoundary = index == 0
                    ? double.NegativeInfinity
                    : (laneCenters[index - 1] + laneCenters[index]) / 2.0;

                double rightBoundary = index == laneCenters.Count - 1
                    ? double.PositiveInfinity
                    : (laneCenters[index] + laneCenters[index + 1]) / 2.0;

                if (centerX >= leftBoundary && centerX < rightBoundary)
                    return index;
            }

            return laneCenters.Count - 1;
        }

                private static int GetConditionMarkerPriority(string marker)
        {
            if (string.IsNullOrWhiteSpace(marker))
                return 0;

            if (marker.Equals("*", StringComparison.Ordinal))
                return 100;

            if (marker.Equals("\u2020", StringComparison.Ordinal))
                return 95;

            if (marker.Equals("#", StringComparison.Ordinal))
                return 90;

            if (marker.Contains("360", StringComparison.OrdinalIgnoreCase))
                return 80;

            if (marker.Contains("rear", StringComparison.OrdinalIgnoreCase))
                return 70;

            if (marker.Contains("front", StringComparison.OrdinalIgnoreCase))
                return 70;

            if (marker.Contains("side", StringComparison.OrdinalIgnoreCase))
                return 70;

            if (marker.Contains("over", StringComparison.OrdinalIgnoreCase))
                return 60;

            return 10;
        }

        private static bool TryInferLocalHeaderLabelBand(
            IReadOnlyList<double> headerValues,
            IReadOnlyList<double> laneCenters,
            IReadOnlyList<PdfWordInfo> allWords,
            out double localBandUpperY,
            out double localBandLowerY)
        {
            localBandUpperY = 0;
            localBandLowerY = 0;

            if (headerValues.Count == 0 || laneCenters.Count == 0 || allWords.Count == 0)
                return false;

            var potentialHeaderWords = allWords
                .Where(word => IsWithinVisualLabelHorizontalRange(word.CenterX, laneCenters))
                .Where(word => IsLikelyHeaderValueWord(word, headerValues))
                .ToList();

            if (potentialHeaderWords.Count == 0)
                return false;

            var headerRows = ClusterRows(potentialHeaderWords)
                .Select(row => new
                {
                    Words = row,
                    CenterY = row.Average(word => word.CenterY),
                    DistinctHeaderMatches = row
                        .Select(word => TryExtractNumber(word.Text, out double value) ? Math.Round(value, 2) : double.NaN)
                        .Where(value => !double.IsNaN(value))
                        .Distinct()
                        .Count()
                })
                .Where(row => row.DistinctHeaderMatches >= Math.Min(3, headerValues.Distinct().Count()))
                .OrderByDescending(row => row.DistinctHeaderMatches)
                .ThenByDescending(row => row.CenterY)
                .ToList();

            var headerRow = headerRows.FirstOrDefault();
            if (headerRow == null)
                return false;

            double headerCenterY = headerRow.CenterY;

            var candidateBodyWords = allWords
                .Where(word => IsWithinVisualLabelHorizontalRange(word.CenterX, laneCenters))
                .Where(word => word.CenterY < headerCenterY - 2.0)
                .Where(IsLikelyCapacityBodyWord)
                .ToList();

            if (candidateBodyWords.Count == 0)
                return false;

            var firstDataRow = ClusterRows(candidateBodyWords)
                .Select(row => new
                {
                    Words = row,
                    CenterY = row.Average(word => word.CenterY),
                    Count = row.Count
                })
                .Where(row => row.CenterY < headerCenterY - 2.0)
                .OrderByDescending(row => row.CenterY)
                .FirstOrDefault();

            if (firstDataRow == null)
                return false;

            double firstDataCenterY = firstDataRow.CenterY;
            double gap = headerCenterY - firstDataCenterY;

            if (gap < 4.0)
                return false;

            double margin = Math.Max(0.5, gap * 0.08);
            localBandUpperY = headerCenterY - margin;
            localBandLowerY = firstDataCenterY + margin;

            if (localBandLowerY >= localBandUpperY)
            {
                localBandUpperY = headerCenterY;
                localBandLowerY = firstDataCenterY;
            }

            return localBandLowerY < localBandUpperY;
        }

        private static bool IsWithinVisualLabelHorizontalRange(
            double centerX,
            IReadOnlyList<double> laneCenters)
        {
            if (laneCenters.Count == 0)
                return false;

            double medianGap = GetMedianPositiveGap(laneCenters.ToList());
            double margin = Math.Max(10.0, medianGap);

            return centerX >= laneCenters.Min() - margin
                && centerX <= laneCenters.Max() + margin;
        }

        private static bool IsLikelyHeaderValueWord(
            PdfWordInfo word,
            IReadOnlyList<double> headerValues)
        {
            if (!TryExtractNumber(word.Text, out double value))
                return false;

            return headerValues.Any(headerValue => Math.Abs(headerValue - value) < 0.05);
        }

        private static bool IsLikelyCapacityBodyWord(PdfWordInfo word)
        {
            if (!TryExtractNumber(word.Text, out double value))
                return false;

            return value > 0 && value <= 1000;
        }

                private static bool IsConditionSymbolMarkerWord(PdfWordInfo word)
        {
            string marker = NormalizeConditionMarker(word.Text);
            return marker is "*" or "\u2020" or "#";
        }

        private sealed record ConditionMarkerCandidate(
            string Marker,
            double CenterX,
            int LaneIndex,
            double DistanceToLaneCenter,
            int Priority);

        private static string? FindNearestConditionMarker(
            double laneCenter,
            IReadOnlyList<double> laneCenters,
            IReadOnlyList<double> markerCenters,
            IReadOnlyList<string> normalizedMarkers)
        {
            // Retained for compatibility with older generated diagnostics/tests. New label
            // assignment uses exclusive visual lane regions through GetExclusiveLaneIndex.
            if (markerCenters.Count == 0 || laneCenters.Count == 0)
                return null;

            int laneIndex = FindNearestLaneIndex(laneCenters.ToList(), laneCenter);
            if (laneIndex < 0)
                return null;

            var candidates = new List<(string Marker, double Distance, int Priority)>();

            for (int i = 0; i < markerCenters.Count; i++)
            {
                if (GetExclusiveLaneIndex(markerCenters[i], laneCenters) != laneIndex)
                    continue;

                string marker = normalizedMarkers[i];
                if (string.IsNullOrWhiteSpace(marker))
                    continue;

                candidates.Add((marker, Math.Abs(markerCenters[i] - laneCenter), GetConditionMarkerPriority(marker)));
            }

            return candidates
                .OrderByDescending(candidate => candidate.Priority)
                .ThenBy(candidate => candidate.Distance)
                .Select(candidate => candidate.Marker)
                .FirstOrDefault();
        }

        private static bool IsConditionMarkerWord(PdfWordInfo word)
        {
            return !string.IsNullOrWhiteSpace(NormalizeConditionMarker(word.Text));
        }

                private static string NormalizeConditionMarker(string? text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return string.Empty;

            string normalized = text.Trim();
            string compact = Regex.Replace(normalized, @"\s+", string.Empty);

            if (compact.Equals("*", StringComparison.Ordinal))
                return "*";

            if (compact.Equals("#", StringComparison.Ordinal))
                return "#";

            // PDF text extraction can emit degree symbols through several encoding paths.
            // Keep the legacy internal marker representation expected by the existing
            // regression tests, while detecting the marker semantically by the 360 token.
            if (normalized.Contains("360", StringComparison.OrdinalIgnoreCase)
                || compact.Contains("360", StringComparison.OrdinalIgnoreCase))
            {
                return "360\u00C2\u00B0";
            }

            if (normalized.Contains("rear", StringComparison.OrdinalIgnoreCase))
                return "rear";

            if (normalized.Contains("front", StringComparison.OrdinalIgnoreCase))
                return "front";

            if (normalized.Contains("side", StringComparison.OrdinalIgnoreCase))
                return "side";

            if (normalized.Contains("over", StringComparison.OrdinalIgnoreCase))
                return "over";

            if (LooksLikeStandaloneConditionSymbol(compact))
                return "\u2020";

            return string.Empty;
        }

        private static bool LooksLikeStandaloneConditionSymbol(string compact)
        {
            if (string.IsNullOrWhiteSpace(compact))
                return false;

            if (compact.Length > 12)
                return false;

            return compact.Any(character => !char.IsLetterOrDigit(character));
        }
private static List<double> InferPhysicalCapacityLaneCenters(
            List<RowStreamData> streamRows,
            List<NumericToken> orderedHeaderTokens)
        {
            if (streamRows.Count == 0 || orderedHeaderTokens.Count == 0)
                return new List<double>();

            double medianHeaderGap = GetMedianPositiveGap(
                orderedHeaderTokens.Select(token => token.Word.CenterX).ToList());

            if (medianHeaderGap <= 0)
                return new List<double>();

            var capacityXs = streamRows
                .SelectMany(row => row.BodyNumbers.Select(token => token.Word.CenterX))
                .OrderBy(value => value)
                .ToList();

            if (capacityXs.Count == 0)
                return new List<double>();

            double clusterTolerance = Math.Max(3.0, medianHeaderGap * 0.24);
            var clusters = new List<List<double>>();

            foreach (double x in capacityXs)
            {
                var lastCluster = clusters.LastOrDefault();
                if (lastCluster == null || Math.Abs(lastCluster.Average() - x) > clusterTolerance)
                {
                    clusters.Add(new List<double> { x });
                    continue;
                }

                lastCluster.Add(x);
            }

            int minimumClusterSupport = Math.Max(2, (int)Math.Ceiling(streamRows.Count * 0.06));

            var lanes = clusters
                .Where(cluster => cluster.Count >= minimumClusterSupport)
                .Select(cluster => cluster.Average())
                .OrderBy(center => center)
                .ToList();

            int headerCount = orderedHeaderTokens.Count;
            if (lanes.Count < headerCount || lanes.Count > headerCount + 2)
                return new List<double>();

            return lanes;
        }

        private static List<double> MapHeaderValuesToPhysicalLanes(
            List<NumericToken> orderedHeaderTokens,
            List<double> laneCenters)
        {
            var headerValues = orderedHeaderTokens.Select(token => token.Value).ToList();
            var headerCenters = orderedHeaderTokens.Select(token => token.Word.CenterX).ToList();

            return ReconstructPhysicalLaneHeaderValuesForTesting(
                headerValues,
                headerCenters,
                laneCenters);
        }

        private static List<double> ReconstructPhysicalLaneHeaderValuesForTesting(
            IReadOnlyList<double> headerValues,
            IReadOnlyList<double> headerCenters,
            IReadOnlyList<double> laneCenters)
        {
            if (headerValues.Count == 0
                || headerValues.Count != headerCenters.Count
                || laneCenters.Count < headerValues.Count
                || laneCenters.Count > headerValues.Count + 2)
            {
                return new List<double>();
            }

            var mapped = new List<double>();

            foreach (double laneCenter in laneCenters)
            {
                int nearestHeaderIndex = 0;
                double nearestDistance = Math.Abs(headerCenters[0] - laneCenter);

                for (int i = 1; i < headerCenters.Count; i++)
                {
                    double distance = Math.Abs(headerCenters[i] - laneCenter);
                    if (distance < nearestDistance)
                    {
                        nearestDistance = distance;
                        nearestHeaderIndex = i;
                    }
                }

                mapped.Add(headerValues[nearestHeaderIndex]);
            }

            if (GetNonDecreasingRatio(mapped) < 0.90)
                return new List<double>();

            if (laneCenters.Count > headerValues.Count && !HasDuplicateNumericValues(mapped))
                return new List<double>();

            return mapped;
        }

        private static int FindNearestLaneIndex(List<double> laneCenters, double x)
        {
            if (laneCenters.Count == 0)
                return -1;

            int nearestIndex = 0;
            double nearestDistance = Math.Abs(laneCenters[0] - x);

            for (int i = 1; i < laneCenters.Count; i++)
            {
                double distance = Math.Abs(laneCenters[i] - x);
                if (distance < nearestDistance)
                {
                    nearestDistance = distance;
                    nearestIndex = i;
                }
            }

            return nearestIndex;
        }

        private static bool IsWithinLaneTolerance(List<double> laneCenters, int laneIndex, double x)
        {
            if (laneCenters.Count <= 1)
                return true;

            double medianGap = GetMedianPositiveGap(laneCenters);
            double tolerance = Math.Max(5.0, medianGap * 0.58);

            return Math.Abs(laneCenters[laneIndex] - x) <= tolerance;
        }

        private static double GetMedianPositiveGap(List<double> centers)
        {
            if (centers.Count < 2)
                return 0;

            var sorted = centers.OrderBy(value => value).ToList();
            var gaps = new List<double>();

            for (int i = 1; i < sorted.Count; i++)
            {
                double gap = sorted[i] - sorted[i - 1];
                if (gap > 0.001)
                    gaps.Add(gap);
            }

            if (gaps.Count == 0)
                return 0;

            return Median(gaps);
        }

        private static double GetNonDecreasingRatio(IReadOnlyList<double> values)
        {
            if (values.Count < 2)
                return 0;

            int nonDecreasing = 0;
            for (int i = 1; i < values.Count; i++)
            {
                if (values[i] >= values[i - 1] - 0.001)
                    nonDecreasing++;
            }

            return (double)nonDecreasing / (values.Count - 1);
        }

        private static List<List<string>> BuildConservativeImportGrid(IReadOnlyList<List<string>> exactGrid)
        {
            if (exactGrid.Count < 2 || exactGrid[0].Count < 2)
                return new List<List<string>>();

            var headerGroups = new List<(double BoomLength, string DisplayHeader, List<int> Columns)>();

            for (int columnIndex = 1; columnIndex < exactGrid[0].Count; columnIndex++)
            {
                string headerText = exactGrid[0][columnIndex];
                if (!TryExtractNumber(headerText, out double boomLength))
                    continue;

                int existingIndex = headerGroups.FindIndex(group =>
                    Math.Abs(group.BoomLength - boomLength) < 0.001);

                if (existingIndex >= 0)
                {
                    headerGroups[existingIndex].Columns.Add(columnIndex);
                }
                else
                {
                    headerGroups.Add((boomLength, FormatNumber(boomLength), new List<int> { columnIndex }));
                }
            }

            if (headerGroups.Count == 0)
                return new List<List<string>>();

            var importGrid = new List<List<string>>
            {
                new List<string> { "Radius (m)" }
                    .Concat(headerGroups.Select(group => group.DisplayHeader))
                    .ToList()
            };

            foreach (var sourceRow in exactGrid.Skip(1))
            {
                if (sourceRow.Count == 0 || !TryExtractNumber(sourceRow[0], out double radius))
                    continue;

                var row = new List<string> { FormatNumber(radius) };

                foreach (var group in headerGroups)
                {
                    var positiveValues = new List<double>();

                    foreach (int columnIndex in group.Columns)
                    {
                        if (columnIndex >= sourceRow.Count)
                            continue;

                        if (TryExtractNumber(sourceRow[columnIndex], out double capacity) && capacity > 0)
                            positiveValues.Add(capacity);
                    }

                    row.Add(positiveValues.Count == 0
                        ? string.Empty
                        : FormatNumber(positiveValues.Min()));
                }

                importGrid.Add(row);
            }

            return importGrid;
        }

        private static NumericToken? FindLeftRadius(
            List<NumericToken> numbers,
            double firstHeaderX,
            double medianHeaderGap)
        {
            if (numbers.Count == 0)
                return null;

            var first = numbers.First();
            var last = numbers.Last();

            if (!ReferenceEquals(first.Word, last.Word)
                && AreClose(first.Value, last.Value)
                && first.Word.CenterX < last.Word.CenterX)
            {
                return first;
            }

            var left = numbers
                .Where(token => token.Word.CenterX < firstHeaderX - medianHeaderGap * 0.10)
                .OrderBy(token => token.Word.CenterX)
                .FirstOrDefault();

            if (left.Word != null)
                return left;

            return first;
        }

        private static NumericToken? FindRightDuplicateRadius(
            List<NumericToken> numbers,
            NumericToken radiusToken,
            double lastHeaderX)
        {
            var candidates = numbers
                .Where(token => !ReferenceEquals(token.Word, radiusToken.Word))
                .Where(token => token.Word.CenterX > lastHeaderX)
                .Where(token => AreClose(token.Value, radiusToken.Value))
                .OrderByDescending(token => token.Word.CenterX)
                .ToList();

            return candidates.Count == 0
                ? null
                : candidates.First();
        }

        private static int EstimateStartColumn(List<NumericToken> headerNumbers, double firstBodyX)
        {
            if (headerNumbers.Count == 0)
                return 0;

            var headers = headerNumbers
                .OrderBy(token => token.Word.CenterX)
                .ToList();

            int nearest = 0;
            double bestDistance = double.MaxValue;

            for (int i = 0; i < headers.Count; i++)
            {
                double distance = Math.Abs(headers[i].Word.CenterX - firstBodyX);
                if (distance < bestDistance)
                {
                    nearest = i;
                    bestDistance = distance;
                }
            }

            return Math.Max(0, nearest);
        }

                private static bool IsStrongTableCandidate(PdfTableCandidateDto candidate)
        {
            return !IsLikelyBodyFragmentCandidate(candidate)
                && candidate.BoomColumnCount >= 3
                && candidate.RadiusRowCount >= 6
                && candidate.NonEmptyCapacityCellCount >= Math.Max(18, candidate.BoomColumnCount * 2);
        }

        private static bool IsLikelyBodyFragmentCandidate(PdfTableCandidateDto candidate)
        {
            if (candidate == null || candidate.RawGrid.Count < 3)
                return false;

            var headerValues = GetCandidateHeaderValues(candidate)
                .Where(value => value > 0 && value < 500)
                .ToList();

            if (headerValues.Count < 3)
                return false;

            var radiusValues = GetCandidateRadiusValues(candidate)
                .Where(value => value > 0 && value < 500)
                .ToList();

            if (radiusValues.Count < MinimumRadiusRowCount)
                return false;

            double medianHeader = GetMedianCandidateValue(headerValues);
            double medianRadius = GetMedianCandidateValue(radiusValues);
            double minRadius = radiusValues.Min();
            double trimmedHeaderMax = GetUpperCandidateQuantileValue(headerValues, 0.75);
            double smallHeaderRatio = headerValues.Count(value => value <= 8.0) / (double)headerValues.Count;

            bool capacityValuesUsedAsHeaders =
                smallHeaderRatio >= 0.60
                && medianHeader <= 8.0
                && minRadius >= 20.0;

            bool radiusScaleFarExceedsHeaderScale =
                medianHeader <= 12.0
                && medianRadius >= 24.0
                && medianRadius > Math.Max(1.0, medianHeader) * 3.0;

            bool lowerTailWithSingleRadiusOutlierHeader =
                smallHeaderRatio >= 0.55
                && trimmedHeaderMax <= 10.0
                && minRadius >= 30.0;

            return capacityValuesUsedAsHeaders
                || radiusScaleFarExceedsHeaderScale
                || lowerTailWithSingleRadiusOutlierHeader;
        }

        private static List<double> GetCandidateRadiusValues(PdfTableCandidateDto candidate)
        {
            return candidate.RawGrid
                .Skip(1)
                .Where(row => row.Count > 0 && !IsNumberOfLinesLabel(row[0]))
                .Select(row => TryExtractNumber(row[0], out double value) ? (double?)value : null)
                .Where(value => value.HasValue)
                .Select(value => value!.Value)
                .ToList();
        }

        private static double GetMedianCandidateValue(IReadOnlyList<double> values)
        {
            if (values.Count == 0)
                return 0;

            var ordered = values.OrderBy(value => value).ToList();
            int middle = ordered.Count / 2;

            return ordered.Count % 2 == 1
                ? ordered[middle]
                : (ordered[middle - 1] + ordered[middle]) / 2.0;
        }

        private static double GetUpperCandidateQuantileValue(IReadOnlyList<double> values, double quantile)
        {
            if (values.Count == 0)
                return 0;

            var ordered = values.OrderBy(value => value).ToList();
            double clamped = Math.Max(0.0, Math.Min(1.0, quantile));
            int index = (int)Math.Round((ordered.Count - 1) * clamped);
            return ordered[Math.Max(0, Math.Min(index, ordered.Count - 1))];
        }

        private static List<PdfTableCandidateDto> BuildUniversalGeometryFallbackCandidates(
            List<List<PdfWordInfo>> rows,
            int pageNumber,
            List<PdfWordInfo> allWords,
            int firstTableIndex)
        {
            var candidates = new List<PdfTableCandidateDto>();

            var header = FindUniversalGeometryHeaderCandidate(rows);
            if (header == null)
                return candidates;

            var radiusColumns = FindUniversalGeometryRadiusColumns(rows, header.RowIndex);
            if (radiusColumns.Count < 2)
                return candidates;

            foreach (var region in BuildUniversalGeometryRegions(header, radiusColumns))
            {
                var candidate = TryBuildUniversalGeometryRegionCandidate(
                    rows,
                    header,
                    region,
                    pageNumber,
                    firstTableIndex + candidates.Count,
                    allWords);

                if (candidate == null)
                    continue;

                candidates.Add(candidate);
            }

            return candidates;
        }

        private static HeaderCandidate? FindUniversalGeometryHeaderCandidate(List<List<PdfWordInfo>> rows)
        {
            HeaderCandidate? best = null;
            double bestScore = double.MinValue;

            for (int rowIndex = 0; rowIndex < rows.Count; rowIndex++)
            {
                var numbers = GetNumericTokens(rows[rowIndex])
                    .Where(token => token.Value > 0 && token.Value < 200)
                    .OrderBy(token => token.Word.CenterX)
                    .ToList();

                numbers = RemoveNearDuplicateNumbers(numbers);

                if (numbers.Count < 6)
                    continue;

                double xSpan = numbers.Last().Word.CenterX - numbers.First().Word.CenterX;
                if (xSpan < 120)
                    continue;

                int duplicateNumericValueCount = numbers
                    .GroupBy(token => FormatNumber(token.Value))
                    .Count(group => group.Count() >= 2);

                double increasingRatio = GetIncreasingRatio(numbers.Select(token => token.Value).ToList());
                double spacingRegularity = GetSpacingRegularity(numbers.Select(token => token.Word.CenterX).ToList());

                bool nearbyHeaderWords = Enumerable.Range(Math.Max(0, rowIndex - 3), Math.Min(4, rowIndex + 1))
                    .Any(index =>
                    {
                        string text = string.Join(" ", rows[index].Select(word => word.Text)).ToLowerInvariant();
                        return text.Contains("radius") || text.Contains("boom") || text.Contains("load chart") || text.Contains("main boom");
                    });

                bool hasBodyRowsBelow = rows
                    .Skip(rowIndex + 1)
                    .Take(28)
                    .Count(row => GetNumericTokens(row).Count >= 4) >= 6;

                if (!hasBodyRowsBelow)
                    continue;

                if (duplicateNumericValueCount == 0 && increasingRatio < 0.45)
                    continue;

                double score =
                    numbers.Count * 8 +
                    duplicateNumericValueCount * 34 +
                    Math.Min(xSpan / 8d, 130) +
                    spacingRegularity * 20 +
                    increasingRatio * 18 +
                    (nearbyHeaderWords ? 180 : 0) -
                    rowIndex * 1.5;

                if (score <= bestScore)
                    continue;

                bestScore = score;
                best = new HeaderCandidate(rowIndex, rows[rowIndex], numbers, score);
            }

            return best;
        }

        private static List<GeometryRadiusColumn> FindUniversalGeometryRadiusColumns(
            List<List<PdfWordInfo>> rows,
            int headerRowIndex)
        {
            var columns = new List<GeometryRadiusColumn>();

            for (int rowIndex = headerRowIndex + 1; rowIndex < rows.Count; rowIndex++)
            {
                var numbers = GetNumericTokens(rows[rowIndex])
                    .Where(token => token.Value > 0 && token.Value <= 250)
                    .OrderBy(token => token.Word.CenterX)
                    .ToList();

                foreach (var token in numbers)
                {
                    GeometryRadiusColumn? match = columns
                        .OrderBy(column => Math.Abs(column.CenterX - token.Word.CenterX))
                        .FirstOrDefault(column => Math.Abs(column.CenterX - token.Word.CenterX) <= 8);

                    if (match == null)
                    {
                        columns.Add(new GeometryRadiusColumn(token.Word.CenterX));
                        match = columns[columns.Count - 1];
                    }

                    match.Add(rowIndex, token);
                }
            }

            return columns
                .Where(column =>
                    column.Tokens.Count >= 6 &&
                    column.MinValue <= 12 &&
                    column.MaxValue - column.MinValue >= 2 &&
                    column.IncreasingRatio >= 0.62)
                .OrderBy(column => column.CenterX)
                .ToList();
        }

        private static List<GeometryTableRegion> BuildUniversalGeometryRegions(
            HeaderCandidate header,
            List<GeometryRadiusColumn> radiusColumns)
        {
            var regions = new List<GeometryTableRegion>();

            for (int index = 0; index < radiusColumns.Count - 1; index++)
            {
                var leftRadius = radiusColumns[index];
                var rightRadius = radiusColumns[index + 1];

                double width = rightRadius.CenterX - leftRadius.CenterX;
                if (width < 150)
                    continue;

                var headerNumbers = header.Numbers
                    .Where(token => token.Word.CenterX > leftRadius.CenterX + 8)
                    .Where(token => token.Word.CenterX < rightRadius.CenterX - 8)
                    .OrderBy(token => token.Word.CenterX)
                    .ToList();

                if (headerNumbers.Count < MinimumBoomColumnCount)
                    continue;

                var headerRuns = BuildUniversalGeometryHeaderRuns(headerNumbers);
                if (headerRuns.Count < MinimumBoomColumnCount)
                    continue;

                regions.Add(new GeometryTableRegion(leftRadius, rightRadius, headerRuns));
            }

            return regions;
        }

        private static List<GeometryHeaderRun> BuildUniversalGeometryHeaderRuns(List<NumericToken> headerNumbers)
        {
            var runs = new List<GeometryHeaderRun>();

            foreach (var token in headerNumbers.OrderBy(token => token.Word.CenterX))
            {
                var last = runs.LastOrDefault();
                if (last != null
                    && AreClose(last.BoomLength, token.Value)
                    && token.Word.CenterX - last.Right <= 45)
                {
                    last.Add(token);
                    continue;
                }

                runs.Add(new GeometryHeaderRun(token.Value, token));
            }

            var uniqueRuns = new List<GeometryHeaderRun>();
            foreach (var run in runs)
            {
                if (uniqueRuns.Any(existing => AreClose(existing.BoomLength, run.BoomLength)))
                    continue;

                uniqueRuns.Add(run);
            }

            return uniqueRuns;
        }

        private static PdfTableCandidateDto? TryBuildUniversalGeometryRegionCandidate(
            List<List<PdfWordInfo>> rows,
            HeaderCandidate header,
            GeometryTableRegion region,
            int pageNumber,
            int tableIndex,
            List<PdfWordInfo> allWords)
        {
            int columnCount = region.HeaderRuns.Count;
            if (columnCount < MinimumBoomColumnCount)
                return null;

            var rawGrid = new List<List<string>>
            {
                new List<string> { "Radius (m)" }
                    .Concat(region.HeaderRuns.Select(run => FormatNumber(run.BoomLength)))
                    .ToList()
            };

            var usedRows = new List<List<PdfWordInfo>>();
            int nonEmptyCells = 0;
            double? previousRadius = null;
            int skippedRowsAfterStart = 0;

            for (int rowIndex = header.RowIndex + 1; rowIndex < rows.Count; rowIndex++)
            {
                var row = rows[rowIndex];
                var numbers = GetNumericTokens(row)
                    .Where(token => token.Value > 0)
                    .OrderBy(token => token.Word.CenterX)
                    .ToList();

                if (numbers.Count < 2)
                {
                    if (rawGrid.Count > 1 && ++skippedRowsAfterStart >= 8)
                        break;

                    continue;
                }

                var radiusToken = FindUniversalGeometryRadiusToken(numbers, region);
                if (radiusToken == null)
                {
                    if (rawGrid.Count > 1 && ++skippedRowsAfterStart >= 8)
                        break;

                    continue;
                }

                double radius = radiusToken.Value.Value;
                if (radius <= 0 || radius > 500)
                    continue;

                if (previousRadius.HasValue && radius < previousRadius.Value - 0.001)
                {
                    if (rawGrid.Count > MinimumRadiusRowCount)
                        break;

                    continue;
                }

                var outputRow = Enumerable.Repeat(string.Empty, columnCount + 1).ToList();
                outputRow[0] = FormatNumber(radius);

                int assigned = 0;
                for (int columnIndex = 0; columnIndex < region.HeaderRuns.Count; columnIndex++)
                {
                    var run = region.HeaderRuns[columnIndex];

                    var capacityTokens = numbers
                        .Where(token => !ReferenceEquals(token.Word, radiusToken.Value.Word))
                        .Where(token => token.Word.CenterX >= run.Left - 4)
                        .Where(token => token.Word.CenterX <= run.Right + 4)
                        .Where(token => token.Value > 0 && token.Value <= 10000)
                        .ToList();

                    if (capacityTokens.Count == 0)
                        continue;

                    double capacity = capacityTokens.Max(token => token.Value);
                    outputRow[columnIndex + 1] = FormatNumber(capacity);
                    assigned++;
                    nonEmptyCells++;
                }

                if (assigned == 0)
                {
                    if (rawGrid.Count > 1 && ++skippedRowsAfterStart >= 8)
                        break;

                    continue;
                }

                rawGrid.Add(outputRow);
                usedRows.Add(row);
                previousRadius = radius;
                skippedRowsAfterStart = 0;
            }

            int radiusRows = rawGrid.Count - 1;
            if (radiusRows < 6)
                return null;

            double density = (double)nonEmptyCells / Math.Max(1, radiusRows * columnCount);
            if (density < 0.10)
                return null;

            double radiusIncreasingRatio = GetRadiusIncreasingRatio(rawGrid);
            double topRowsDensity = GetTopRowsDensity(rawGrid, Math.Min(8, radiusRows));
            double firstColumnDensity = GetColumnDensity(rawGrid, 1);
            double columnCoverage = GetColumnCoverage(rawGrid);
            double capacityTrendRatio = GetCapacityTrendRatio(rawGrid);

            if (radiusIncreasingRatio < 0.72)
                return null;

            if (columnCoverage < 0.45)
                return null;

            var boundsWords = new List<PdfWordInfo>();
            boundsWords.AddRange(header.RowWords.Where(word =>
                word.CenterX >= region.LeftRadius.CenterX - 15 &&
                word.CenterX <= region.RightRadius.CenterX + 15));

            boundsWords.AddRange(usedRows.SelectMany(row => row.Where(word =>
                word.CenterX >= region.LeftRadius.CenterX - 15 &&
                word.CenterX <= region.RightRadius.CenterX + 15)));

            if (boundsWords.Count == 0)
                boundsWords = usedRows.SelectMany(row => row).ToList();

            bool consolidatedDuplicateHeaders = region.HeaderRuns.Any(run => run.Tokens.Count > 1);
            var rawGridWithNumberOfLines = AppendNumberOfLinesRowIfPresent(
                rawGrid,
                rows,
                header,
                region.HeaderRuns.Select(run => (run.Left + run.Right) / 2.0).ToList());
            var exactVisualGrid = consolidatedDuplicateHeaders
                ? BuildExactVisualGrid(rows, header, region)
                : rawGridWithNumberOfLines;
            int numberOfLinesMetadataColumns = CountNumberOfLinesMetadataCells(exactVisualGrid);
            int visualColumnCount = Math.Max(
                region.HeaderRuns.Sum(run => run.Tokens.Count),
                Math.Max(columnCount, (exactVisualGrid.FirstOrDefault()?.Count ?? 1) - 1));
            int uniqueBoomLengthCount = region.HeaderRuns.Count;
            int exactVisualCellCount = CountNonEmptyCapacityCells(exactVisualGrid);

            double score =
                520 +
                columnCount * 54 +
                radiusRows * 20 +
                nonEmptyCells * 7 +
                density * 260 +
                topRowsDensity * 140 +
                firstColumnDensity * 90 +
                columnCoverage * 120 +
                radiusIncreasingRatio * 95 +
                capacityTrendRatio * 50 +
                ScoreKeywords(allWords);

            if (consolidatedDuplicateHeaders)
                score += 70;

            if (columnCount <= 3)
                score -= 80;

            var warnings = new List<string>
            {
                "Geometry fallback extractor detected a side-by-side or repeated-header load-chart region.",
                "Capacity cells were assigned by PDF X-position bands. Review the preview carefully before saving."
            };

            if (consolidatedDuplicateHeaders)
            {
                warnings.Add("ENVELOPE_IMPORT_REVIEW_REQUIRED: This candidate is a consolidated maximum-capacity envelope, not a full manufacturer multi-dimensional load chart.");
                warnings.Add($"Full manufacturer visual columns detected: {visualColumnCount} visual boom column(s) were consolidated to {uniqueBoomLengthCount} unique boom-length column(s).");
                warnings.Add("Repeated boom-length headers were consolidated into unique boom columns by using the maximum capacity found under each repeated boom-length group. This can overestimate capacity if repeated columns represent line count, counterweight, mode, boom angle, jib, or another operating condition.");
                warnings.Add("Do not save this envelope candidate unless you have manually verified the intended operating condition and line-count/reeving information against the original PDF.");
            }

            warnings.Add($"Fallback region bounds X={region.LeftRadius.CenterX:0.#} to X={region.RightRadius.CenterX:0.#}; extracted {columnCount} unique boom column(s) from {visualColumnCount} visual column(s), {radiusRows} radius row(s), and {nonEmptyCells} capacity cell(s).");

            if (density < 0.25)
                warnings.Add("This extracted region has sparse cells. Verify that the selected table is the intended load chart.");

            if (numberOfLinesMetadataColumns > 0)
                warnings.Add($"Detected Number of lines / parts-of-line metadata for {numberOfLinesMetadataColumns} visual boom column(s); this metadata row is preserved but excluded from capacity parsing.");

            return new PdfTableCandidateDto
            {
                PageNumber = pageNumber,
                TableIndex = tableIndex,
                Left = boundsWords.Count == 0 ? 0 : boundsWords.Min(word => word.Left),
                Right = boundsWords.Count == 0 ? 0 : boundsWords.Max(word => word.Right),
                Top = boundsWords.Count == 0 ? 0 : boundsWords.Max(word => word.Top),
                Bottom = boundsWords.Count == 0 ? 0 : boundsWords.Min(word => word.Bottom),
                BoomColumnCount = consolidatedDuplicateHeaders ? visualColumnCount : columnCount,
                RadiusRowCount = consolidatedDuplicateHeaders ? Math.Max(radiusRows, CountCapacityRows(exactVisualGrid)) : radiusRows,
                NonEmptyCapacityCellCount = consolidatedDuplicateHeaders ? Math.Max(nonEmptyCells, exactVisualCellCount) : nonEmptyCells,
                Score = score,
                ConfidenceLabel = consolidatedDuplicateHeaders ? "Envelope review" : GetConfidenceLabel(score),
                Summary = consolidatedDuplicateHeaders
                    ? $"{visualColumnCount} exact visual boom columns ({uniqueBoomLengthCount} unique boom lengths), {Math.Max(radiusRows, CountCapacityRows(exactVisualGrid))} radius rows. Import uses envelope fallback density {density:P0}."
                    : $"{columnCount} boom columns, {radiusRows} radius rows, geometry fallback density {density:P0}.",
                RawGrid = consolidatedDuplicateHeaders ? exactVisualGrid : rawGridWithNumberOfLines,
                ImportGrid = consolidatedDuplicateHeaders ? rawGridWithNumberOfLines : new List<List<string>>(),
                Warnings = warnings.Distinct().ToList()
            };
        }

        private static List<List<string>> BuildExactVisualGrid(
            List<List<PdfWordInfo>> rows,
            HeaderCandidate header,
            GeometryTableRegion region)
        {
            var visualColumns = region.HeaderRuns
                .SelectMany(run => run.Tokens.Select(token => (BoomLength: run.BoomLength, CenterX: token.Word.CenterX)))
                .OrderBy(column => column.CenterX)
                .ToList();

            if (visualColumns.Count == 0)
            {
                return new List<List<string>>();
            }

            var exactGrid = new List<List<string>>
            {
                new List<string> { "Radius (m)" }
                    .Concat(visualColumns.Select(column => FormatNumber(column.BoomLength)))
                    .ToList()
            };

            double? previousRadius = null;
            int skippedRowsAfterStart = 0;

            for (int rowIndex = header.RowIndex + 1; rowIndex < rows.Count; rowIndex++)
            {
                var row = rows[rowIndex];
                var numbers = GetNumericTokens(row)
                    .Where(token => token.Value > 0)
                    .OrderBy(token => token.Word.CenterX)
                    .ToList();

                if (numbers.Count < 2)
                {
                    if (exactGrid.Count > 1 && ++skippedRowsAfterStart >= 8)
                        break;

                    continue;
                }

                var radiusToken = FindUniversalGeometryRadiusToken(numbers, region);
                if (radiusToken == null)
                {
                    if (exactGrid.Count > 1 && ++skippedRowsAfterStart >= 8)
                        break;

                    continue;
                }

                double radius = radiusToken.Value.Value;
                if (radius <= 0 || radius > 500)
                    continue;

                if (previousRadius.HasValue && radius < previousRadius.Value - 0.001)
                {
                    if (exactGrid.Count > MinimumRadiusRowCount)
                        break;

                    continue;
                }

                var outputRow = Enumerable.Repeat(string.Empty, visualColumns.Count + 1).ToList();
                outputRow[0] = FormatNumber(radius);

                int assigned = 0;
                for (int columnIndex = 0; columnIndex < visualColumns.Count; columnIndex++)
                {
                    var visualColumn = visualColumns[columnIndex];

                    NumericToken? capacityToken = numbers
                        .Where(token => !ReferenceEquals(token.Word, radiusToken.Value.Word))
                        .Where(token => Math.Abs(token.Word.CenterX - region.LeftRadius.CenterX) > 14)
                        .Where(token => Math.Abs(token.Word.CenterX - region.RightRadius.CenterX) > 14)
                        .Where(token => Math.Abs(token.Word.CenterX - visualColumn.CenterX) <= 12)
                        .Where(token => token.Value > 0 && token.Value <= 10000)
                        .OrderBy(token => Math.Abs(token.Word.CenterX - visualColumn.CenterX))
                        .Select(token => (NumericToken?)token)
                        .FirstOrDefault();

                    if (capacityToken == null)
                        continue;

                    outputRow[columnIndex + 1] = FormatNumber(capacityToken.Value.Value);
                    assigned++;
                }

                if (assigned == 0)
                {
                    if (exactGrid.Count > 1 && ++skippedRowsAfterStart >= 8)
                        break;

                    continue;
                }

                exactGrid.Add(outputRow);
                previousRadius = radius;
                skippedRowsAfterStart = 0;
            }

            return AppendNumberOfLinesRowIfPresent(
                exactGrid,
                rows,
                header,
                visualColumns.Select(column => column.CenterX).ToList());
        }

        private static List<double> ResolveCandidateColumnCenters(
            HeaderCandidate header,
            CandidateGrid candidateGrid,
            bool usesPhysicalLaneReconstruction)
        {
            var orderedHeaderTokens = header.Numbers
                .OrderBy(token => token.Word.CenterX)
                .ToList();

            if (usesPhysicalLaneReconstruction)
            {
                var inferredCenters = InferPhysicalCapacityLaneCenters(candidateGrid.UsedRows, orderedHeaderTokens);
                if (inferredCenters.Count == candidateGrid.ColumnCount)
                    return inferredCenters;
            }

            return orderedHeaderTokens
                .Take(candidateGrid.ColumnCount)
                .Select(token => token.Word.CenterX)
                .ToList();
        }

        private static List<List<string>> AppendNumberOfLinesRowIfPresent(
            IReadOnlyList<List<string>> rawGrid,
            List<List<PdfWordInfo>> rows,
            HeaderCandidate header,
            IReadOnlyList<double> columnCenters)
        {
            var result = rawGrid.Select(row => row.ToList()).ToList();

            if (result.Count == 0 || columnCenters.Count == 0 || HasNumberOfLinesRow(result))
                return result;

            var candidates = rows
                .Skip(header.RowIndex + 1)
                .Where(IsNumberOfLinesPdfRow)
                .Select(row => TryBuildNumberOfLinesGridRow(row, columnCenters))
                .Where(row => row != null)
                .Select(row => row!)
                .OrderByDescending(row => CountNumberOfLinesMetadataCells(new[] { row }))
                .ToList();

            var selected = candidates.FirstOrDefault();
            if (selected == null)
                return result;

            result.Add(selected);
            return result;
        }

        private static bool HasNumberOfLinesRow(IReadOnlyList<List<string>> rawGrid)
        {
            return rawGrid.Any(row => row.Count > 0 && IsNumberOfLinesLabel(row[0]));
        }

        private static List<string>? TryBuildNumberOfLinesGridRow(
            List<PdfWordInfo> row,
            IReadOnlyList<double> columnCenters)
        {
            if (columnCenters.Count == 0)
                return null;

            var output = Enumerable.Repeat(string.Empty, columnCenters.Count + 1).ToList();
            output[0] = "Number of lines";

            var numericTokens = GetNumericTokens(row)
                .Where(token => TryConvertToLineCount(token.Value, out _))
                .OrderBy(token => token.Word.CenterX)
                .ToList();

            foreach (var token in numericTokens)
            {
                if (!TryConvertToLineCount(token.Value, out int lineCount))
                    continue;

                int columnIndex = GetExclusiveLaneIndex(token.Word.CenterX, columnCenters);
                if (columnIndex < 0 || columnIndex >= columnCenters.Count)
                    continue;

                if (!IsWithinHeaderColumnAssignmentRegion(columnCenters, columnIndex, token.Word.CenterX))
                    continue;

                output[columnIndex + 1] = lineCount.ToString(CultureInfo.InvariantCulture);
            }

            int assignedCount = CountNumberOfLinesMetadataCells(new[] { output });
            int minimumAssigned = Math.Max(2, Math.Min(4, columnCenters.Count / 4));
            return assignedCount >= minimumAssigned ? output : null;
        }

        private static bool IsNumberOfLinesPdfRow(List<PdfWordInfo> row)
        {
            string text = string.Join(" ", row.Select(word => word.Text));
            return IsNumberOfLinesLabel(text);
        }

        private static bool IsNumberOfLinesLabel(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return false;

            string compact = Regex.Replace(value.ToLowerInvariant(), @"[^a-z0-9]+", string.Empty);
            if (compact.Length == 0)
                return false;

            return compact.Contains("numberoflines")
                || compact.Contains("numberofline")
                || compact.Contains("nooflines")
                || compact.Contains("noofline")
                || compact.Contains("numberofparts")
                || compact.Contains("partsofline")
                || compact.Contains("partsoflines")
                || compact.Contains("partsofrope")
                || compact.Contains("reeving")
                || compact.Contains("lineparts")
                || compact.Contains("linecount");
        }

        private static bool TryConvertToLineCount(double value, out int lineCount)
        {
            lineCount = 0;
            double rounded = Math.Round(value);
            if (Math.Abs(value - rounded) > 0.001)
                return false;

            int candidate = (int)rounded;
            if (candidate < 1 || candidate > 40)
                return false;

            lineCount = candidate;
            return true;
        }

        private static int CountNumberOfLinesMetadataCells(IEnumerable<List<string>> rows)
        {
            return rows
                .Where(row => row.Count > 0 && IsNumberOfLinesLabel(row[0]))
                .Sum(row => row.Skip(1).Count(cell => TryExtractNumber(cell, out double value) && TryConvertToLineCount(value, out _)));
        }

        private static int CountCapacityRows(IReadOnlyList<List<string>> rawGrid)
        {
            if (rawGrid.Count <= 1)
                return 0;

            return rawGrid
                .Skip(1)
                .Count(row => row.Count > 0 && !IsNumberOfLinesLabel(row[0]));
        }

        private static int CountNonEmptyCapacityCells(IReadOnlyList<List<string>> rawGrid)
        {
            if (rawGrid.Count <= 1)
                return 0;

            return rawGrid
                .Skip(1)
                .Where(row => row.Count > 0 && !IsNumberOfLinesLabel(row[0]))
                .Sum(row => row.Skip(1).Count(cell => !string.IsNullOrWhiteSpace(cell)));
        }
        private static NumericToken? FindUniversalGeometryRadiusToken(
            List<NumericToken> numbers,
            GeometryTableRegion region)
        {
            var candidates = numbers
                .Where(token => token.Value > 0 && token.Value <= 500)
                .Where(token =>
                    Math.Abs(token.Word.CenterX - region.LeftRadius.CenterX) <= 14 ||
                    Math.Abs(token.Word.CenterX - region.RightRadius.CenterX) <= 14)
                .OrderBy(token => Math.Min(
                    Math.Abs(token.Word.CenterX - region.LeftRadius.CenterX),
                    Math.Abs(token.Word.CenterX - region.RightRadius.CenterX)))
                .ThenBy(token => token.Word.CenterX)
                .ToList();

            return candidates.Count == 0
                ? null
                : candidates.First();
        }

        private sealed class GeometryRadiusColumn
        {
            private readonly List<GeometryRadiusToken> _tokens = new();

            public GeometryRadiusColumn(double initialCenterX)
            {
                CenterX = initialCenterX;
            }

            public double CenterX { get; private set; }
            public IReadOnlyList<GeometryRadiusToken> Tokens => _tokens;
            public double MinValue => _tokens.Count == 0 ? 0 : _tokens.Min(token => token.Token.Value);
            public double MaxValue => _tokens.Count == 0 ? 0 : _tokens.Max(token => token.Token.Value);

            public double IncreasingRatio
            {
                get
                {
                    var values = _tokens
                        .OrderBy(token => token.RowIndex)
                        .Select(token => token.Token.Value)
                        .ToList();

                    return GetIncreasingRatio(values);
                }
            }

            public void Add(int rowIndex, NumericToken token)
            {
                _tokens.Add(new GeometryRadiusToken(rowIndex, token));
                CenterX = _tokens.Average(item => item.Token.Word.CenterX);
            }
        }

        private sealed class GeometryRadiusToken
        {
            public GeometryRadiusToken(int rowIndex, NumericToken token)
            {
                RowIndex = rowIndex;
                Token = token;
            }

            public int RowIndex { get; }
            public NumericToken Token { get; }
        }

        private sealed class GeometryHeaderRun
        {
            public GeometryHeaderRun(double boomLength, NumericToken firstToken)
            {
                BoomLength = boomLength;
                Add(firstToken);
            }

            public double BoomLength { get; }
            public List<NumericToken> Tokens { get; } = new();
            public double Left => Tokens.Count == 0 ? 0 : Tokens.Min(token => token.Word.CenterX);
            public double Right => Tokens.Count == 0 ? 0 : Tokens.Max(token => token.Word.CenterX);

            public void Add(NumericToken token)
            {
                Tokens.Add(token);
            }
        }

        private sealed class GeometryTableRegion
        {
            public GeometryTableRegion(
                GeometryRadiusColumn leftRadius,
                GeometryRadiusColumn rightRadius,
                List<GeometryHeaderRun> headerRuns)
            {
                LeftRadius = leftRadius;
                RightRadius = rightRadius;
                HeaderRuns = headerRuns;
            }

            public GeometryRadiusColumn LeftRadius { get; }
            public GeometryRadiusColumn RightRadius { get; }
            public List<GeometryHeaderRun> HeaderRuns { get; }
        }
        private static bool IsDuplicateCandidate(
            List<PdfTableCandidateDto> existingCandidates,
            PdfTableCandidateDto candidate)
        {
            string candidateSignature = BuildCandidateSignature(candidate);

            foreach (var existing in existingCandidates)
            {
                bool sameSignature = string.Equals(
                    BuildCandidateSignature(existing),
                    candidateSignature,
                    StringComparison.OrdinalIgnoreCase);

                if (sameSignature
                    && HasSubstantialOverlap(existing.Left, existing.Right, candidate.Left, candidate.Right)
                    && HasSubstantialOverlap(existing.Bottom, existing.Top, candidate.Bottom, candidate.Top))
                {
                    return true;
                }

                bool candidateIsWeakerContainedTable =
                    candidate.Score < existing.Score * 0.40
                    && candidate.BoomColumnCount <= 4
                    && HasSubstantialOverlap(existing.Left, existing.Right, candidate.Left, candidate.Right)
                    && HasSubstantialOverlap(existing.Bottom, existing.Top, candidate.Bottom, candidate.Top);

                if (candidateIsWeakerContainedTable)
                    return true;
            }

            return false;
        }

        private static bool HasSubstantialOverlap(double firstStart, double firstEnd, double secondStart, double secondEnd)
        {
            double firstMin = Math.Min(firstStart, firstEnd);
            double firstMax = Math.Max(firstStart, firstEnd);
            double secondMin = Math.Min(secondStart, secondEnd);
            double secondMax = Math.Max(secondStart, secondEnd);

            double overlap = Math.Min(firstMax, secondMax) - Math.Max(firstMin, secondMin);
            if (overlap <= 0)
                return false;

            double smallerSpan = Math.Min(firstMax - firstMin, secondMax - secondMin);
            if (smallerSpan <= 0)
                return false;

            return overlap / smallerSpan >= 0.70;
        }

        private static bool IsLikelyMetadataCandidate(PdfTableCandidateDto candidate)
        {
            var headerValues = GetCandidateHeaderValues(candidate);
            bool hasAngleOrRangeMarker = headerValues.Any(value => Math.Abs(value - 360) < 0.001 || value > 180);
            bool verySmallTable = candidate.BoomColumnCount <= 4
                && candidate.NonEmptyCapacityCellCount <= Math.Max(30, candidate.RadiusRowCount + 8);
            bool manySkippedValues = candidate.Warnings.Any(warning =>
                warning.Contains("outside the supported boom-column range", StringComparison.OrdinalIgnoreCase));

            return hasAngleOrRangeMarker && verySmallTable && manySkippedValues;
        }

        private static List<double> GetCandidateHeaderValues(PdfTableCandidateDto candidate)
        {
            var headerRow = candidate.RawGrid.FirstOrDefault();
            if (headerRow == null)
                return new List<double>();

            return headerRow
                .Skip(1)
                .Select(cell => TryExtractNumber(cell, out double value) ? value : double.NaN)
                .Where(value => !double.IsNaN(value))
                .ToList();
        }
        private static PdfTableCandidateDto ReindexCandidate(PdfTableCandidateDto candidate, int newIndex)
        {
            return new PdfTableCandidateDto
            {
                PageNumber = candidate.PageNumber,
                TableIndex = newIndex,
                Left = candidate.Left,
                Right = candidate.Right,
                Top = candidate.Top,
                Bottom = candidate.Bottom,
                BoomColumnCount = candidate.BoomColumnCount,
                RadiusRowCount = candidate.RadiusRowCount,
                NonEmptyCapacityCellCount = candidate.NonEmptyCapacityCellCount,
                Score = candidate.Score,
                ConfidenceLabel = candidate.ConfidenceLabel,
                Summary = candidate.Summary,
                RawGrid = candidate.RawGrid,
                ImportGrid = candidate.ImportGrid,
                Warnings = candidate.Warnings
            };
        }

        private static string BuildCandidateSignature(PdfTableCandidateDto candidate)
        {
            string headers = string.Join(",", candidate.RawGrid.FirstOrDefault() ?? new List<string>());
            string radii = string.Join(",", candidate.RawGrid.Skip(1).Take(12).Select(row => row.FirstOrDefault() ?? string.Empty));
            return $"{candidate.PageNumber}|{headers}|{radii}|{candidate.RadiusRowCount}|{candidate.BoomColumnCount}";
        }

        private static PdfWordInfo ToWordInfo(Word word)
        {
            string text = NormalizeWordText(word.Text);

            return new PdfWordInfo
            {
                Text = text,
                Left = word.BoundingBox.Left,
                Right = word.BoundingBox.Right,
                Bottom = word.BoundingBox.Bottom,
                Top = word.BoundingBox.Top
            };
        }

        private static List<List<PdfWordInfo>> ClusterRows(List<PdfWordInfo> words)
        {
            double medianHeight = Median(words.Select(word => word.Height).Where(value => value > 0).ToList());
            double tolerance = Math.Max(2.5, medianHeight * 0.75);

            var rows = new List<List<PdfWordInfo>>();

            foreach (var word in words.OrderByDescending(word => word.CenterY).ThenBy(word => word.Left))
            {
                int matchIndex = -1;
                double bestDistance = double.MaxValue;

                for (int i = 0; i < rows.Count; i++)
                {
                    double rowY = rows[i].Average(item => item.CenterY);
                    double distance = Math.Abs(rowY - word.CenterY);

                    if (distance <= tolerance && distance < bestDistance)
                    {
                        matchIndex = i;
                        bestDistance = distance;
                    }
                }

                if (matchIndex < 0)
                    rows.Add(new List<PdfWordInfo> { word });
                else
                    rows[matchIndex].Add(word);
            }

            foreach (var row in rows)
                row.Sort((a, b) => a.Left.CompareTo(b.Left));

            return rows
                .OrderByDescending(row => row.Average(word => word.CenterY))
                .ToList();
        }

        private static List<NumericToken> RemoveNearDuplicateNumbers(List<NumericToken> tokens)
        {
            var result = new List<NumericToken>();

            foreach (var token in tokens)
            {
                if (result.Any(existing =>
                        Math.Abs(existing.Value - token.Value) < 0.001 &&
                        Math.Abs(existing.Word.CenterX - token.Word.CenterX) < 5))
                    continue;

                result.Add(token);
            }

            return result;
        }

        private static List<NumericToken> GetNumericTokens(List<PdfWordInfo> row)
        {
            var result = new List<NumericToken>();

            foreach (var word in row)
            {
                if (TryExtractNumber(word.Text, out double value))
                    result.Add(new NumericToken(word, value));
            }

            return result;
        }

        private static bool TryExtractNumber(string? input, out double value)
        {
            value = 0;

            if (string.IsNullOrWhiteSpace(input))
                return false;

            string clean = input.Trim();
            clean = clean.Replace('\u00A0', ' ');
            clean = clean.Replace('\u2212', '-');
            clean = Regex.Replace(clean, @"(?i)\b(mm|cm|kg|tonnes?|tons?|ton|t|m|%)\b", string.Empty);
            clean = Regex.Replace(clean, @"[^\d,\.\-]", string.Empty);

            if (!Regex.IsMatch(clean, @"\d"))
                return false;

            if (clean.Count(character => character == '-') > 1)
                return false;

            if (clean.Contains(',') && clean.Contains('.'))
            {
                int lastComma = clean.LastIndexOf(',');
                int lastDot = clean.LastIndexOf('.');

                if (lastComma > lastDot)
                {
                    clean = clean.Replace(".", string.Empty);
                    clean = ReplaceLast(clean, ',', '.');
                    clean = clean.Replace(",", string.Empty);
                }
                else
                {
                    clean = clean.Replace(",", string.Empty);
                }
            }
            else if (clean.Contains(','))
            {
                int lastComma = clean.LastIndexOf(',');
                int digitsAfter = clean.Length - lastComma - 1;

                if (digitsAfter == 3 && clean.Count(character => character == ',') == 1)
                    clean = clean.Replace(",", string.Empty);
                else
                    clean = clean.Replace(",", ".");
            }
            else if (clean.Count(character => character == '.') > 1)
            {
                string[] parts = clean.Split('.');
                if (parts.Skip(1).All(part => part.Length == 3))
                    clean = clean.Replace(".", string.Empty);
            }

            return double.TryParse(clean, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
        }

        private static string NormalizeWordText(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return string.Empty;

            return text.Trim()
                .Replace('\u00A0', ' ')
                .Replace("\u2212", "-");
        }

        private static string FormatNumber(double value)
        {
            return value.ToString("0.###", CultureInfo.InvariantCulture);
        }

        private static bool AreClose(double a, double b)
        {
            return Math.Abs(a - b) <= 0.001;
        }

        private static double ScoreKeywords(List<PdfWordInfo> words)
        {
            string text = string.Join(" ", words.Select(word => word.Text)).ToLowerInvariant();
            double score = 0;

            string[] positiveKeywords =
            {
                "radius", "radii", "boom", "capacity", "load", "loads",
                "counterweight", "outrigger", "outriggers", "ton", "tons",
                "tonne", "tonnes", "lifting", "telescopic"
            };

            foreach (string keyword in positiveKeywords)
            {
                if (text.Contains(keyword))
                    score += 2.5;
            }

            return Math.Min(score, 20);
        }

        private static bool HasLoadChartKeywords(List<PdfWordInfo> words)
        {
            string text = string.Join(" ", words.Select(word => word.Text)).ToLowerInvariant();
            return text.Contains("radius")
                || text.Contains("boom")
                || text.Contains("capacity")
                || text.Contains("load");
        }

        private static double GetIncreasingRatio(List<double> values)
        {
            if (values.Count < 2)
                return 0;

            int increasing = 0;
            for (int i = 1; i < values.Count; i++)
            {
                if (values[i] > values[i - 1])
                    increasing++;
            }

            return (double)increasing / (values.Count - 1);
        }

        private static double GetRadiusIncreasingRatio(List<List<string>> rawGrid)
        {
            var radii = new List<double>();

            foreach (var row in rawGrid.Skip(1))
            {
                if (row.Count > 0 && TryExtractNumber(row[0], out double radius))
                    radii.Add(radius);
            }

            return GetIncreasingRatio(radii);
        }

        private static double GetTopRowsDensity(List<List<string>> rawGrid, int topRowCount)
        {
            if (rawGrid.Count <= 1 || topRowCount <= 0)
                return 0;

            int columnCount = rawGrid[0].Count - 1;
            if (columnCount <= 0)
                return 0;

            var rows = rawGrid.Skip(1).Take(topRowCount).ToList();
            int filled = rows.Sum(row => row.Skip(1).Count(cell => !string.IsNullOrWhiteSpace(cell)));

            return (double)filled / Math.Max(1, rows.Count * columnCount);
        }

        private static double GetColumnDensity(List<List<string>> rawGrid, int columnIndex)
        {
            if (rawGrid.Count <= 1)
                return 0;

            var rows = rawGrid.Skip(1).ToList();
            int filled = rows.Count(row => columnIndex < row.Count && !string.IsNullOrWhiteSpace(row[columnIndex]));

            return (double)filled / rows.Count;
        }

        private static double GetColumnCoverage(List<List<string>> rawGrid)
        {
            if (rawGrid.Count <= 1 || rawGrid[0].Count <= 1)
                return 0;

            int columnCount = rawGrid[0].Count - 1;
            int coveredColumns = 0;

            for (int column = 1; column <= columnCount; column++)
            {
                if (rawGrid.Skip(1).Any(row => column < row.Count && !string.IsNullOrWhiteSpace(row[column])))
                    coveredColumns++;
            }

            return (double)coveredColumns / columnCount;
        }

        private static double GetCapacityTrendRatio(List<List<string>> rawGrid)
        {
            if (rawGrid.Count <= 2)
                return 0.5;

            int columnCount = rawGrid[0].Count - 1;
            int totalPairs = 0;
            int decreasingPairs = 0;

            for (int column = 1; column <= columnCount; column++)
            {
                double? previous = null;

                foreach (var row in rawGrid.Skip(1))
                {
                    if (column >= row.Count)
                        continue;

                    if (!TryExtractNumber(row[column], out double value))
                        continue;

                    if (previous.HasValue)
                    {
                        totalPairs++;

                        if (value <= previous.Value + 0.001)
                            decreasingPairs++;
                    }

                    previous = value;
                }
            }

            if (totalPairs == 0)
                return 0.5;

            return (double)decreasingPairs / totalPairs;
        }

        private static double GetSpacingRegularity(List<double> values)
        {
            var sorted = values.OrderBy(value => value).ToList();
            var gaps = new List<double>();

            for (int i = 1; i < sorted.Count; i++)
            {
                double gap = sorted[i] - sorted[i - 1];
                if (gap > 0.1)
                    gaps.Add(gap);
            }

            if (gaps.Count < 2)
                return 0.5;

            double median = Median(gaps);
            if (median <= 0)
                return 0;

            double averageDeviation = gaps.Average(gap => Math.Abs(gap - median)) / median;
            return Math.Max(0, Math.Min(1, 1 - averageDeviation));
        }

        private static double GetMedianGap(List<double> values)
        {
            var sorted = values.OrderBy(value => value).ToList();
            var gaps = new List<double>();

            for (int i = 1; i < sorted.Count; i++)
            {
                double gap = sorted[i] - sorted[i - 1];
                if (gap > 0.1)
                    gaps.Add(gap);
            }

            return Median(gaps);
        }

        private static double Median(List<double> values)
        {
            if (values.Count == 0)
                return 0;

            var sorted = values.OrderBy(value => value).ToList();
            int middle = sorted.Count / 2;

            if (sorted.Count % 2 == 1)
                return sorted[middle];

            return (sorted[middle - 1] + sorted[middle]) / 2d;
        }

        private static string ReplaceLast(string value, char oldChar, char newChar)
        {
            int index = value.LastIndexOf(oldChar);
            if (index < 0)
                return value;

            char[] chars = value.ToCharArray();
            chars[index] = newChar;
            return new string(chars);
        }

        private static string GetConfidenceLabel(double score)
        {
            if (score >= 900)
                return "High confidence";

            if (score >= 560)
                return "Medium confidence";

            if (score >= 260)
                return "Low confidence";

            return "Very low confidence";
        }

        private sealed class PdfWordInfo
        {
            public string Text { get; init; } = string.Empty;
            public double Left { get; init; }
            public double Right { get; init; }
            public double Bottom { get; init; }
            public double Top { get; init; }
            public double CenterX => (Left + Right) / 2d;
            public double CenterY => (Bottom + Top) / 2d;
            public double Width => Math.Abs(Right - Left);
            public double Height => Math.Abs(Top - Bottom);
        }

        private sealed class HeaderCandidate
        {
            public HeaderCandidate(int rowIndex, List<PdfWordInfo> rowWords, List<NumericToken> numbers, double score)
            {
                RowIndex = rowIndex;
                RowWords = rowWords;
                Numbers = numbers;
                Score = score;
            }

            public int RowIndex { get; }
            public List<PdfWordInfo> RowWords { get; }
            public List<NumericToken> Numbers { get; }
            public double Score { get; }
        }

        private sealed class RowStreamData
        {
            public RowStreamData(
                double radius,
                NumericToken radiusToken,
                NumericToken? rightDuplicateRadiusToken,
                List<NumericToken> bodyNumbers,
                int startColumn,
                List<PdfWordInfo> originalRow)
            {
                Radius = radius;
                RadiusToken = radiusToken;
                RightDuplicateRadiusToken = rightDuplicateRadiusToken;
                BodyNumbers = bodyNumbers;
                StartColumn = startColumn;
                OriginalRow = originalRow;
            }

            public double Radius { get; }
            public NumericToken RadiusToken { get; }
            public NumericToken? RightDuplicateRadiusToken { get; }
            public List<NumericToken> BodyNumbers { get; }
            public int StartColumn { get; }
            public List<PdfWordInfo> OriginalRow { get; }
        }

        private sealed class CandidateGrid
        {
            public CandidateGrid(
                List<List<string>> rawGrid,
                List<double> headerValues,
                int columnCount,
                List<RowStreamData> usedRows,
                int nonEmptyCellCount,
                int skippedBodyValues,
                double density,
                double topRowsDensity,
                double firstColumnDensity,
                double columnCoverage,
                double radiusIncreasingRatio,
                double capacityTrendRatio,
                bool emptyTopLeftPenalty,
                double score)
            {
                RawGrid = rawGrid;
                HeaderValues = headerValues;
                ColumnCount = columnCount;
                UsedRows = usedRows;
                NonEmptyCellCount = nonEmptyCellCount;
                SkippedBodyValues = skippedBodyValues;
                Density = density;
                TopRowsDensity = topRowsDensity;
                FirstColumnDensity = firstColumnDensity;
                ColumnCoverage = columnCoverage;
                RadiusIncreasingRatio = radiusIncreasingRatio;
                CapacityTrendRatio = capacityTrendRatio;
                EmptyTopLeftPenalty = emptyTopLeftPenalty;
                Score = score;
            }

            public List<List<string>> RawGrid { get; }
            public List<double> HeaderValues { get; }
            public int ColumnCount { get; }
            public List<RowStreamData> UsedRows { get; }
            public int NonEmptyCellCount { get; }
            public int SkippedBodyValues { get; }
            public double Density { get; }
            public double TopRowsDensity { get; }
            public double FirstColumnDensity { get; }
            public double ColumnCoverage { get; }
            public double RadiusIncreasingRatio { get; }
            public double CapacityTrendRatio { get; }
            public bool EmptyTopLeftPenalty { get; }
            public double Score { get; }
            public int RadiusRowCount => RawGrid.Count - 1;
        }

        private readonly struct NumericToken
        {
            public NumericToken(PdfWordInfo word, double value)
            {
                Word = word;
                Value = value;
            }

            public PdfWordInfo Word { get; }
            public double Value { get; }
        }
    }
}
