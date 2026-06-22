using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.Json;
using CraneOptimizer.UI.Features.F4_LoadChart.Services;
using Xunit;

namespace CraneOptimizer.Application.Tests.LoadChart
{
    public sealed class PdfCorpusRegressionGateTests
    {
        [Fact]
        public void FullCorpusExtractionGate_WithGolden100TonsAndStructuralAudit()
        {
            string corpusDir = Environment.GetEnvironmentVariable("CRANE_PDF_CORPUS_DIR") ?? string.Empty;
            string auditDir = Environment.GetEnvironmentVariable("CRANE_PDF_CORPUS_AUDIT_DIR") ?? string.Empty;

            if (string.IsNullOrWhiteSpace(corpusDir) || string.IsNullOrWhiteSpace(auditDir))
            {
                // The corpus PDFs are intentionally not committed to the repository. Normal test runs pass unless
                // the gate is explicitly activated by the installer/CI with these environment variables.
                return;
            }

            Assert.True(Directory.Exists(corpusDir), "CRANE_PDF_CORPUS_DIR does not exist: " + corpusDir);
            Directory.CreateDirectory(auditDir);

            var pdfPaths = Directory.GetFiles(corpusDir, "*.pdf")
                .OrderBy(path => Path.GetFileName(path), StringComparer.OrdinalIgnoreCase)
                .ToList();

            Assert.NotEmpty(pdfPaths);

            var failures = new List<string>();
            var summaries = new List<object>();

            foreach (string pdfPath in pdfPaths)
            {
                string fileName = Path.GetFileName(pdfPath);

                try
                {
                    object scan = LoadChartPdfService.ScanPdf(pdfPath);
                    object? selected = InvokeGetBestTable(scan);
                    object? import = TryImportSelected(scan, selected, out string? importError);

                    WriteJson(Path.Combine(auditDir, Sanitize(fileName) + ".full-scan.audit.json"), ProjectScan(scan));
                    WriteJson(Path.Combine(auditDir, Sanitize(fileName) + ".selected.audit.json"), selected == null ? new { FileName = fileName, Selected = (object?)null } : ProjectTable(selected));

                    ValidateGenericPdf(fileName, scan, selected, importError, failures);

                    if (fileName.Equals("100Tons.pdf", StringComparison.OrdinalIgnoreCase))
                    {
                        Validate100TonsGolden(scan, selected, failures, auditDir);
                    }

                    summaries.Add(BuildPdfSummary(fileName, pdfPath, scan, selected, import, importError));
                }
                catch (Exception ex)
                {
                    failures.Add(fileName + ": extractor threw " + ex);
                    summaries.Add(new
                    {
                        FileName = fileName,
                        Status = "FAILED",
                        Error = ex.ToString()
                    });
                }
            }

            WriteJson(Path.Combine(auditDir, "_corpus_summary.audit.json"), summaries);
            WriteCsv(Path.Combine(auditDir, "_corpus_summary.audit.csv"), summaries);
            WriteJson(Path.Combine(auditDir, "_corpus_failures.audit.json"), failures);

            Assert.True(failures.Count == 0, "Corpus regression gate failed:" + Environment.NewLine + string.Join(Environment.NewLine, failures));
        }

        private static void ValidateGenericPdf(string fileName, object scan, object? selected, string? importError, List<string> failures)
        {
            int pageCount = Int(scan, "PageCount");
            int allTables = AsObjects(Get(scan, "AllTables")).Count();
            int pageCandidates = AsObjects(Get(scan, "Pages")).Count();

            if (pageCount <= 0)
                failures.Add(fileName + ": page count is zero.");

            if (pageCandidates <= 0)
                failures.Add(fileName + ": no page candidates were returned.");

            if (allTables <= 0)
                failures.Add(fileName + ": no table candidates were returned.");

            if (selected == null)
            {
                failures.Add(fileName + ": no selected/best table was returned.");
                return;
            }

            int columns = Int(selected, "BoomColumnCount");
            int radiusRows = Int(selected, "RadiusRowCount");
            int cells = Int(selected, "NonEmptyCapacityCellCount");
            var rawGrid = Grid(selected, "RawGrid");
            var headers = HeaderValues(selected);
            var lineVector = ExtractLineVector(selected);
            bool hasLineRow = HasLineRow(selected);

            if (columns <= 0)
                failures.Add(fileName + ": selected table has no boom columns.");

            if (radiusRows <= 0)
                failures.Add(fileName + ": selected table has no radius rows.");

            if (cells <= 0)
                failures.Add(fileName + ": selected table has no numeric capacity cells.");

            if (rawGrid.Count == 0)
                failures.Add(fileName + ": selected table has an empty raw grid.");

            if (headers.Count > 0 && columns > 0 && headers.Count < Math.Min(columns, 3))
                failures.Add(fileName + ": selected table has too few numeric boom headers. Headers=" + string.Join(",", headers));

            if (hasLineRow && columns > 0 && lineVector.Length != columns)
            {
                failures.Add(fileName + ": Number-of-lines vector is not expanded to all visual boom columns. Columns=" + columns + ", LineVector=" + string.Join(",", lineVector));
            }

            if (!string.IsNullOrWhiteSpace(importError))
                failures.Add(fileName + ": ImportSelectedTable failed: " + importError);
        }

        private static void Validate100TonsGolden(object scan, object? selected, List<string> failures, string auditDir)
        {
            var pages = AsObjects(Get(scan, "Pages")).ToList();
            object page17 = SinglePage(pages, 17, failures);
            object page18 = SinglePage(pages, 18, failures);
            object page19 = SinglePage(pages, 19, failures);

            WriteJson(Path.Combine(auditDir, "100Tons.page17.golden-audit.json"), ProjectPage(page17));
            WriteJson(Path.Combine(auditDir, "100Tons.page18.golden-audit.json"), ProjectPage(page18));
            WriteJson(Path.Combine(auditDir, "100Tons.page19.golden-audit.json"), ProjectPage(page19));

            if (selected == null)
            {
                failures.Add("100Tons.pdf: no selected table available for golden validation.");
                return;
            }

            // Full-PDF selection is intentionally not forced to Page 19. The extractor may
            // legitimately select a later same-family continuation chart as the global best
            // table for 100Tons.pdf. The Page 17/18/19 defect is validated below using
            // page-specific golden candidates. The global best table is still checked by
            // ValidateGenericPdf for structural validity and importability.
            if (Int(selected, "BoomColumnCount") <= 0 || Int(selected, "RadiusRowCount") <= 0 || Int(selected, "NonEmptyCapacityCellCount") <= 0)
            {
                failures.Add("100Tons.pdf: global selected candidate is structurally invalid. Page "
                    + Int(selected, "PageNumber")
                    + ", Columns " + Int(selected, "BoomColumnCount")
                    + ", RadiusRows " + Int(selected, "RadiusRowCount")
                    + ", Cells " + Int(selected, "NonEmptyCapacityCellCount")
                    + ", Combined=" + IsCombinedContinuationCandidate(selected));
            }

            var page19Combined = Tables(page19).Where(IsCombinedContinuationCandidate).Where(t => Int(t, "BoomColumnCount") == 30).OrderByDescending(t => Int(t, "NonEmptyCapacityCellCount")).FirstOrDefault();
            if (page19Combined == null)
            {
                failures.Add("100Tons.pdf: Page 19 missing 30-column combined continuation candidate.");
            }
            else
            {
                ExpectEqual("100Tons.pdf Page 19 numeric cells", 430, Int(page19Combined, "NonEmptyCapacityCellCount"), failures);
                ExpectSequence("100Tons.pdf Page 19 Number-of-lines", new[] { 12, 11, 11, 11, 11, 9, 9, 9, 9, 8, 8, 8, 8, 8, 6, 6, 6, 6, 6, 5, 5, 5, 5, 5, 5, 4, 4, 4, 3, 3 }, ExtractLineVector(page19Combined), failures);
            }

            var page18Combined = Tables(page18)
                .Where(IsCombinedContinuationCandidate)
                .Where(t => Int(t, "BoomColumnCount") == 29)
                .OrderByDescending(t => Int(t, "NonEmptyCapacityCellCount"))
                .FirstOrDefault();

            if (page18Combined == null)
            {
                failures.Add("100Tons.pdf: Page 18 missing 29-column combined continuation candidate.");
            }
            else
            {
                ExpectEqual("100Tons.pdf Page 18 numeric cells", 493, Int(page18Combined, "NonEmptyCapacityCellCount"), failures);
                var headers = HeaderValues(page18Combined);
                if (headers.Count != 29)
                    failures.Add("100Tons.pdf Page 18: expected 29 boom headers but got " + headers.Count + ".");
                if (headers.Count > 0 && Math.Abs(headers[0] - 30.50) > 0.001)
                    failures.Add("100Tons.pdf Page 18: first header should be 30.50, not " + headers[0].ToString(CultureInfo.InvariantCulture) + ".");
                if (headers.Take(Math.Min(5, headers.Count)).Any(v => Math.Abs(v - 4.5) < 0.001))
                    failures.Add("100Tons.pdf Page 18: body/radius value 4.5 was promoted into headers.");
                ExpectSequence("100Tons.pdf Page 18 Number-of-lines", new[] { 6, 6, 6, 6, 5, 5, 5, 5, 5, 5, 4, 4, 4, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 2, 2, 2, 2 }, ExtractLineVector(page18Combined), failures);
            }

            var page17Tables = Tables(page17).ToList();
            if (page17Tables.Any(IsCombinedContinuationCandidate))
                failures.Add("100Tons.pdf Page 17: left/right charts must not be combined because counterweights differ.");

            object? page17Left = page17Tables
                .Where(t => Int(t, "BoomColumnCount") == 14)
                .Where(t => Int(t, "NonEmptyCapacityCellCount") == 242)
                .Where(t => WarningContains(t, "COUNTERWEIGHT") && WarningContains(t, "16.5"))
                .OrderByDescending(t => Double(t, "Score"))
                .FirstOrDefault();

            object? page17Right = page17Tables
                .Where(t => Int(t, "BoomColumnCount") == 15)
                .Where(t => Int(t, "NonEmptyCapacityCellCount") == 190)
                .Where(t => WarningContains(t, "COUNTERWEIGHT") && WarningContains(t, "23.5"))
                .OrderByDescending(t => Double(t, "Score"))
                .FirstOrDefault();

            if (page17Left == null)
            {
                failures.Add("100Tons.pdf Page 17: missing left 16.5T 14-column chart.");
            }
            else
            {
                ExpectSequence("100Tons.pdf Page 17 left Number-of-lines", new[] { 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 2, 2, 2, 2 }, ExtractLineVector(page17Left), failures);
            }

            if (page17Right == null)
            {
                failures.Add("100Tons.pdf Page 17: missing right 23.5T 15-column chart.");
            }
            else
            {
                ExpectSequence("100Tons.pdf Page 17 right Number-of-lines", new[] { 12, 11, 11, 11, 11, 9, 9, 9, 9, 8, 8, 8, 8, 8, 6 }, ExtractLineVector(page17Right), failures);
            }
        }

        private static void ExpectEqual(string label, int expected, int actual, List<string> failures)
        {
            if (expected != actual)
                failures.Add(label + ": expected " + expected + " but got " + actual + ".");
        }

        private static void ExpectSequence(string label, int[] expected, int[] actual, List<string> failures)
        {
            if (!expected.SequenceEqual(actual))
                failures.Add(label + ": expected [" + string.Join(",", expected) + "] but got [" + string.Join(",", actual) + "].");
        }

        private static object BuildPdfSummary(string fileName, string pdfPath, object scan, object? selected, object? import, string? importError)
        {
            return new
            {
                FileName = fileName,
                PdfPath = pdfPath,
                PageCount = Int(scan, "PageCount"),
                PageCandidates = AsObjects(Get(scan, "Pages")).Count(),
                TableCandidates = AsObjects(Get(scan, "AllTables")).Count(),
                Selected = selected == null ? null : new
                {
                    PageNumber = Int(selected, "PageNumber"),
                    TableIndex = Int(selected, "TableIndex"),
                    BoomColumnCount = Int(selected, "BoomColumnCount"),
                    RadiusRowCount = Int(selected, "RadiusRowCount"),
                    NonEmptyCapacityCellCount = Int(selected, "NonEmptyCapacityCellCount"),
                    Score = Double(selected, "Score"),
                    IsCombinedContinuationCandidate = IsCombinedContinuationCandidate(selected),
                    CandidateKindLabel = String(selected, "CandidateKindLabel"),
                    Summary = String(selected, "Summary"),
                    Headers = HeaderValues(selected),
                    LineVector = ExtractLineVector(selected),
                    Warnings = StringList(Get(selected, "Warnings"))
                },
                ImportSucceeded = import != null && string.IsNullOrWhiteSpace(importError),
                ImportError = importError,
                Import = import == null ? null : ProjectImport(import)
            };
        }

        private static object ProjectScan(object scan)
        {
            return new
            {
                PdfPath = String(scan, "PdfPath"),
                PageCount = Int(scan, "PageCount"),
                Warnings = StringList(Get(scan, "Warnings")),
                Pages = AsObjects(Get(scan, "Pages")).Select(ProjectPage).ToList(),
                AllTables = AsObjects(Get(scan, "AllTables")).Select(ProjectTable).ToList()
            };
        }

        private static object ProjectPage(object page)
        {
            return new
            {
                PageNumber = Int(page, "PageNumber"),
                WordCount = Int(page, "WordCount"),
                NumericTokenCount = Int(page, "NumericTokenCount"),
                Score = Double(page, "Score"),
                ConfidenceLabel = String(page, "ConfidenceLabel"),
                Summary = String(page, "Summary"),
                TextPreview = String(page, "TextPreview"),
                Tables = Tables(page).Select(ProjectTable).ToList()
            };
        }

        private static object ProjectTable(object table)
        {
            return new
            {
                PageNumber = Int(table, "PageNumber"),
                TableIndex = Int(table, "TableIndex"),
                Left = Double(table, "Left"),
                Right = Double(table, "Right"),
                Top = Double(table, "Top"),
                Bottom = Double(table, "Bottom"),
                BoomColumnCount = Int(table, "BoomColumnCount"),
                RadiusRowCount = Int(table, "RadiusRowCount"),
                LineRequirementRowCount = Int(table, "LineRequirementRowCount"),
                NonEmptyCapacityCellCount = Int(table, "NonEmptyCapacityCellCount"),
                Score = Double(table, "Score"),
                ConfidenceLabel = String(table, "ConfidenceLabel"),
                CandidateKindLabel = String(table, "CandidateKindLabel"),
                Summary = String(table, "Summary"),
                DisplayTitle = String(table, "DisplayTitle"),
                DisplayDetails = String(table, "DisplayDetails"),
                IsEnvelopeCandidate = Bool(table, "IsEnvelopeCandidate"),
                IsCombinedContinuationCandidate = IsCombinedContinuationCandidate(table),
                IsEnvelopeReferenceFragment = Bool(table, "IsEnvelopeReferenceFragment"),
                Warnings = StringList(Get(table, "Warnings")),
                HeaderValues = HeaderValues(table),
                LineVector = ExtractLineVector(table),
                RawGrid = Grid(table, "RawGrid"),
                ImportGrid = Grid(table, "ImportGrid")
            };
        }

        private static object? ProjectImport(object import)
        {
            object? chart = Get(import, "Chart");
            object? envelope = Get(import, "ImportableEnvelopeChart");
            return new
            {
                SourceKind = String(import, "SourceKind"),
                SourceDescription = String(import, "SourceDescription"),
                Warnings = StringList(Get(import, "Warnings")),
                ExactVisualPreviewTitle = String(import, "ExactVisualPreviewTitle"),
                ExactVisualPreviewDescription = String(import, "ExactVisualPreviewDescription"),
                Chart = chart == null ? null : ProjectChart(chart),
                ImportableEnvelopeChart = envelope == null ? null : ProjectChart(envelope)
            };
        }

        private static object ProjectChart(object chart)
        {
            return new
            {
                MinRadius = Double(chart, "MinRadius"),
                MaxRadius = Double(chart, "MaxRadius"),
                MinBoomLength = Double(chart, "MinBoomLength"),
                MaxBoomLength = Double(chart, "MaxBoomLength"),
                MaxCapacity = Double(chart, "MaxCapacity"),
                RawMaxCapacity = Double(chart, "RawMaxCapacity"),
                EffectiveCapacityUnit = String(chart, "EffectiveCapacityUnit"),
                IsExactVisualChart = Bool(chart, "IsExactVisualChart"),
                PreviewHeaders = StringList(Get(chart, "PreviewHeaders")),
                VisualSummary = Get(chart, "VisualSummary"),
                PointCount = AsObjects(Get(chart, "Points")).Count()
            };
        }

        private static object SinglePage(IEnumerable<object> pages, int pageNumber, List<string> failures)
        {
            var matches = pages.Where(page => Int(page, "PageNumber") == pageNumber).ToList();
            if (matches.Count != 1)
            {
                failures.Add("Expected exactly one page candidate for page " + pageNumber + " but found " + matches.Count + ".");
                return matches.FirstOrDefault() ?? new object();
            }
            return matches[0];
        }

        private static IEnumerable<object> Tables(object page)
        {
            return AsObjects(Get(page, "Tables"));
        }

        private static object? InvokeGetBestTable(object scan)
        {
            var method = scan.GetType().GetMethod("GetBestTable", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, binder: null, types: Type.EmptyTypes, modifiers: null);
            return method == null ? null : method.Invoke(scan, Array.Empty<object>());
        }

        private static object? TryImportSelected(object scan, object? selected, out string? importError)
        {
            importError = null;
            if (selected == null)
                return null;

            try
            {
                var method = typeof(LoadChartPdfService).GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
                    .Where(m => m.Name == "ImportSelectedTable")
                    .Where(m => m.GetParameters().Length == 2)
                    .FirstOrDefault();

                if (method == null)
                {
                    importError = "ImportSelectedTable(scan, selected) method was not found.";
                    return null;
                }

                return method.Invoke(null, new[] { scan, selected });
            }
            catch (TargetInvocationException ex)
            {
                importError = ex.InnerException?.ToString() ?? ex.ToString();
                return null;
            }
            catch (Exception ex)
            {
                importError = ex.ToString();
                return null;
            }
        }

        private static bool IsCombinedContinuationCandidate(object table)
        {
            return Bool(table, "IsCombinedContinuationCandidate");
        }

        private static bool WarningContains(object table, string text)
        {
            return StringList(Get(table, "Warnings")).Any(warning => warning.IndexOf(text, StringComparison.OrdinalIgnoreCase) >= 0);
        }

        private static List<double> HeaderValues(object table)
        {
            var grid = Grid(table, "RawGrid");
            if (grid.Count == 0)
                return new List<double>();

            return grid[0]
                .Where(cell => !string.IsNullOrWhiteSpace(cell))
                .Select(cell => TryParseDouble(cell, out double value) ? value : double.NaN)
                .Where(value => !double.IsNaN(value))
                .ToList();
        }

        private static bool HasLineRow(object table)
        {
            return Grid(table, "RawGrid").Any(row => row.Count > 0 && row[0].IndexOf("line", StringComparison.OrdinalIgnoreCase) >= 0);
        }

        private static int[] ExtractLineVector(object table)
        {
            var grid = Grid(table, "RawGrid");
            var lineRow = grid.FirstOrDefault(row => row.Count > 0 && row[0].IndexOf("line", StringComparison.OrdinalIgnoreCase) >= 0);
            if (lineRow == null)
                return Array.Empty<int>();

            return lineRow
                .Skip(1)
                .Where(cell => !string.IsNullOrWhiteSpace(cell))
                .Select(cell => TryParseDouble(cell, out double value) ? (int)Math.Round(value) : int.MinValue)
                .Where(value => value != int.MinValue)
                .ToArray();
        }

        private static object? Get(object obj, string propertyName)
        {
            if (obj == null)
                return null;

            var property = obj.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            return property == null ? null : property.GetValue(obj);
        }

        private static int Int(object obj, string propertyName)
        {
            object? value = Get(obj, propertyName);
            if (value == null)
                return 0;
            return Convert.ToInt32(value, CultureInfo.InvariantCulture);
        }

        private static double Double(object obj, string propertyName)
        {
            object? value = Get(obj, propertyName);
            if (value == null)
                return 0;
            return Convert.ToDouble(value, CultureInfo.InvariantCulture);
        }

        private static bool Bool(object obj, string propertyName)
        {
            object? value = Get(obj, propertyName);
            return value is bool b && b;
        }

        private static string String(object obj, string propertyName)
        {
            return Get(obj, propertyName)?.ToString() ?? string.Empty;
        }

        private static IEnumerable<object> AsObjects(object? value)
        {
            if (value == null || value is string)
                yield break;

            if (value is IEnumerable enumerable)
            {
                foreach (object? item in enumerable)
                {
                    if (item != null)
                        yield return item;
                }
            }
        }

        private static List<string> StringList(object? value)
        {
            return AsObjects(value).Select(item => item.ToString() ?? string.Empty).ToList();
        }

        private static List<List<string>> Grid(object obj, string propertyName)
        {
            var rows = new List<List<string>>();
            object? value = Get(obj, propertyName);
            if (value == null || value is string)
                return rows;

            if (value is IEnumerable rowEnumerable)
            {
                foreach (object? rowObj in rowEnumerable)
                {
                    var row = new List<string>();
                    if (rowObj is IEnumerable cellEnumerable && rowObj is not string)
                    {
                        foreach (object? cell in cellEnumerable)
                            row.Add(cell?.ToString() ?? string.Empty);
                    }
                    rows.Add(row);
                }
            }

            return rows;
        }

        private static bool TryParseDouble(string value, out double result)
        {
            string normalized = (value ?? string.Empty).Trim().Replace(",", string.Empty);
            return double.TryParse(normalized, NumberStyles.Float, CultureInfo.InvariantCulture, out result);
        }

        private static void WriteJson(string path, object value)
        {
            var options = new JsonSerializerOptions { WriteIndented = true };
            File.WriteAllText(path, JsonSerializer.Serialize(value, options));
        }

        private static void WriteCsv(string path, List<object> summaries)
        {
            var lines = new List<string>
            {
                "FileName,PageCount,PageCandidates,TableCandidates,SelectedPage,SelectedTable,SelectedBoomColumns,SelectedRadiusRows,SelectedNumericCells,SelectedCombined,ImportSucceeded,ImportError"
            };

            foreach (object item in summaries)
            {
                object? selected = Get(item, "Selected");
                lines.Add(string.Join(",", new[]
                {
                    Csv(String(item, "FileName")),
                    Int(item, "PageCount").ToString(CultureInfo.InvariantCulture),
                    Int(item, "PageCandidates").ToString(CultureInfo.InvariantCulture),
                    Int(item, "TableCandidates").ToString(CultureInfo.InvariantCulture),
                    selected == null ? "" : Int(selected, "PageNumber").ToString(CultureInfo.InvariantCulture),
                    selected == null ? "" : Int(selected, "TableIndex").ToString(CultureInfo.InvariantCulture),
                    selected == null ? "" : Int(selected, "BoomColumnCount").ToString(CultureInfo.InvariantCulture),
                    selected == null ? "" : Int(selected, "RadiusRowCount").ToString(CultureInfo.InvariantCulture),
                    selected == null ? "" : Int(selected, "NonEmptyCapacityCellCount").ToString(CultureInfo.InvariantCulture),
                    selected == null ? "" : Bool(selected, "IsCombinedContinuationCandidate").ToString(CultureInfo.InvariantCulture),
                    Bool(item, "ImportSucceeded").ToString(CultureInfo.InvariantCulture),
                    Csv(String(item, "ImportError"))
                }));
            }

            File.WriteAllLines(path, lines, Encoding.UTF8);
        }

        private static string Csv(string value)
        {
            value = value ?? string.Empty;
            if (value.Contains(",") || value.Contains("\"") || value.Contains("\n") || value.Contains("\r"))
                return "\"" + value.Replace("\"", "\"\"") + "\"";
            return value;
        }

        private static string Sanitize(string value)
        {
            var invalid = Path.GetInvalidFileNameChars();
            return new string(value.Select(ch => invalid.Contains(ch) ? '_' : ch).ToArray());
        }
    }
}
