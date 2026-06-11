using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using CraneOptimizer.UI.Features.F4_LoadChart.Services;
using Xunit;

namespace CraneOptimizer.Application.Tests.LoadChart
{
    public sealed class PdfLoadChartMultiTableSegmentationTests
    {
        [Fact]
        public void HeaderSegmentation_SplitsSideBySideMainBoomAndJibHeaderAtLargeLayoutGap()
        {
            var segments = InvokeSplitHeaderValuesForTesting(
                new[] { 11.8, 15.95, 20.1, 28.4, 36.7, 45.0, 15.0, 30.0 },
                new[] { 170.716, 221.727, 272.589, 323.455, 374.321, 425.187, 946.962, 1084.781 });

            Assert.Single(segments);
            Assert.Equal(new[] { 11.8, 15.95, 20.1, 28.4, 36.7, 45.0 }, segments[0], new DoubleArrayPrecisionComparer(3));
        }

        [Fact]
        public void HeaderSegmentation_DoesNotSplitRegularRepeatedVisualBoomHeader()
        {
            var values = new[] { 13.1, 13.1, 17.6, 22.1, 26.6, 31.1, 35.5, 40.0, 44.5, 49.0, 53.5, 58.0, 62.0 };
            var xs = new[] { 88.0, 122.0, 156.0, 190.0, 224.0, 258.0, 292.0, 326.0, 360.0, 394.0, 428.0, 462.0, 496.0 };

            var segments = InvokeSplitHeaderValuesForTesting(values, xs);

            Assert.Single(segments);
            Assert.Equal(values, segments[0], new DoubleArrayPrecisionComparer(3));
        }

        [Fact]
        public void OpenEndedBodyLimit_KeepsMainTableLastCapacityButRejectsFarJibColumns()
        {
            var method = typeof(LoadChartPdfService).GetMethod(
                "GetOpenEndedBodyRightLimitForTesting",
                BindingFlags.Static | BindingFlags.NonPublic);

            Assert.NotNull(method);

            double limit = (double)method!.Invoke(null, new object[] { 425.187, 50.866 })!;

            Assert.True(425.5 < limit);
            Assert.True(505.0 > limit);
            Assert.True(809.0 > limit);
        }

        [Fact]
        public void SparseTerminalRows_AssignFarRightCapacityByXPositionNotSequentialOrder()
        {
            var headerCenters = new[]
            {
                104.788, 141.332, 177.076, 213.22, 249.364, 285.508,
                321.652, 357.796, 393.94, 430.084, 466.228, 502.372
            };

            int columnForTwo = InvokeResolveSequentialOrGeometryColumnForTesting(
                headerCenters,
                sequentialColumn: 9,
                bodyCenterX: 434.968);

            int columnForOnePointFive = InvokeResolveSequentialOrGeometryColumnForTesting(
                headerCenters,
                sequentialColumn: 10,
                bodyCenterX: 510.128);

            Assert.Equal(9, columnForTwo);
            Assert.Equal(11, columnForOnePointFive);
        }

        [Fact]
        public void SparseTerminalRows_PreserveDenseRowGeometryAgreement()
        {
            var headerCenters = new[]
            {
                104.788, 141.332, 177.076, 213.22, 249.364, 285.508,
                321.652, 357.796, 393.94, 430.084, 466.228, 502.372
            };

            Assert.Equal(0, InvokeResolveSequentialOrGeometryColumnForTesting(headerCenters, 0, 110.304));
            Assert.Equal(10, InvokeResolveSequentialOrGeometryColumnForTesting(headerCenters, 10, 471.744));
            Assert.Equal(11, InvokeResolveSequentialOrGeometryColumnForTesting(headerCenters, 11, 507.912));
        }


        [Fact]
        public void TableReadingOrder_ReindexesSideBySideTablesByLeftPositionEvenWhenRightTableScoresHigher()
        {
            var candidates = new List<PdfTableCandidateDto>
            {
                CreateCandidate(tableIndex: 1, left: 652.903, right: 1132.692, top: 566.0, bottom: 94.0, score: 3094.623),
                CreateCandidate(tableIndex: 2, left: 57.872, right: 537.656, top: 566.0, bottom: 94.0, score: 2471.083)
            };

            var ordered = InvokeOrderCandidatesInReadingOrderForTesting(candidates);

            Assert.Equal(2, ordered.Count);
            Assert.Equal(1, ordered[0].TableIndex);
            Assert.Equal(57.872, ordered[0].Left, precision: 3);
            Assert.Equal(2471.083, ordered[0].Score, precision: 3);
            Assert.Equal(2, ordered[1].TableIndex);
            Assert.Equal(652.903, ordered[1].Left, precision: 3);
            Assert.Equal(3094.623, ordered[1].Score, precision: 3);
        }

        [Fact]
        public void TableReadingOrder_SortsVisualRowsTopToBottomThenLeftToRight()
        {
            var candidates = new List<PdfTableCandidateDto>
            {
                CreateCandidate(tableIndex: 1, left: 600, right: 900, top: 720, bottom: 520, score: 400),
                CreateCandidate(tableIndex: 2, left: 100, right: 400, top: 720, bottom: 520, score: 100),
                CreateCandidate(tableIndex: 3, left: 100, right: 400, top: 430, bottom: 240, score: 900)
            };

            var ordered = InvokeOrderCandidatesInReadingOrderForTesting(candidates);

            Assert.Equal(new[] { 1, 2, 3 }, ordered.Select(candidate => candidate.TableIndex).ToArray());
            Assert.Equal(100, ordered[0].Left, precision: 3);
            Assert.Equal(600, ordered[1].Left, precision: 3);
            Assert.Equal(100, ordered[2].Left, precision: 3);
            Assert.Equal(900, ordered[2].Score, precision: 3);
        }

        [Fact]
        public void TableReadingOrder_KeepsScoreBasedBestTableSelectionSeparateFromVisibleNumbering()
        {
            var leftPhysicalTable = CreateCandidate(tableIndex: 1, left: 50, right: 450, top: 700, bottom: 300, score: 10);
            var rightPhysicalTable = CreateCandidate(tableIndex: 2, left: 550, right: 950, top: 700, bottom: 300, score: 100);
            var ordered = InvokeOrderCandidatesInReadingOrderForTesting(new List<PdfTableCandidateDto> { rightPhysicalTable, leftPhysicalTable });

            var scanResult = new PdfLoadChartScanResult
            {
                PdfPath = "synthetic.pdf",
                PageCount = 1,
                Pages = new List<PdfPageCandidateDto>
                {
                    new PdfPageCandidateDto
                    {
                        PageNumber = 14,
                        Tables = ordered
                    }
                }
            };

            Assert.Equal(1, ordered[0].TableIndex);
            Assert.Equal(50, ordered[0].Left, precision: 3);
            Assert.Equal(2, ordered[1].TableIndex);
            Assert.Equal(550, ordered[1].Left, precision: 3);
            Assert.Same(ordered[1], scanResult.GetBestTable());
        }
        [Fact]
        public void TableContinuation_AddsCombinedCandidateForVerifiedSharedBoundaryTables()
        {
            var left = CreateCandidate(
                tableIndex: 1,
                left: 57.872,
                right: 537.656,
                top: 566.0,
                bottom: 94.0,
                score: 2471.083,
                rawGrid: BuildSyntheticLoadChartGrid(
                    new[] { 13.3, 17.6, 21.9, 26.2, 30.5, 34.8 },
                    new[] { 3.0, 4.0, 5.0, 6.0 },
                    baseCapacity: 130));

            var right = CreateCandidate(
                tableIndex: 2,
                left: 652.903,
                right: 1132.692,
                top: 566.0,
                bottom: 94.0,
                score: 3094.623,
                rawGrid: BuildSyntheticLoadChartGrid(
                    new[] { 34.8, 39.1, 43.4, 47.8, 52.1, 56.4, 60.0 },
                    new[] { 3.0, 4.0, 5.0, 6.0 },
                    baseCapacity: 110));

            var candidates = InvokeAppendVerifiedContinuationCandidatesForTesting(new List<PdfTableCandidateDto> { left, right });

            Assert.Equal(3, candidates.Count);
            Assert.Equal(1, candidates[0].TableIndex);
            Assert.Equal(2, candidates[1].TableIndex);

            var combined = candidates[2];
            Assert.Equal(3, combined.TableIndex);
            Assert.Contains(combined.Warnings, warning => warning.Contains("COMBINED_CONTINUATION_TABLE", StringComparison.OrdinalIgnoreCase));
            Assert.Equal(13, combined.BoomColumnCount);
            Assert.Equal(4, combined.RadiusRowCount);
            Assert.Equal(new[] { "Radius (m)", "13.3", "17.6", "21.9", "26.2", "30.5", "34.8", "34.8", "39.1", "43.4", "47.8", "52.1", "56.4", "60" }, combined.RawGrid[0]);
            Assert.True(combined.ImportGrid.Count > 1);
            Assert.Equal(12, combined.ImportGrid[0].Count - 1);
        }

        [Fact]
        public void TableContinuation_DoesNotCombineBroadOverlappingBoomRanges()
        {
            var left = CreateCandidate(
                tableIndex: 1,
                left: 50,
                right: 450,
                top: 700,
                bottom: 300,
                score: 1000,
                rawGrid: BuildSyntheticLoadChartGrid(
                    new[] { 13.3, 17.6, 21.9, 26.2, 30.5, 34.8 },
                    new[] { 3.0, 4.0, 5.0 },
                    baseCapacity: 120));

            var right = CreateCandidate(
                tableIndex: 2,
                left: 550,
                right: 950,
                top: 700,
                bottom: 300,
                score: 1100,
                rawGrid: BuildSyntheticLoadChartGrid(
                    new[] { 21.9, 26.2, 30.5, 34.8, 39.1, 43.4 },
                    new[] { 3.0, 4.0, 5.0 },
                    baseCapacity: 100));

            var candidates = InvokeAppendVerifiedContinuationCandidatesForTesting(new List<PdfTableCandidateDto> { left, right });

            Assert.Equal(2, candidates.Count);
        }

        [Fact]
        public void TableContinuation_DoesNotCombineUnrelatedSeparatedBoomRanges()
        {
            var left = CreateCandidate(
                tableIndex: 1,
                left: 50,
                right: 450,
                top: 700,
                bottom: 300,
                score: 1000,
                rawGrid: BuildSyntheticLoadChartGrid(
                    new[] { 13.3, 17.6, 21.9, 26.2 },
                    new[] { 3.0, 4.0, 5.0 },
                    baseCapacity: 120));

            var right = CreateCandidate(
                tableIndex: 2,
                left: 550,
                right: 950,
                top: 700,
                bottom: 300,
                score: 1100,
                rawGrid: BuildSyntheticLoadChartGrid(
                    new[] { 70.0, 74.0, 78.0, 82.0 },
                    new[] { 3.0, 4.0, 5.0 },
                    baseCapacity: 100));

            var candidates = InvokeAppendVerifiedContinuationCandidatesForTesting(new List<PdfTableCandidateDto> { left, right });

            Assert.Equal(2, candidates.Count);
        }

        [Fact]
        public void TableContinuation_DoesNotCombineSmallForwardGapWithoutSharedBoundary()
        {
            var left = CreateCandidate(
                tableIndex: 1,
                left: 50,
                right: 450,
                top: 700,
                bottom: 300,
                score: 1000,
                rawGrid: BuildSyntheticLoadChartGrid(
                    new[] { 11.5, 23.7, 36.5, 48.7 },
                    new[] { 3.0, 4.0, 5.0, 6.0 },
                    baseCapacity: 120));

            var right = CreateCandidate(
                tableIndex: 2,
                left: 550,
                right: 950,
                top: 700,
                bottom: 300,
                score: 1100,
                rawGrid: BuildSyntheticLoadChartGrid(
                    new[] { 51.9, 54.0, 56.0, 60.0 },
                    new[] { 3.0, 4.0, 5.0, 6.0 },
                    baseCapacity: 100));

            var candidates = InvokeAppendVerifiedContinuationCandidatesForTesting(new List<PdfTableCandidateDto> { left, right });

            Assert.Equal(2, candidates.Count);
        }

        [Fact]
        public void TableContinuation_DoesNotCombineDifferentRadiusAxes()
        {
            var left = CreateCandidate(
                tableIndex: 1,
                left: 50,
                right: 450,
                top: 700,
                bottom: 300,
                score: 1000,
                rawGrid: BuildSyntheticLoadChartGrid(
                    new[] { 13.3, 17.6, 21.9, 26.2 },
                    new[] { 3.0, 4.0, 5.0 },
                    baseCapacity: 120));

            var right = CreateCandidate(
                tableIndex: 2,
                left: 550,
                right: 950,
                top: 700,
                bottom: 300,
                score: 1100,
                rawGrid: BuildSyntheticLoadChartGrid(
                    new[] { 26.2, 30.5, 34.8, 39.1 },
                    new[] { 20.0, 21.0, 22.0 },
                    baseCapacity: 100));

            var candidates = InvokeAppendVerifiedContinuationCandidatesForTesting(new List<PdfTableCandidateDto> { left, right });

            Assert.Equal(2, candidates.Count);
        }

        private static List<PdfTableCandidateDto> InvokeAppendVerifiedContinuationCandidatesForTesting(
            IReadOnlyList<PdfTableCandidateDto> candidates)
        {
            var method = typeof(LoadChartPdfService).GetMethod(
                "AppendVerifiedContinuationCandidatesForTesting",
                BindingFlags.Static | BindingFlags.NonPublic);

            Assert.NotNull(method);

            var result = (IEnumerable)method!.Invoke(null, new object[] { candidates })!;

            return result
                .Cast<PdfTableCandidateDto>()
                .ToList();
        }
        private static List<PdfTableCandidateDto> InvokeOrderCandidatesInReadingOrderForTesting(
            IReadOnlyList<PdfTableCandidateDto> candidates)
        {
            var method = typeof(LoadChartPdfService).GetMethod(
                "OrderCandidatesInReadingOrderForTesting",
                BindingFlags.Static | BindingFlags.NonPublic);

            Assert.NotNull(method);

            var result = (IEnumerable)method!.Invoke(null, new object[] { candidates })!;

            return result
                .Cast<PdfTableCandidateDto>()
                .ToList();
        }

        private static PdfTableCandidateDto CreateCandidate(
            int tableIndex,
            double left,
            double right,
            double top,
            double bottom,
            double score,
            List<List<string>>? rawGrid = null,
            List<string>? warnings = null)
        {
            rawGrid ??= new List<List<string>>
            {
                new List<string> { "Radius (m)", "13.3", "17.6" },
                new List<string> { "3", "130", "110" }
            };

            return new PdfTableCandidateDto
            {
                PageNumber = 14,
                TableIndex = tableIndex,
                Left = left,
                Right = right,
                Top = top,
                Bottom = bottom,
                BoomColumnCount = Math.Max(0, rawGrid.FirstOrDefault()?.Count - 1 ?? 0),
                RadiusRowCount = rawGrid.Skip(1).Count(row => row.Count > 0 && IsNumericText(row[0])),
                NonEmptyCapacityCellCount = CountPositiveCapacityCells(rawGrid),
                Score = score,
                ConfidenceLabel = "High",
                Summary = "Synthetic candidate for PDF table regression testing.",
                RawGrid = rawGrid,
                Warnings = warnings ?? new List<string>()
            };
        }

        private static List<List<string>> BuildSyntheticLoadChartGrid(
            IReadOnlyList<double> boomHeaders,
            IReadOnlyList<double> radii,
            double baseCapacity)
        {
            var grid = new List<List<string>>
            {
                new List<string> { "Radius (m)" }
                    .Concat(boomHeaders.Select(FormatTestNumber))
                    .ToList()
            };

            foreach (double radius in radii)
            {
                var row = new List<string> { FormatTestNumber(radius) };

                for (int column = 0; column < boomHeaders.Count; column++)
                {
                    double capacity = Math.Max(1, baseCapacity - radius * 2.0 - column * 3.0);
                    row.Add(FormatTestNumber(capacity));
                }

                grid.Add(row);
            }

            return grid;
        }

        private static int CountPositiveCapacityCells(IReadOnlyList<List<string>> rawGrid)
        {
            return rawGrid
                .Skip(1)
                .Where(row => row.Count > 0 && IsNumericText(row[0]))
                .Sum(row => row.Skip(1).Count(cell => IsNumericText(cell)));
        }

        private static string FormatTestNumber(double value)
        {
            return value.ToString("0.###", CultureInfo.InvariantCulture);
        }

        private static bool IsNumericText(string text)
        {
            return double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out _);
        }        private static int InvokeResolveSequentialOrGeometryColumnForTesting(
            IReadOnlyList<double> headerCenters,
            int sequentialColumn,
            double bodyCenterX)
        {
            var method = typeof(LoadChartPdfService).GetMethod(
                "ResolveSequentialOrGeometryColumnForTesting",
                BindingFlags.Static | BindingFlags.NonPublic);

            Assert.NotNull(method);

            return (int)method!.Invoke(null, new object[] { headerCenters, sequentialColumn, bodyCenterX })!;
        }
        private static List<double[]> InvokeSplitHeaderValuesForTesting(
            IReadOnlyList<double> values,
            IReadOnlyList<double> centerXs)
        {
            var method = typeof(LoadChartPdfService).GetMethod(
                "SplitHeaderValuesForTesting",
                BindingFlags.Static | BindingFlags.NonPublic);

            Assert.NotNull(method);

            var result = (IEnumerable)method!.Invoke(null, new object[] { values, centerXs })!;

            return result
                .Cast<IEnumerable<double>>()
                .Select(segment => segment.ToArray())
                .ToList();
        }

        private sealed class DoubleArrayPrecisionComparer : IEqualityComparer<double>
        {
            private readonly int _precision;

            public DoubleArrayPrecisionComparer(int precision)
            {
                _precision = precision;
            }

            public bool Equals(double x, double y)
            {
                return Math.Round(x, _precision).Equals(Math.Round(y, _precision));
            }

            public int GetHashCode(double obj)
            {
                return Math.Round(obj, _precision).GetHashCode();
            }
        }
    }
}