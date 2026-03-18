using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;

namespace WPF_IFC_test1.Database.Services
{
    /// <summary>
    /// Extracts raw table data from a digital PDF load chart.
    /// Uses PdfPig (free, no internet, no API cost).
    ///
    ///  NuGet:  Install-Package PdfPig
    ///
    /// SAFETY DESIGN:
    ///   Returns RawLoadChartGrid (string table) — NOT database entities.
    ///   The caller shows this to the user in a verification grid so they
    ///   can correct any misread numbers BEFORE anything is saved.
    ///
    /// Works with:  digital PDFs (text is selectable/copyable in Acrobat)
    /// Will fail:   scanned/image PDFs — use CSV import for those
    /// </summary>
    public static class LoadChartPdfExtractor
    {
        // ─────────────────────────────────────────────────────────────────────
        //  Result type
        // ─────────────────────────────────────────────────────────────────────

        public class RawLoadChartGrid
        {
            /// <summary>
            /// The extracted table as raw strings.
            /// grid[0]    = header row: { "Radius", "13.2", "17.7", "22.2", ... }
            /// grid[1..n] = data rows:  { "3",      "200",  "133.8", "",    ... }
            /// Empty string = cell was blank (not rated).
            /// </summary>
            public List<List<string>> Cells { get; set; } = new();

            /// <summary>Auto-detected crane model (may be empty).</summary>
            public string DetectedCraneModel { get; set; } = "";

            /// <summary>Which PDF page was used (1-based).</summary>
            public int PageUsed    { get; set; }
            public int TotalPages  { get; set; }

            /// <summary>
            /// Warnings about possible extraction errors.
            /// Shown to the user in the verification grid so they know what to check.
            /// </summary>
            public List<string> Warnings { get; set; } = new();
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Public entry point
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Opens the PDF and extracts the load chart table as raw strings.
        /// pageNumber = 0 → auto-detect the page with the most numbers.
        /// </summary>
        public static RawLoadChartGrid ExtractRawGrid(string pdfPath,
                                                       int pageNumber = 0)
        {
            if (!File.Exists(pdfPath))
                throw new FileNotFoundException("PDF file not found", pdfPath);

            using var doc = PdfDocument.Open(pdfPath);
            int totalPages = doc.NumberOfPages;

            int targetPage = pageNumber > 0 && pageNumber <= totalPages
                ? pageNumber
                : FindBestPageNumber(doc);

            if (targetPage < 1)
                throw new InvalidOperationException(
                    "Could not find a page with a load chart table.\n" +
                    "Try specifying the page number manually.");

            var page  = doc.GetPage(targetPage);
            var words = page.GetWords().ToList();

            string detectedModel = TryDetectCraneModel(words);
            var (grid, warnings) = BuildGrid(words);

            if (grid.Count < 2)
                throw new InvalidOperationException(
                    "Could not extract a table from this PDF page.\n\n" +
                    "This usually means the PDF is a scanned image — the text\n" +
                    "is not machine-readable. In that case:\n" +
                    "  1. Open the PDF in Excel (File → Open) — Excel can often\n" +
                    "     extract the table automatically.\n" +
                    "  2. Save as CSV and use the CSV import.\n" +
                    "  3. Or manually fill in the CSV template.");

            return new RawLoadChartGrid
            {
                Cells              = grid,
                DetectedCraneModel = detectedModel,
                PageUsed           = targetPage,
                TotalPages         = totalPages,
                Warnings           = warnings
            };
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Core grid extraction
        // ─────────────────────────────────────────────────────────────────────

        private static (List<List<string>> grid, List<string> warnings)
            BuildGrid(List<Word> words)
        {
            var warnings = new List<string>();

            // Collect numeric tokens with their positions
            var tokens = new List<(double x, double y, string text)>();
            foreach (var word in words)
            {
                string clean = word.Text.Trim().TrimEnd('m', 't', ' ');
                if (double.TryParse(clean, NumberStyles.Any,
                        CultureInfo.InvariantCulture, out double val)
                    && val > 0 && val < 10_000)
                {
                    tokens.Add((
                        Math.Round(word.BoundingBox.Left,   1),
                        Math.Round(word.BoundingBox.Bottom, 1),
                        clean
                    ));
                }
            }

            if (tokens.Count < 4)
                return (new List<List<string>>(), new List<string> { "Too few numbers found on this page." });

            // Group tokens into rows by Y coordinate
            tokens.Sort((a, b) => b.y.CompareTo(a.y)); // top of page first

            const double rowTol = 5.5;
            var rawRows = new List<List<(double x, string text)>>();
            var cur     = new List<(double x, string text)> { (tokens[0].x, tokens[0].text) };
            double rowY = tokens[0].y;

            for (int i = 1; i < tokens.Count; i++)
            {
                var t = tokens[i];
                if (Math.Abs(t.y - rowY) <= rowTol)
                {
                    cur.Add((t.x, t.text));
                }
                else
                {
                    cur.Sort((a, b) => a.x.CompareTo(b.x));
                    rawRows.Add(new List<(double x, string text)>(cur));
                    cur.Clear();
                    cur.Add((t.x, t.text));
                    rowY = t.y;
                }
            }
            if (cur.Count > 0) { cur.Sort((a, b) => a.x.CompareTo(b.x)); rawRows.Add(cur); }

            // Find the header row (most values in boom length range 10–100 m)
            int headerIdx    = -1;
            int maxBoomCount = 0;
            for (int i = 0; i < Math.Min(rawRows.Count, 6); i++)
            {
                int n = rawRows[i].Count(c =>
                    double.TryParse(c.text, NumberStyles.Any,
                        CultureInfo.InvariantCulture, out double v) && v >= 10 && v <= 100);
                if (n > maxBoomCount) { maxBoomCount = n; headerIdx = i; }
            }

            if (headerIdx < 0 || maxBoomCount < 2)
                return (new List<List<string>>(), new List<string> { "Could not identify a boom length header row." });

            // Get boom column X positions from the header
            var boomCols = rawRows[headerIdx]
                .Where(c => double.TryParse(c.text, NumberStyles.Any,
                    CultureInfo.InvariantCulture, out double v) && v >= 10 && v <= 100)
                .OrderBy(c => c.x)
                .ToList();

            var colXs = boomCols.Select(c => c.x).ToList();
            const double colTol = 14.0;

            // Build aligned grid
            var grid = new List<List<string>>();

            // Header row
            var headerRow = new List<string> { "Radius" };
            headerRow.AddRange(boomCols.Select(c => c.text));
            grid.Add(headerRow);

            // Data rows
            for (int i = headerIdx + 1; i < rawRows.Count; i++)
            {
                var row = rawRows[i];
                if (row.Count == 0) continue;

                // First cell must be a plausible radius (2–60 m)
                if (!double.TryParse(row[0].text, NumberStyles.Any,
                        CultureInfo.InvariantCulture, out double radius)
                    || radius < 2 || radius > 60)
                    continue;

                // Align remaining cells to boom columns
                var dataRow = new List<string>(Enumerable.Repeat("", boomCols.Count + 1));
                dataRow[0] = row[0].text;

                foreach (var cell in row.Skip(1))
                {
                    int col = FindNearestColumn(cell.x, colXs, colTol);
                    if (col >= 0) dataRow[col + 1] = cell.text;
                }
                grid.Add(dataRow);
            }

            // Produce warnings about suspicious results
            AddSanityWarnings(grid, warnings);

            return (grid, warnings);
        }

        private static void AddSanityWarnings(List<List<string>> grid, List<string> warnings)
        {
            if (grid.Count < 3) return;
            int dataRows = grid.Count - 1;

            for (int col = 1; col < grid[0].Count; col++)
            {
                // Warn about very sparse columns
                int filled = grid.Skip(1).Count(r => col < r.Count && !string.IsNullOrEmpty(r[col]));
                if (filled < 2 && dataRows > 4)
                    warnings.Add($"⚠  Column \"{grid[0][col]}m\" has only {filled} value(s) — possible column misalignment.");

                // Warn about large jumps between consecutive rows in same column
                double? prev = null;
                for (int row = 1; row < grid.Count; row++)
                {
                    if (col >= grid[row].Count || string.IsNullOrEmpty(grid[row][col]))
                    { prev = null; continue; }

                    if (!double.TryParse(grid[row][col], NumberStyles.Any,
                            CultureInfo.InvariantCulture, out double val)) continue;

                    if (prev.HasValue && prev.Value > 0)
                    {
                        double ratio = val / prev.Value;
                        if (ratio > 4.0 || ratio < 0.05)
                            warnings.Add(
                                $"⚠  Suspicious jump at radius {grid[row][0]}m / boom {grid[0][col]}m: " +
                                $"{prev.Value} → {val}. Please verify.");
                    }
                    prev = val;
                }
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Helpers
        // ─────────────────────────────────────────────────────────────────────

        private static int FindNearestColumn(double x, List<double> xs, double tol)
        {
            int best = -1; double bestD = tol;
            for (int i = 0; i < xs.Count; i++)
            {
                double d = Math.Abs(x - xs[i]);
                if (d < bestD) { bestD = d; best = i; }
            }
            return best;
        }

        private static int FindBestPageNumber(PdfDocument doc)
        {
            int best = -1, maxN = 0;
            for (int p = 1; p <= doc.NumberOfPages; p++)
            {
                int n = doc.GetPage(p).GetWords()
                    .Count(w => double.TryParse(w.Text.Trim().TrimEnd('m'),
                        NumberStyles.Any, CultureInfo.InvariantCulture, out _));
                if (n > maxN) { maxN = n; best = p; }
            }
            return best;
        }

        private static string TryDetectCraneModel(List<Word> words)
        {
            var re = new System.Text.RegularExpressions.Regex(
                @"\b(LTM|LTC|LTT|LG|LTR|MK|AC|ATF|GMK|TM|CC|CTL|SL|SCC|STC|GRT|TMS|WL)\s*\d{2,4}",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            foreach (var w in words)
                if (re.IsMatch(w.Text)) return w.Text.Trim();
            return "";
        }

        /// <summary>
        /// Guesses the manufacturer from the detected model string.
        /// </summary>
        public static string GuessManufacturerFromModel(string model)
            => CraneLoadChartLookup.GuessManufacturer(model);
    }
}
