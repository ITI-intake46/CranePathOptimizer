using CraneOptimizer.Domain.Entities;
using System.Data;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;

namespace CraneOptimizer.UI.Features.F4_LoadChart.Services
{
    internal sealed class ParsedLoadChart
    {
        public DataTable PreviewTable { get; init; } = new();
        public List<string> PreviewHeaders { get; init; } = new();
        public List<TargetPoint> Points { get; init; } = new();
        public double MinRadius { get; init; }
        public double MaxRadius { get; init; }
        public double MinBoomLength { get; init; }
        public double MaxBoomLength { get; init; }
        public double MaxCapacity { get; init; }
    }

    internal static class LoadChartCsvService
    {
        public static ParsedLoadChart ParseCsv(string csvFilePath)
        {
            if (!File.Exists(csvFilePath))
                throw new FileNotFoundException("CSV file not found.", csvFilePath);

            var rawGrid = ReadCsvAsRawGrid(csvFilePath, out _);

            if (rawGrid.Count < 2)
                throw new FormatException("CSV must contain a header row and at least one data row.");

            return ParseRawGrid(rawGrid);
        }

        public static ParsedLoadChart ParseRawGrid(IReadOnlyList<List<string>> rawGrid)
        {
            if (rawGrid.Count < 2)
                throw new FormatException("The load chart grid must contain a header row and at least one data row.");

            var previewTable = CreatePreviewTable(rawGrid, out var previewHeaders);
            return ParseTable(previewTable, previewHeaders);
        }

        public static ParsedLoadChart ParseTable(DataTable table, IReadOnlyList<string>? previewHeaders = null)
        {
            if (table.Columns.Count < 2)
                throw new FormatException("The load chart grid must contain a radius column and at least one boom column.");

            var boomLengths = new List<double>();
            for (int columnIndex = 1; columnIndex < table.Columns.Count; columnIndex++)
            {
                string rawHeader = previewHeaders != null && columnIndex < previewHeaders.Count
                    ? previewHeaders[columnIndex]
                    : table.Columns[columnIndex].ColumnName;
                string headerText = NormalizeGridNumber(rawHeader);
                if (!TryParseFlexibleNumber(headerText, out double boomLength))
                    throw new FormatException($"Column '{rawHeader}' is not a valid boom length.");

                boomLengths.Add(boomLength);
            }

            var points = new List<TargetPoint>();
            double? minRadius = null;
            double? maxRadius = null;
            double? minBoom = null;
            double? maxBoom = null;
            double? maxCapacity = null;

            foreach (DataRow row in table.Rows)
            {
                string radiusText = NormalizeGridNumber(row[0]?.ToString());
                if (string.IsNullOrWhiteSpace(radiusText))
                    continue;

                if (!TryParseFlexibleNumber(radiusText, out double radius))
                    throw new FormatException($"Radius value '{radiusText}' is invalid.");

                minRadius = !minRadius.HasValue || radius < minRadius.Value ? radius : minRadius;
                maxRadius = !maxRadius.HasValue || radius > maxRadius.Value ? radius : maxRadius;

                for (int i = 0; i < boomLengths.Count; i++)
                {
                    double boomLength = boomLengths[i];
                    string capacityText = NormalizeGridNumber(row[i + 1]?.ToString());
                    double capacity = 0d;

                    if (!string.IsNullOrWhiteSpace(capacityText))
                    {
                        if (TryParseFlexibleNumber(capacityText, out capacity))
                        {
                            maxCapacity = !maxCapacity.HasValue || capacity > maxCapacity.Value ? capacity : maxCapacity;
                        }
                        else
                        {
                            throw new FormatException(
                                $"Capacity value '{capacityText}' is invalid at radius '{radiusText}', boom '{boomLength}'.");
                        }
                    }

                    minBoom = !minBoom.HasValue || boomLength < minBoom.Value ? boomLength : minBoom;
                    maxBoom = !maxBoom.HasValue || boomLength > maxBoom.Value ? boomLength : maxBoom;

                    points.Add(new TargetPoint
                    {
                        Radius = radius,
                        BoomLength = boomLength,
                        Capacity = capacity
                    });
                }
            }

            if (points.Count == 0)
                throw new FormatException("The grid does not contain any load chart points.");

            return new ParsedLoadChart
            {
                PreviewTable = table.Copy(),
                PreviewHeaders = previewHeaders?.ToList()
                    ?? table.Columns.Cast<DataColumn>().Select(column => column.ColumnName).ToList(),
                Points = points,
                MinRadius = minRadius ?? 0d,
                MaxRadius = maxRadius ?? 0d,
                MinBoomLength = minBoom ?? 0d,
                MaxBoomLength = maxBoom ?? 0d,
                MaxCapacity = maxCapacity ?? 0d
            };
        }

        public static ParsedLoadChart TransposePreview(DataTable source, IReadOnlyList<string> previewHeaders)
        {
            var rawGrid = ToRawGrid(source, previewHeaders);
            var transposedGrid = TransposeRawGrid(rawGrid);
            return ParseRawGrid(transposedGrid);
        }

        internal static bool TryParseFlexibleNumber(string? input, out double value)
        {
            value = 0d;
            string normalized = NormalizeFlexibleNumber(input);
            return !string.IsNullOrWhiteSpace(normalized)
                && double.TryParse(normalized, NumberStyles.Any, CultureInfo.InvariantCulture, out value);
        }

        private static DataTable CreatePreviewTable(IReadOnlyList<List<string>> rawGrid, out List<string> previewHeaders)
        {
            var table = new DataTable();
            previewHeaders = new List<string>();
            if (rawGrid.Count == 0)
                return table;

            int columnCount = rawGrid[0].Count;

            for (int columnIndex = 0; columnIndex < columnCount; columnIndex++)
            {
                string rawHeader = rawGrid[0][columnIndex].Trim();
                if (string.IsNullOrWhiteSpace(rawHeader))
                    rawHeader = columnIndex == 0 ? "Radius (m)" : $"Col{columnIndex}";

                string displayHeader = Regex.Replace(rawHeader, @"_\d+$", string.Empty);
                if (columnIndex > 0 && TryParseFlexibleNumber(displayHeader, out double headerNumber))
                    displayHeader = headerNumber.ToString("F1", CultureInfo.InvariantCulture);

                previewHeaders.Add(displayHeader);
                table.Columns.Add($"C{columnIndex}", typeof(string));
            }

            for (int rowIndex = 1; rowIndex < rawGrid.Count; rowIndex++)
            {
                var row = table.NewRow();
                var sourceRow = rawGrid[rowIndex];

                for (int columnIndex = 0; columnIndex < columnCount; columnIndex++)
                    row[columnIndex] = columnIndex < sourceRow.Count ? sourceRow[columnIndex] : string.Empty;

                table.Rows.Add(row);
            }

            return table;
        }

        private static List<List<string>> ToRawGrid(DataTable source, IReadOnlyList<string> previewHeaders)
        {
            var rawGrid = new List<List<string>>
            {
                previewHeaders.Count > 0
                    ? previewHeaders.ToList()
                    : source.Columns.Cast<DataColumn>().Select(column => column.ColumnName).ToList()
            };

            foreach (DataRow row in source.Rows)
            {
                var values = new List<string>();
                for (int columnIndex = 0; columnIndex < source.Columns.Count; columnIndex++)
                    values.Add(row[columnIndex]?.ToString() ?? string.Empty);

                rawGrid.Add(values);
            }

            return rawGrid;
        }

        private static List<List<string>> TransposeRawGrid(IReadOnlyList<List<string>> source)
        {
            int maxColumns = source.Max(row => row.Count);
            var result = new List<List<string>>();

            for (int columnIndex = 0; columnIndex < maxColumns; columnIndex++)
            {
                var row = new List<string>();
                for (int rowIndex = 0; rowIndex < source.Count; rowIndex++)
                    row.Add(columnIndex < source[rowIndex].Count ? source[rowIndex][columnIndex] : string.Empty);

                result.Add(row);
            }

            return result;
        }

        private static List<List<string>> ReadCsvAsRawGrid(string path, out List<string> warnings)
        {
            warnings = new List<string>();
            var grid = new List<List<string>>();

            string content;
            try
            {
                content = File.ReadAllText(path, System.Text.Encoding.UTF8);
            }
            catch
            {
                content = File.ReadAllText(path, System.Text.Encoding.Default);
            }

            content = content.TrimStart('\uFEFF');

            char separator = DetectCsvSeparator(content);
            if (separator == ';')
                warnings.Add("Detected semicolon-separated CSV (European Excel format) and converted it automatically.");

            var logicalRows = ParseCsvLogicalRows(content, separator)
                .Where(row => row.Count > 0
                    && !row[0].TrimStart().StartsWith("#")
                    && row.Any(cell => !string.IsNullOrWhiteSpace(cell)))
                .ToList();

            if (logicalRows.Count < 2)
                return grid;

            bool isEuropean = separator == ';' || DetectEuropeanDecimal(logicalRows[0]);
            if (isEuropean && separator != ';')
                warnings.Add("Detected European decimal format and converted it automatically.");

            bool row0HasNumbers = logicalRows[0].Skip(1)
                .Any(cell => TryParseFlexibleNumber(NormalizeCsvNumber(cell, isEuropean), out _));

            if (!row0HasNumbers && logicalRows.Count >= 2)
            {
                warnings.Add("Detected a two-row header and used the second row as boom lengths.");
                logicalRows.RemoveAt(0);
            }

            var headerCells = logicalRows[0];
            var boomColumns = new List<(int Index, string BoomLength)>();
            for (int i = 1; i < headerCells.Count; i++)
            {
                string header = NormalizeCsvNumber(headerCells[i], isEuropean);
                if (!string.IsNullOrEmpty(header) && TryParseFlexibleNumber(header, out _))
                    boomColumns.Add((i, header));
            }

            if (boomColumns.Count == 0)
                return grid;

            var seenBooms = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            var deduplicatedBoomColumns = new List<(int Index, string DisplayName)>();

            foreach (var (index, boomLength) in boomColumns)
            {
                if (seenBooms.TryGetValue(boomLength, out int count))
                {
                    seenBooms[boomLength] = count + 1;
                    deduplicatedBoomColumns.Add((index, $"{boomLength}_{count + 1}"));
                }
                else
                {
                    seenBooms[boomLength] = 1;
                    deduplicatedBoomColumns.Add((index, boomLength));
                }
            }

            grid.Add(new List<string> { "Radius (m)" }
                .Concat(deduplicatedBoomColumns.Select(item => item.DisplayName))
                .ToList());

            for (int rowIndex = 1; rowIndex < logicalRows.Count; rowIndex++)
            {
                foreach (var explodedRow in ExplodeMultiLineCells(logicalRows[rowIndex]))
                {
                    string radius = explodedRow.Count > 0
                        ? NormalizeCsvNumber(explodedRow[0], isEuropean)
                        : string.Empty;

                    if (!TryParseFlexibleNumber(radius, out _))
                        continue;

                    var dataRow = new List<string> { radius };
                    foreach (var (index, _) in deduplicatedBoomColumns)
                    {
                        string value = index < explodedRow.Count
                            ? NormalizeCsvNumber(explodedRow[index], isEuropean)
                            : string.Empty;
                        dataRow.Add(value);
                    }

                    grid.Add(dataRow);
                }
            }

            return grid;
        }

        private static char DetectCsvSeparator(string content)
        {
            int semicolons = 0;
            int commas = 0;
            int linesChecked = 0;

            foreach (string line in content.Split('\n').Take(20))
            {
                string trimmed = line.Trim();
                if (string.IsNullOrEmpty(trimmed) || trimmed.StartsWith("#"))
                    continue;

                bool inQuotes = false;
                foreach (char character in trimmed)
                {
                    if (character == '"')
                    {
                        inQuotes = !inQuotes;
                        continue;
                    }

                    if (inQuotes)
                        continue;

                    if (character == ';')
                        semicolons++;
                    else if (character == ',')
                        commas++;
                }

                linesChecked++;
                if (linesChecked >= 5)
                    break;
            }

            return semicolons > 0 && semicolons >= commas / 2 ? ';' : ',';
        }

        private static List<List<string>> ParseCsvLogicalRows(string content, char separator)
        {
            var rows = new List<List<string>>();
            var currentRow = new List<string>();
            var cell = new System.Text.StringBuilder();
            bool inQuotes = false;
            int index = 0;

            while (index < content.Length)
            {
                char character = content[index];

                if (character == '"')
                {
                    if (inQuotes && index + 1 < content.Length && content[index + 1] == '"')
                    {
                        cell.Append('"');
                        index += 2;
                        continue;
                    }

                    inQuotes = !inQuotes;
                    index++;
                }
                else if (character == separator && !inQuotes)
                {
                    currentRow.Add(cell.ToString());
                    cell.Clear();
                    index++;
                }
                else if (!inQuotes && (character == '\r' || character == '\n'))
                {
                    currentRow.Add(cell.ToString());
                    cell.Clear();

                    if (currentRow.Any(value => !string.IsNullOrWhiteSpace(value)))
                        rows.Add(currentRow);

                    currentRow = new List<string>();

                    if (character == '\r' && index + 1 < content.Length && content[index + 1] == '\n')
                        index++;

                    index++;
                }
                else
                {
                    cell.Append(character);
                    index++;
                }
            }

            currentRow.Add(cell.ToString());
            if (currentRow.Any(value => !string.IsNullOrWhiteSpace(value)))
                rows.Add(currentRow);

            return rows;
        }

        private static List<List<string>> ExplodeMultiLineCells(List<string> row)
        {
            int maxLines = row.Max(cell => cell.Split('\n').Length);
            if (maxLines <= 1)
                return new() { row };

            var result = new List<List<string>>();
            for (int lineIndex = 0; lineIndex < maxLines; lineIndex++)
            {
                var newRow = new List<string>();
                for (int columnIndex = 0; columnIndex < row.Count; columnIndex++)
                {
                    string cell = row[columnIndex];
                    string[] parts = cell.Split('\n');
                    if (parts.Length == 1 && ShouldAlignSingleLineCellToLastLine(row, columnIndex, maxLines))
                    {
                        newRow.Add(lineIndex == maxLines - 1 ? parts[0].Trim() : string.Empty);
                    }
                    else
                    {
                        newRow.Add(lineIndex < parts.Length ? parts[lineIndex].Trim() : string.Empty);
                    }
                }

                result.Add(newRow);
            }

            return result;
        }

        private static bool ShouldAlignSingleLineCellToLastLine(List<string> row, int columnIndex, int maxLines)
        {
            if (columnIndex <= 1 || columnIndex >= row.Count - 1)
                return false;

            if (string.IsNullOrWhiteSpace(row[columnIndex]))
                return false;

            if (row[columnIndex].Split('\n').Length != 1)
                return false;

            return row
                .Take(columnIndex)
                .Skip(1)
                .Any(cell => cell.Split('\n').Length == maxLines && !string.IsNullOrWhiteSpace(cell));
        }

        private static bool DetectEuropeanDecimal(List<string> headerRow)
        {
            return headerRow.Skip(1).Any(header => Regex.IsMatch(header, @"\d+,\d"));
        }

        private static string NormalizeCsvNumber(string value, bool europeanFormat)
        {
            if (string.IsNullOrWhiteSpace(value))
                return string.Empty;

            string clean = value.Trim().TrimEnd('m', ' ').Trim();
            if (europeanFormat)
            {
                clean = Regex.Replace(clean, @"(\d),(\d)", "$1.$2");
                clean = Regex.Replace(clean, @"(\d)\.(\d{3})(?!\d*\.)", "$1$2");
            }

            return clean;
        }

        private static string NormalizeFlexibleNumber(string? input)
        {
            if (string.IsNullOrWhiteSpace(input))
                return string.Empty;

            string clean = input.Trim().Trim('"').Trim();
            clean = Regex.Replace(clean, @"_\d+$", string.Empty);
            clean = clean.Replace('\u00A0', ' ');
            clean = Regex.Replace(clean, @"(?i)\s*(mm|cm|kg|tonnes?|tons?|ton|t|m|%)\s*$", string.Empty);
            clean = clean.Replace(" ", string.Empty);

            if (clean.Contains(',') && !clean.Contains('.'))
            {
                int lastComma = clean.LastIndexOf(',');
                int decimalDigits = clean.Length - lastComma - 1;

                if (decimalDigits == 3 && clean.Count(character => character == ',') == 1)
                    clean = clean.Replace(",", string.Empty);
                else
                    clean = clean.Replace(",", ".");
            }
            else if (clean.Contains('.') && !clean.Contains(','))
            {
                string[] parts = clean.Split('.');
                bool looksLikeThousands = parts.Length > 1
                    && parts.Skip(1).All(part => part.Length == 3);

                if (looksLikeThousands)
                    clean = clean.Replace(".", string.Empty);
            }
            else if (clean.Contains(',') && clean.Contains('.'))
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

            return clean.Trim();
        }

        private static string NormalizeGridNumber(string? value)
        {
            return NormalizeFlexibleNumber(value);
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
    }
}
