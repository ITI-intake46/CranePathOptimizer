using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using WPF_IFC_test1.Database.Entities;

namespace WPF_IFC_test1.Database.Services
{
    /// <summary>
    /// Reads and writes crane load chart CSV files.
    ///
    /// Expected CSV format:
    ///   - Row 1 (header): "Radius" , 13.2 , 17.7 , 22.2 , ...   ← boom lengths
    ///   - Row 2+        :   3      , 200  , 133.8, 124.4, ...   ← capacities at each radius
    ///   - Empty cell    = not rated (null in DB)
    ///
    /// Example:
    ///   Radius,13.2,17.7,22.2,26.7,31.3
    ///   3,200,133.8,124.4,120.5,
    ///   3.5,142,131.3,125.5,120.7,
    ///   4,133,123.6,123.6,119,112.8
    /// </summary>
    public static class LoadChartCsvService
    {
        // ─────────────────────────────────────────────────────────────────────
        //  IMPORT  (CSV → entities)
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Parses a CSV file and returns (chart, points) entities ready to save.
        /// Throws a descriptive exception if the format is wrong.
        /// </summary>
        public static (CraneLoadChartEntity chart,
                        List<CraneLoadChartPointEntity> points)
            ImportFromCsv(string csvFilePath,
                          string craneModel = "",
                          string manufacturer = "",
                          string configName = "",
                          string chartType = "360",
                          string outriggerConfig = "")
        {
            if (!File.Exists(csvFilePath))
                throw new FileNotFoundException("CSV file not found", csvFilePath);

            string[] lines = File.ReadAllLines(csvFilePath, Encoding.UTF8);

            // Remove blank lines and comment lines (starting with #)
            lines = lines
                .Where(l => !string.IsNullOrWhiteSpace(l) && !l.TrimStart().StartsWith("#"))
                .ToArray();

            if (lines.Length < 2)
                throw new FormatException(
                    "CSV must have at least 2 rows: a header row (boom lengths) and one data row.");

            // ── Auto-detect separator and number format ───────────────────
            char sep = DetectSeparator(lines);
            string[] headerCells = SplitCsvLine(lines[0], sep);

            // Detect European decimal: semicolon files always use comma as decimal;
            // comma files may also have European decimals in header cells like "13,1"
            bool isEuropean = sep == ';'
                || headerCells.Skip(1).Any(h =>
                    System.Text.RegularExpressions.Regex.IsMatch(h, @"\d+,\d"));

            // ── Parse header row — boom lengths ───────────────────────────────

            // First cell should be "Radius", "r", "m" or similar — skip it
            // Remaining cells are boom lengths
            var boomLengths = new List<double>();

            for (int i = 1; i < headerCells.Length; i++)
            {
                string cell = NormalizeNumber(headerCells[i], isEuropean);
                if (string.IsNullOrEmpty(cell)) continue;

                if (!double.TryParse(cell, NumberStyles.Any, CultureInfo.InvariantCulture, out double boom))
                    throw new FormatException(
                        $"Header cell [{i}] is not a valid number: \"{headerCells[i].Trim()}\"\n" +
                        "The header row must be: Radius, 13.2, 17.7, 22.2, ...");

                boomLengths.Add(boom);
            }

            if (boomLengths.Count == 0)
                throw new FormatException("No boom lengths found in header row.");

            // ── Parse data rows — radius + capacities ─────────────────────────
            var points = new List<CraneLoadChartPointEntity>();
            int rowErrors = 0;

            for (int lineIdx = 1; lineIdx < lines.Length; lineIdx++)
            {
                string[] cells = SplitCsvLine(lines[lineIdx], sep);

                if (cells.Length == 0 || string.IsNullOrWhiteSpace(cells[0]))
                    continue;

                // First cell = radius in metres
                string radiusStr = NormalizeNumber(cells[0], isEuropean);
                if (!double.TryParse(radiusStr, NumberStyles.Any,
                        CultureInfo.InvariantCulture, out double radius))
                {
                    rowErrors++;
                    continue; // skip bad row
                }

                for (int i = 0; i < boomLengths.Count; i++)
                {
                    double? capacity = null;

                    int cellIdx = i + 1;
                    if (cellIdx < cells.Length)
                    {
                        string cell = NormalizeNumber(cells[cellIdx], isEuropean);
                        if (!string.IsNullOrEmpty(cell) &&
                            double.TryParse(cell, NumberStyles.Any,
                                CultureInfo.InvariantCulture, out double cap))
                        {
                            capacity = cap;
                        }
                    }
                    // if cell is missing or blank → capacity stays null (not rated)

                    points.Add(new CraneLoadChartPointEntity
                    {
                        RadiusM = radius,
                        BoomLengthM = boomLengths[i],
                        CapacityTonne = capacity
                    });
                }
            }

            if (points.Count == 0)
                throw new FormatException("No valid data rows found in CSV.");

            // Try to guess crane model from filename if not provided
            // e.g.  "LTM1200-5.1_360_full.csv"  →  "LTM1200-5.1"
            if (string.IsNullOrEmpty(craneModel))
                craneModel = GuessModelFromFilename(csvFilePath) ?? "";

            if (string.IsNullOrEmpty(manufacturer))
                manufacturer = GuessManufacturerFromModel(craneModel);

            var chart = new CraneLoadChartEntity
            {
                CraneModel = craneModel,
                Manufacturer = manufacturer,
                ConfigName = configName,
                ChartType = chartType,
                OutriggerConfig = outriggerConfig,
                SourceImagePath = csvFilePath,
                ImportMethod = "CSV",
                ImportedAt = DateTime.UtcNow,
                Notes = rowErrors > 0 ? $"{rowErrors} rows skipped (parse error)" : ""
            };

            return (chart, points);
        }

        // ─────────────────────────────────────────────────────────────────────
        //  EXPORT  (entities → CSV)
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Writes a load chart to a CSV file.
        /// Can be called automatically after AI extraction — zero user effort.
        /// </summary>
        public static void ExportToCsv(
            CraneLoadChartEntity chart,
            List<CraneLoadChartPointEntity> points,
            string outputPath)
        {
            if (points.Count == 0)
                throw new ArgumentException("No points to export.");

            // Get unique sorted boom lengths and radii
            var booms = points.Select(p => p.BoomLengthM).Distinct().OrderBy(x => x).ToList();
            var radii = points.Select(p => p.RadiusM).Distinct().OrderBy(x => x).ToList();

            var sb = new StringBuilder();

            // ── Comment header ────────────────────────────────────────────────
            sb.AppendLine($"# Crane Load Chart — {chart.CraneModel} — {chart.ConfigName}");
            sb.AppendLine($"# Manufacturer: {chart.Manufacturer}");
            sb.AppendLine($"# Chart Type: {chart.ChartType}  |  Outrigger: {chart.OutriggerConfig}");
            sb.AppendLine($"# Exported: {DateTime.Now:yyyy-MM-dd HH:mm}");
            sb.AppendLine($"# Capacity values in TONNES. Empty cell = not rated.");
            sb.AppendLine("#");

            // ── Header row: Radius, boom1, boom2, ... ─────────────────────────
            sb.Append("Radius (m)");
            foreach (double boom in booms)
                sb.Append($",{boom.ToString(CultureInfo.InvariantCulture)}m");
            sb.AppendLine();

            // ── Data rows ─────────────────────────────────────────────────────
            // BUG FIX: The original used .ToDictionary() directly, which throws
            // InvalidOperationException when duplicate (RadiusM, BoomLengthM) pairs
            // exist in the points list (e.g. imported from multiple sources or
            // re-imported without cleaning). The import path uses GroupBy to handle
            // duplicates safely — ExportToCsv must do the same.
            // Use the same rounding strategy as CraneLoadChartLookup.GetCapacity
            // and take the LAST entry per key (most recently added wins).
            var lookup = points
                .Where(p => p.CapacityTonne.HasValue)
                .GroupBy(p => (Math.Round(p.RadiusM, 4), Math.Round(p.BoomLengthM, 4)))
                .ToDictionary(g => g.Key, g => g.Last().CapacityTonne!.Value);

            foreach (double radius in radii)
            {
                sb.Append(radius.ToString(CultureInfo.InvariantCulture));

                foreach (double boom in booms)
                {
                    sb.Append(',');
                    var key = (Math.Round(radius, 4), Math.Round(boom, 4));
                    if (lookup.TryGetValue(key, out double cap))
                        sb.Append(cap.ToString(CultureInfo.InvariantCulture));
                    // else empty cell = not rated
                }

                sb.AppendLine();
            }

            // Ensure output directory exists
            string? dir = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);

            File.WriteAllText(outputPath, sb.ToString(), Encoding.UTF8);
        }

        /// <summary>
        /// Generates a sensible default file path for auto-saving.
        /// e.g.  C:\Users\...\Documents\CraneCharts\LTM1200-5.1_360_full.csv
        /// </summary>
        public static string GetDefaultExportPath(CraneLoadChartEntity chart)
        {
            string docsFolder = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            string chartsDir = Path.Combine(docsFolder, "CraneCharts");

            // Build a safe filename from chart metadata
            string model = SanitizeFilename(chart.CraneModel) ?? "Unknown";
            string type = SanitizeFilename(chart.ChartType) ?? "360";
            string config = SanitizeFilename(chart.OutriggerConfig) ?? "";

            string filename = string.IsNullOrEmpty(config)
                ? $"{model}_{type}.csv"
                : $"{model}_{type}_{config}.csv";

            return Path.Combine(chartsDir, filename);
        }

        // ─────────────────────────────────────────────────────────────────────
        //  VALIDATE  (check before importing)
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Quick validation — returns (isValid, errorMessage).
        /// Use this to give user feedback before attempting import.
        /// </summary>
        public static (bool isValid, string message) ValidateCsv(string csvFilePath)
        {
            try
            {
                if (!File.Exists(csvFilePath))
                    return (false, "File not found.");

                string[] lines = File.ReadAllLines(csvFilePath)
                    .Where(l => !string.IsNullOrWhiteSpace(l) && !l.TrimStart().StartsWith("#"))
                    .ToArray();

                if (lines.Length < 2)
                    return (false, "File needs at least 2 rows (header + 1 data row).");

                char sep = DetectSeparator(lines);
                string[] header = SplitCsvLine(lines[0], sep);
                if (header.Length < 2)
                    return (false, "Header row needs at least 2 columns.");

                bool isEuropean = sep == ';'
                    || header.Skip(1).Any(h =>
                        System.Text.RegularExpressions.Regex.IsMatch(h, @"\d+,\d"));

                // Check first data row has a parseable radius
                string[] firstData = SplitCsvLine(lines[1], sep);
                string firstCell = NormalizeNumber(firstData[0], isEuropean);
                if (!double.TryParse(firstCell, NumberStyles.Any,
                        CultureInfo.InvariantCulture, out _))
                    return (false, $"First data cell \"{firstData[0]}\" is not a number (should be radius in metres).");

                int boomCount = header.Length - 1;
                int dataRows = lines.Length - 1;

                return (true, $"✓ Valid — {boomCount} boom lengths × {dataRows} radius rows = up to {boomCount * dataRows} data points");
            }
            catch (Exception ex)
            {
                return (false, $"Could not read file: {ex.Message}");
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Private helpers
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Auto-detects whether the CSV uses comma or semicolon as the field separator.
        /// European Excel (Windows, regional settings) exports with semicolons.
        /// </summary>
        private static char DetectSeparator(string[] lines)
        {
            int semicolons = 0, commas = 0;
            int checked_ = 0;

            foreach (string line in lines.Take(10))
            {
                string t = line.Trim();
                if (string.IsNullOrEmpty(t) || t.StartsWith("#")) continue;
                bool inQ = false;
                foreach (char c in t)
                {
                    if (c == '"') { inQ = !inQ; continue; }
                    if (inQ) continue;
                    if (c == ';') semicolons++;
                    else if (c == ',') commas++;
                }
                if (++checked_ >= 5) break;
            }

            return (semicolons > 0 && semicolons >= commas / 2) ? ';' : ',';
        }

        /// <summary>Splits a CSV line respecting quoted fields. Supports comma or semicolon separators.</summary>
        private static string[] SplitCsvLine(string line, char separator = ',')
        {
            var result = new List<string>();
            bool inQuotes = false;
            var current = new StringBuilder();

            foreach (char c in line)
            {
                if (c == '"')
                {
                    inQuotes = !inQuotes;
                }
                else if (c == separator && !inQuotes)
                {
                    result.Add(current.ToString());
                    current.Clear();
                }
                else
                {
                    current.Append(c);
                }
            }
            result.Add(current.ToString());
            return result.ToArray();
        }

        /// <summary>
        /// Strips "m" suffix and converts European comma-decimal to dot-decimal.
        /// e.g. "13,1 m" → "13.1",  "42,2" → "42.2",  "86" → "86"
        /// </summary>
        private static string NormalizeNumber(string value, bool europeanFormat)
        {
            if (string.IsNullOrWhiteSpace(value)) return "";
            string clean = value.Trim().TrimEnd('m', ' ').Trim();
            if (europeanFormat)
                clean = System.Text.RegularExpressions.Regex.Replace(
                    clean, @"(\d),(\d)", "$1.$2");
            return clean;
        }

        public static string? GuessModelFromFilename(string path)
        {
            string name = Path.GetFileNameWithoutExtension(path);
            // Strip common suffixes like _360, _full, _partial
            string[] tokens = name.Split('_');
            return tokens.Length > 0 ? tokens[0].Trim() : null;
        }

        /// <summary>
        /// Guesses the manufacturer from the model string using a prefix lookup table.
        /// Returns empty string if not recognised.
        /// </summary>
        public static string GuessManufacturerFromModel(string? model)
            => CraneLoadChartLookup.GuessManufacturer(model ?? "");

        private static string? SanitizeFilename(string? input)
        {
            if (string.IsNullOrWhiteSpace(input)) return null;
            foreach (char c in Path.GetInvalidFileNameChars())
                input = input.Replace(c, '_');
            return input.Trim().Replace(' ', '_');
        }
    }
}