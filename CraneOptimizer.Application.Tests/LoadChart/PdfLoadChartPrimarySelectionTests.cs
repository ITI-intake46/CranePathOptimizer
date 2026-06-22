using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using CraneOptimizer.UI.Features.F4_LoadChart.Services;

namespace CraneOptimizer.Application.Tests.LoadChart
{
    public sealed class PdfLoadChartPrimarySelectionTests
    {
        [Theory]
        [InlineData("100Tons.pdf", "STC1000C", 100)]
        [InlineData("130Tons.pdf", "STC1300C", 130)]
        [InlineData("80Tons.pdf", "STC800", 80)]
        [InlineData("12_Seiter_Prospekt_SRC550C_eng.pdf", "SRC550C", 55)]
        [InlineData("200Tons.pdf", "LTM 1200-5.1", 200)]
        [InlineData("70Tons-Liebherr.pdf", "LTM 1070-4.2", 70)]
        public void NominalCapacityInference_UsesModelAndFilenameEvidence(
            string pdfPath,
            string textPreview,
            double expectedCapacityT)
        {
            var pages = new List<PdfPageCandidateDto>
            {
                new PdfPageCandidateDto
                {
                    PageNumber = 1,
                    TextPreview = textPreview
                }
            };

            double? inferred = LoadChartPdfService.InferNominalCapacityForTesting(pdfPath, pages);

            Assert.NotNull(inferred);
            Assert.Equal(expectedCapacityT, inferred!.Value, precision: 3);
        }

        [Fact]
        public void SelectBestTable_RejectsHighScoreAuxiliaryCandidateWhenNominalCapacityDisagrees()
        {
            var lowCapacityAuxiliary = CreateCandidate(
                tableIndex: 1,
                score: 2860,
                maxCapacityT: 25,
                boomColumns: 14,
                radiusRows: 26,
                warnings: new List<string>
                {
                    "Geometry fallback extractor detected a side-by-side or repeated-header load-chart region.",
                    "ENVELOPE_IMPORT_REVIEW_REQUIRED"
                });

            var primaryCandidate = CreateCandidate(
                tableIndex: 2,
                score: 2288,
                maxCapacityT: 100,
                boomColumns: 15,
                radiusRows: 18,
                warnings: new List<string>
                {
                    "Geometry fallback extractor detected a side-by-side or repeated-header load-chart region."
                });

            var selected = SelectBest("100Tons.pdf", "SANY STC1000C", lowCapacityAuxiliary, primaryCandidate);

            Assert.Same(primaryCandidate, selected);
            Assert.Contains(selected!.Warnings, warning => warning.Contains("PRIMARY_CHART_ADJUDICATION_OVERRIDE", StringComparison.OrdinalIgnoreCase));
            Assert.Contains(selected.Warnings, warning => warning.Contains("REJECTED_LOW_CAPACITY", StringComparison.OrdinalIgnoreCase));
        }

        [Fact]
        public void SelectBestTable_PrefersVerifiedCombinedContinuationCandidateOverPartialTie()
        {
            var rightSidePartial = CreateCandidate(
                tableIndex: 2,
                score: 3094.623,
                maxCapacityT: 44,
                boomColumns: 14,
                radiusRows: 29,
                warnings: new List<string>
                {
                    "Geometry fallback extractor detected a side-by-side or repeated-header load-chart region.",
                    "ENVELOPE_IMPORT_REVIEW_REQUIRED"
                });

            var combinedContinuation = CreateCandidate(
                tableIndex: 3,
                score: 3094.613,
                maxCapacityT: 130,
                boomColumns: 29,
                radiusRows: 33,
                warnings: new List<string>
                {
                    "COMBINED_CONTINUATION_TABLE: synthetic verified continuation candidate.",
                    "CONTINUATION_VERIFIED: compatible radius axes and non-decreasing combined boom headers."
                });

            var selected = SelectBest("130Tons.pdf", "SANY STC1300C", rightSidePartial, combinedContinuation);

            Assert.Same(combinedContinuation, selected);
            Assert.Contains(selected!.Warnings, warning => warning.Contains("PRIMARY_CHART_ADJUDICATION_CONTINUATION", StringComparison.OrdinalIgnoreCase));
        }

        [Fact]
        public void SelectBestTable_WhenNominalCapacityIsUnknown_PreservesHighestRawScoreBehavior()
        {
            var lowerScore = CreateCandidate(tableIndex: 1, score: 10, maxCapacityT: 40, boomColumns: 6, radiusRows: 10);
            var higherScore = CreateCandidate(tableIndex: 2, score: 100, maxCapacityT: 35, boomColumns: 6, radiusRows: 10);

            var selected = SelectBest("unknown.pdf", "No model evidence", lowerScore, higherScore);

            Assert.Same(higherScore, selected);
        }

        [Fact]
        public void CandidateMaxCapacity_NormalizesKilogramStyleTablesToTonnes()
        {
            var kgCandidate = CreateCandidate(
                tableIndex: 1,
                score: 100,
                maxCapacityT: 80000,
                boomColumns: 6,
                radiusRows: 3);

            double maxCapacityT = LoadChartPdfService.GetCandidateMaxCapacityForTesting(kgCandidate);

            Assert.Equal(80, maxCapacityT, precision: 3);
        }

        private static PdfTableCandidateDto? SelectBest(
            string pdfPath,
            string textPreview,
            params PdfTableCandidateDto[] candidates)
        {
            var page = new PdfPageCandidateDto
            {
                PageNumber = 14,
                WordCount = 500,
                NumericTokenCount = 300,
                Score = candidates.Max(candidate => candidate.Score) + 15,
                ConfidenceLabel = "High confidence",
                Summary = "Synthetic load-chart candidate page.",
                TextPreview = textPreview,
                Tables = candidates.ToList()
            };

            return LoadChartPdfService.SelectBestTableForTesting(pdfPath, new List<PdfPageCandidateDto> { page });
        }

        private static PdfTableCandidateDto CreateCandidate(
            int tableIndex,
            double score,
            double maxCapacityT,
            int boomColumns,
            int radiusRows,
            List<string>? warnings = null)
        {
            var rawGrid = BuildSyntheticLoadChartGrid(boomColumns, radiusRows, maxCapacityT);

            return new PdfTableCandidateDto
            {
                PageNumber = 14,
                TableIndex = tableIndex,
                Left = tableIndex * 100,
                Right = tableIndex * 100 + 90,
                Top = 700,
                Bottom = 300,
                BoomColumnCount = boomColumns,
                RadiusRowCount = radiusRows,
                NonEmptyCapacityCellCount = CountPositiveCapacityCells(rawGrid),
                Score = score,
                ConfidenceLabel = "High confidence",
                Summary = "Synthetic primary-chart adjudication candidate.",
                RawGrid = rawGrid,
                Warnings = warnings ?? new List<string>()
            };
        }

        private static List<List<string>> BuildSyntheticLoadChartGrid(
            int boomColumns,
            int radiusRows,
            double maxCapacity)
        {
            var grid = new List<List<string>>
            {
                new List<string> { "Radius (m)" }
                    .Concat(Enumerable.Range(0, boomColumns).Select(index => Format(10 + index * 5)))
                    .ToList()
            };

            for (int rowIndex = 0; rowIndex < radiusRows; rowIndex++)
            {
                var row = new List<string> { Format(3 + rowIndex) };
                for (int columnIndex = 0; columnIndex < boomColumns; columnIndex++)
                {
                    double capacity = rowIndex == 0 && columnIndex == 0
                        ? maxCapacity
                        : Math.Max(1, maxCapacity - rowIndex * 2 - columnIndex * 3);
                    row.Add(Format(capacity));
                }

                grid.Add(row);
            }

            return grid;
        }

        private static int CountPositiveCapacityCells(IReadOnlyList<List<string>> rawGrid)
        {
            return rawGrid
                .Skip(1)
                .Sum(row => row.Skip(1).Count(cell => double.TryParse(cell, NumberStyles.Float, CultureInfo.InvariantCulture, out double value) && value > 0));
        }

        private static string Format(double value)
        {
            return value.ToString("0.###", CultureInfo.InvariantCulture);
        }
    }
}
