using CraneOptimizer.UI.Features.F4_LoadChart.Views;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;

namespace CraneOptimizer.UI.Features.F4_LoadChart.Services
{
    internal sealed class LoadChartImportResult
    {
        public ParsedLoadChart Chart { get; init; } = new();
        public ParsedLoadChart? ImportableEnvelopeChart { get; init; }
        public string SourceKind { get; init; } = "Unknown";
        public string SourceDescription { get; init; } = string.Empty;
        public LoadChartPdfMetadataDto DetectedMetadata { get; init; } = LoadChartPdfMetadataDto.Empty;
        public List<string> Warnings { get; init; } = new();
        public List<List<string>> ExactVisualPreviewGrid { get; init; } = new();
        public string ExactVisualPreviewTitle { get; init; } = string.Empty;
        public string ExactVisualPreviewDescription { get; init; } = string.Empty;
        public bool HasExactVisualPreview => ExactVisualPreviewGrid.Count > 1 && ExactVisualPreviewGrid[0].Count > 1;

        public string BuildStatusMessage(string fileName)
        {
            string description = string.IsNullOrWhiteSpace(SourceDescription)
                ? string.Empty
                : $" ({SourceDescription})";

            if (string.Equals(SourceKind, "PDF", StringComparison.OrdinalIgnoreCase) && Chart.Points.Count > 0)
            {
                int radiusCount = Chart.Points
                    .Select(point => point.Radius)
                    .Distinct()
                    .Count();

                int boomCount = Chart.Points
                    .Select(point => point.BoomLength)
                    .Distinct()
                    .Count();

                int actionableNoteCount = Warnings.Count(IsActionableWarning);
                string noteText = actionableNoteCount == 0
                    ? string.Empty
                    : $" {actionableNoteCount} import note(s).";

                string exactText = Chart.IsExactVisualChart
                    ? " exact visual capacity cells"
                    : " capacity points";

                string capacityText = Chart.IsCapacityUnitResolved
                    ? $"normalized max {Chart.MaxCapacity:0.###} t"
                    : $"raw max {Chart.RawMaxCapacity:0.###} (capacity unit unresolved)";

                return $"Loaded {fileName}{description}: {Chart.Points.Count}{exactText}, {radiusCount} radii, {boomCount} numeric boom lengths, {capacityText}.{noteText}";
            }

            string warningText = Warnings.Count == 0
                ? string.Empty
                : $" {Warnings.Count} warning(s).";

            return $"Loaded {fileName}{description}.{warningText}";
        }

        public string BuildWarningsMessage()
        {
            if (Warnings.Count == 0)
                return string.Empty;

            var displayed = Warnings.Take(12).ToList();
            if (Warnings.Count > displayed.Count)
            {
                string suffix = string.Equals(SourceKind, "PDF", StringComparison.OrdinalIgnoreCase)
                    ? "more import note(s)."
                    : "more warning(s).";

                displayed.Add($"...and {Warnings.Count - displayed.Count} {suffix}");
            }

            return string.Join(Environment.NewLine, displayed);
        }

        public bool HasActionableWarnings()
            => Warnings.Any(IsActionableWarning);

        private static bool IsActionableWarning(string warning)
        {
            if (string.IsNullOrWhiteSpace(warning))
                return false;

            string[] informationalPrefixes =
            {
                "PDF import used page",
                "Imported ",
                "Detected ",
                "Row-stream extractor",
                "X positions were used",
                "Parsed visual PDF rows",
                "Column alignment was inferred",
                "Nearby load-chart keywords",
                "No load-chart keywords"
            };

            return !informationalPrefixes.Any(prefix =>
                warning.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
        }
    }

    internal static class LoadChartImportService
    {
        public static LoadChartImportResult Import(string path, Window? owner = null)
        {
            if (string.IsNullOrWhiteSpace(path))
                throw new ArgumentException("Import path is empty.", nameof(path));

            if (!File.Exists(path))
                throw new FileNotFoundException("Load chart file not found.", path);

            string extension = Path.GetExtension(path).ToLowerInvariant();

            return extension switch
            {
                ".csv" => new LoadChartImportResult
                {
                    Chart = LoadChartCsvService.ParseCsv(path),
                    SourceKind = "CSV",
                    SourceDescription = "CSV"
                },
                ".pdf" => ImportPdfWithSelection(path, owner),
                _ => throw new NotSupportedException("Only CSV and text-based PDF load chart files are supported.")
            };
        }

        private static LoadChartImportResult ImportPdfWithSelection(string path, Window? owner)
        {
            var scanResult = LoadChartPdfService.ScanPdf(path);

            if (owner == null)
            {
                var bestTable = scanResult.GetBestTable()
                    ?? throw new FormatException("No load-chart table candidates were detected in the selected PDF.");

                return LoadChartPdfService.ImportSelectedTable(scanResult, bestTable);
            }

            var dialog = new PdfLoadChartImportDialog(scanResult)
            {
                Owner = owner
            };

            bool? result = dialog.ShowDialog();

            if (result != true || dialog.ImportResult == null)
                throw new OperationCanceledException("PDF load chart import was cancelled.");

            return dialog.ImportResult;
        }
    }
}