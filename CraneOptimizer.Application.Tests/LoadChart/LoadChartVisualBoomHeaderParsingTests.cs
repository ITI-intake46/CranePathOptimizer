using System;
using System.Collections.Generic;
using System.Linq;
using CraneOptimizer.UI.Features.F4_LoadChart.Services;
using Xunit;

namespace CraneOptimizer.Application.Tests.LoadChart
{
    public sealed class LoadChartVisualBoomHeaderParsingTests
    {
        [Theory]
        [InlineData("13.1 *")]
        [InlineData("13.1*")]
        [InlineData("13,1 *")]
        [InlineData("13.1 m *")]
        [InlineData("13.1 360\u00B0")]
        [InlineData("13.1 360 deg")]
        [InlineData("13.1 over rear")]
        [InlineData("13.1 rear")]
        public void ExactVisualImport_AcceptsSingleNumericBoomHeaderWithOperationalAnnotation(string annotatedHeader)
        {
            ParsedLoadChart parsed = LoadChartCsvService.ParseExactVisualRawGrid(new List<List<string>>
            {
                new() { "Radius (m)", annotatedHeader, "13.1", "17.6" },
                new() { "3", "160", "86", "86" },
                new() { "3.5", "139.3", "86", "86" }
            });

            Assert.Equal("ExactPdfVisual", parsed.LoadChartStorageMode);
            Assert.Equal("ConservativeMinimumAcrossDuplicateVisualColumns", parsed.LoadChartCapacityPolicy);
            Assert.Equal(6, parsed.Points.Count);

            var annotatedColumnPoints = parsed.Points
                .Where(point => point.VisualColumnIndex == 1)
                .ToList();

            Assert.Equal(2, annotatedColumnPoints.Count);
            Assert.All(annotatedColumnPoints, point =>
            {
                Assert.Equal(13.1, point.BoomLength, precision: 3);
                Assert.Equal(annotatedHeader, point.VisualColumnHeader);
            });
        }

        [Fact]
        public void ExactVisualImport_PreservesDuplicateAnnotatedAndUnmarkedBoomLanes()
        {
            ParsedLoadChart parsed = LoadChartCsvService.ParseExactVisualRawGrid(new List<List<string>>
            {
                new() { "Radius (m)", "13.1 *", "13.1", "17.6" },
                new() { "3", "160", "86", "86" }
            });

            var duplicateThirteenPointOneLanes = parsed.Points
                .Where(point => Math.Abs(point.Radius - 3d) < 0.001d)
                .Where(point => Math.Abs(point.BoomLength - 13.1d) < 0.001d)
                .OrderBy(point => point.VisualColumnIndex)
                .ToList();

            Assert.Equal(2, duplicateThirteenPointOneLanes.Count);
            Assert.Equal(1, duplicateThirteenPointOneLanes[0].VisualColumnIndex);
            Assert.Equal("13.1 *", duplicateThirteenPointOneLanes[0].VisualColumnHeader);
            Assert.Equal(160d, duplicateThirteenPointOneLanes[0].Capacity, precision: 3);
            Assert.Equal(2, duplicateThirteenPointOneLanes[1].VisualColumnIndex);
            Assert.Equal("13.1", duplicateThirteenPointOneLanes[1].VisualColumnHeader);
            Assert.Equal(86d, duplicateThirteenPointOneLanes[1].Capacity, precision: 3);
        }

        [Theory]
        [InlineData("13.1 17.6")]
        [InlineData("13.1 / 17.6")]
        [InlineData("13.1-17.6")]
        [InlineData("13.1 x 2")]
        public void ExactVisualImport_RejectsAmbiguousMultiNumericBoomHeaders(string ambiguousHeader)
        {
            FormatException exception = Assert.Throws<FormatException>(() =>
                LoadChartCsvService.ParseExactVisualRawGrid(new List<List<string>>
                {
                    new() { "Radius (m)", ambiguousHeader, "17.6" },
                    new() { "3", "160", "86" }
                }));

            Assert.Contains(ambiguousHeader, exception.Message);
        }
    }
}