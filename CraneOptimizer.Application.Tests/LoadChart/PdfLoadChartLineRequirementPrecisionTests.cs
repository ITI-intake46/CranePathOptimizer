using System.Linq;
using CraneOptimizer.UI.Features.F4_LoadChart.Services;

namespace CraneOptimizer.Application.Tests.LoadChart
{
    public sealed class PdfLoadChartLineRequirementPrecisionTests
    {
        [Fact]
        public void ParseRawGrid_SplitsLineRequirementVisualParsedAndMappedCounts()
        {
            var rawGrid = new List<List<string>>
            {
                new() { "Radius (m)", "10", "20", "30", "40" },
                new() { "5", "40", "", "30", "" },
                new() { "6", "35", "32", "", "25" },
                new() { "Number of lines", "4", "", "2", "" }
            };

            var chart = LoadChartCsvService.ParseRawGrid(rawGrid);
            var summary = chart.VisualSummary;

            Assert.Equal(4, summary.VisualBoomColumnCount);
            Assert.Equal(2, summary.CapacityRadiusRowCount);
            Assert.Equal(1, summary.LineRequirementRowCount);
            Assert.Equal(4, summary.LineRequirementVisualSlotCount);
            Assert.Equal(2, summary.ParsedLineRequirementValueCount);
            Assert.Equal(2, summary.MappedLineRequirementColumnCount);
            Assert.Equal(summary.ParsedLineRequirementValueCount, summary.LineRequirementCellCount);
            Assert.True(summary.HasOperationalLineRequirements);

            var lineRow = Assert.Single(chart.VisualRows.Where(row => row.RowKind == LoadChartVisualRowKind.LineRequirementRow));
            Assert.Equal(4, lineRow.LineRequirementVisualSlotCount);
            Assert.Equal(2, lineRow.ParsedLineRequirementValueCount);
            Assert.Equal(2, lineRow.MappedLineRequirementColumnCount);
            Assert.Equal(lineRow.ParsedLineRequirementValueCount, lineRow.LineRequirementCellCount);
            Assert.True(lineRow.ParticipatesInLineRequirementLookup);
            Assert.False(lineRow.ParticipatesInCapacityLookup);

            Assert.All(chart.Points.Where(point => point.BoomLength == 10), point => Assert.Equal(4, point.NumberOfLines));
            Assert.All(chart.Points.Where(point => point.BoomLength == 20), point => Assert.Null(point.NumberOfLines));
            Assert.All(chart.Points.Where(point => point.BoomLength == 30), point => Assert.Equal(2, point.NumberOfLines));
            Assert.All(chart.Points.Where(point => point.BoomLength == 40), point => Assert.Null(point.NumberOfLines));
        }

        [Fact]
        public void ParseExactVisualRawGrid_PreservesLineRowButCountsOnlyMappedValuesByVisualColumn()
        {
            var rawGrid = new List<List<string>>
            {
                new() { "Radius (m)", "17.5", "17.5", "21.8", "26.1" },
                new() { "3", "100", "", "90", "80" },
                new() { "4", "80", "70", "", "60" },
                new() { "Number of lines", "8", "", "4", "2" }
            };

            var chart = LoadChartCsvService.ParseExactVisualRawGrid(rawGrid, "line-requirement-precision-test");
            var summary = chart.VisualSummary;

            Assert.True(chart.IsExactVisualChart);
            Assert.Equal(4, summary.VisualBoomColumnCount);
            Assert.Equal(3, summary.UniqueBoomLengthCount);
            Assert.Equal(2, summary.CapacityRadiusRowCount);
            Assert.Equal(1, summary.LineRequirementRowCount);
            Assert.Equal(4, summary.LineRequirementVisualSlotCount);
            Assert.Equal(3, summary.ParsedLineRequirementValueCount);
            Assert.Equal(3, summary.MappedLineRequirementColumnCount);
            Assert.Equal(summary.ParsedLineRequirementValueCount, summary.LineRequirementCellCount);
            Assert.Equal(8, summary.CapacityGridSlotCount);
            Assert.Equal(6, summary.NumericRatedCapacityCellCount);
            Assert.Equal(2, summary.BlankUnavailableCellCount);
            Assert.Equal(12, summary.TotalVisualGridSlotCount);

            Assert.All(chart.Points.Where(point => point.VisualColumnIndex == 1), point => Assert.Equal(8, point.NumberOfLines));
            Assert.All(chart.Points.Where(point => point.VisualColumnIndex == 2), point => Assert.Null(point.NumberOfLines));
            Assert.All(chart.Points.Where(point => point.VisualColumnIndex == 3), point => Assert.Equal(4, point.NumberOfLines));
            Assert.All(chart.Points.Where(point => point.VisualColumnIndex == 4), point => Assert.Equal(2, point.NumberOfLines));
        }
    }
}
