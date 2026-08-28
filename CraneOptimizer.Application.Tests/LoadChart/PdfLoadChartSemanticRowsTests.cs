using System;
using System.Linq;
using CraneOptimizer.UI.Features.F4_LoadChart.Services;

namespace CraneOptimizer.Application.Tests.LoadChart
{
    public sealed class PdfLoadChartSemanticRowsTests
    {
        [Fact]
        public void ParseExactVisualRawGrid_ClassifiesNumberOfLinesAsOperationalMetadata()
        {
            var rawGrid = new List<List<string>>
            {
                new() { "Radius (m)", "17.5", "17.5", "21.8" },
                new() { "3", "100", "", "90" },
                new() { "4", "80", "70", "" },
                new() { "Number of lines", "8", "6", "4" }
            };

            var chart = LoadChartCsvService.ParseExactVisualRawGrid(rawGrid, "semantic-row-test", LoadChartCapacityUnitMode.Ton);
            var summary = chart.VisualSummary;

            Assert.True(chart.IsExactVisualChart);
            Assert.Equal(3, summary.VisualBoomColumnCount);
            Assert.Equal(2, summary.UniqueBoomLengthCount);
            Assert.Equal(2, summary.CapacityRadiusRowCount);
            Assert.Equal(1, summary.LineRequirementRowCount);
            Assert.Equal(9, summary.TotalVisualGridSlotCount);
            Assert.Equal(6, summary.CapacityGridSlotCount);
            Assert.Equal(4, summary.NumericRatedCapacityCellCount);
            Assert.Equal(2, summary.BlankUnavailableCellCount);
            Assert.Equal(3, summary.LineRequirementCellCount);

            Assert.Contains(chart.VisualRows, row =>
                row.RowKind == LoadChartVisualRowKind.LineRequirementRow &&
                string.Equals(row.DisplayLabel, "Number of lines", StringComparison.OrdinalIgnoreCase));

            Assert.Equal(6, chart.Points.Count);
            Assert.Equal(4, chart.MaxRadius);
            Assert.All(chart.Points.Where(point => point.VisualColumnIndex == 1), point => Assert.Equal(8, point.NumberOfLines));
            Assert.All(chart.Points.Where(point => point.VisualColumnIndex == 2), point => Assert.Equal(6, point.NumberOfLines));
            Assert.All(chart.Points.Where(point => point.VisualColumnIndex == 3), point => Assert.Equal(4, point.NumberOfLines));
        }

        [Fact]
        public void ParseRawGrid_NumberOfLinesRowRemainsVisibleButDoesNotBecomeRadiusData()
        {
            var rawGrid = new List<List<string>>
            {
                new() { "Radius (m)", "10", "20" },
                new() { "5", "40", "30" },
                new() { "6", "35", "" },
                new() { "Number of lines", "4", "2" }
            };

            var chart = LoadChartCsvService.ParseRawGrid(rawGrid, LoadChartCapacityUnitMode.Ton);
            var summary = chart.VisualSummary;

            Assert.Equal(2, summary.CapacityRadiusRowCount);
            Assert.Equal(1, summary.LineRequirementRowCount);
            Assert.Equal(3, summary.NumericRatedCapacityCellCount);
            Assert.Equal(1, summary.BlankUnavailableCellCount);
            Assert.Equal(2, summary.LineRequirementCellCount);
            Assert.Equal(6, summary.TotalVisualGridSlotCount);
            Assert.Equal(6, chart.MaxRadius);
            Assert.Equal(4, chart.Points.Count);
            Assert.Contains(chart.PreviewTable.Rows.Cast<System.Data.DataRow>(), row =>
                string.Equals(row[0]?.ToString(), "Number of lines", StringComparison.OrdinalIgnoreCase));
        }
    }
}
