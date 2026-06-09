using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using CraneOptimizer.UI.Features.F4_LoadChart.Services;
using Xunit;

namespace CraneOptimizer.Application.Tests.LoadChart
{
    public sealed class LoadChartNumberOfLinesMetadataTests
    {
        [Fact]
        public void ParseExactVisualRawGrid_PreservesNumberOfLinesPerVisualColumnWithoutCreatingCapacityRows()
        {
            var rawGrid = new List<List<string>>
            {
                new() { "Radius (m)", "39.1", "39.1", "43.4" },
                new() { "10", "38.3", "35.1", "29.9" },
                new() { "12", "31.5", "28.8", "24.8" },
                new() { "Number of lines", "5", "4", "3" }
            };

            var chart = LoadChartCsvService.ParseExactVisualRawGrid(rawGrid, "unit-test");

            Assert.Equal(6, chart.Points.Count);
            Assert.DoesNotContain(chart.Points, point => Math.Abs(point.Radius - 5d) < 0.0001 && point.Capacity < 0);

            var columnOne = chart.Points.Where(point => point.VisualColumnIndex == 1).ToList();
            var columnTwo = chart.Points.Where(point => point.VisualColumnIndex == 2).ToList();
            var columnThree = chart.Points.Where(point => point.VisualColumnIndex == 3).ToList();

            Assert.All(columnOne, point => Assert.Equal("39.1", point.VisualColumnHeader));
            Assert.All(columnTwo, point => Assert.Equal("39.1 (2)", point.VisualColumnHeader));
            Assert.All(columnThree, point => Assert.Equal("43.4", point.VisualColumnHeader));

            Assert.All(columnOne, point => Assert.Equal(5, point.NumberOfLines));
            Assert.All(columnTwo, point => Assert.Equal(4, point.NumberOfLines));
            Assert.All(columnThree, point => Assert.Equal(3, point.NumberOfLines));
            Assert.All(columnOne, point => Assert.Equal("39.1 / 5 lines", point.OperatingConditionLabel));
            Assert.All(columnTwo, point => Assert.Equal("39.1 / 4 lines", point.OperatingConditionLabel));
            Assert.All(columnThree, point => Assert.Equal("43.4 / 3 lines", point.OperatingConditionLabel));

            var serializedGrid = JsonSerializer.Deserialize<List<List<string>>>(chart.ExactLoadChartRawGridJson);
            Assert.NotNull(serializedGrid);
            Assert.Contains(serializedGrid!, row => row.Count > 0 && row[0] == "Number of lines");
        }

        [Fact]
        public void ParseRawGrid_IgnoresNumberOfLinesRowAsCapacityDataAndStoresMetadata()
        {
            var rawGrid = new List<List<string>>
            {
                new() { "Radius (m)", "20", "30" },
                new() { "5", "10", "8" },
                new() { "10", "7", "5" },
                new() { "Number of lines", "4", "2" }
            };

            var chart = LoadChartCsvService.ParseRawGrid(rawGrid);

            Assert.Equal(4, chart.Points.Count);
            Assert.Equal(10, chart.MaxCapacity);
            Assert.Equal(10, chart.MaxRadius);

            Assert.All(chart.Points.Where(point => Math.Abs(point.BoomLength - 20d) < 0.0001), point => Assert.Equal(4, point.NumberOfLines));
            Assert.All(chart.Points.Where(point => Math.Abs(point.BoomLength - 30d) < 0.0001), point => Assert.Equal(2, point.NumberOfLines));
            Assert.All(chart.Points.Where(point => Math.Abs(point.BoomLength - 20d) < 0.0001), point => Assert.Equal("4 lines", point.OperatingConditionLabel));
            Assert.All(chart.Points.Where(point => Math.Abs(point.BoomLength - 30d) < 0.0001), point => Assert.Equal("2 lines", point.OperatingConditionLabel));
        }
    }
}
