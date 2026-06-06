using CraneOptimizer.UI.Features.F4_LoadChart.DTOs;
using CraneOptimizer.UI.Features.F4_LoadChart.Views;
using System.Data;
using System.Reflection;

namespace CraneOptimizer.Application.Tests.LoadChart
{
    public sealed class LoadChartCatalogPreviewTests
    {
        [Fact]
        public void SavedStandardChartPreview_RendersMinusOneBlankSentinelsAsEmptyCells()
        {
            var crane = CreateCrane(
                new[]
                {
                    new LoadChartPointDto(2.5, 13.1, 180),
                    new LoadChartPointDto(2.5, 17.6, -1),
                    new LoadChartPointDto(3.0, 13.1, 160),
                    new LoadChartPointDto(3.0, 17.6, 86)
                });

            DataTable table = BuildPreviewTable(crane);

            Assert.Equal("13.1", table.Columns[1].ColumnName);
            Assert.Equal("17.6", table.Columns[2].ColumnName);
            Assert.Equal("180", table.Rows[0][1]);
            Assert.Equal(string.Empty, table.Rows[0][2]);
            Assert.Equal("160", table.Rows[1][1]);
            Assert.Equal("86", table.Rows[1][2]);
        }

        [Fact]
        public void SavedExactVisualChartPreview_RendersMinusOneBlankSentinelsAsEmptyCells()
        {
            var crane = CreateCrane(
                new[]
                {
                    new LoadChartPointDto(2.5, 13.1, 180, 1, "13.1"),
                    new LoadChartPointDto(2.5, 17.6, -1, 2, "17.6"),
                    new LoadChartPointDto(3.0, 13.1, 160, 1, "13.1"),
                    new LoadChartPointDto(3.0, 17.6, 86, 2, "17.6")
                },
                storageMode: "ExactPdfVisual",
                capacityPolicy: "ConservativeMinimumAcrossDuplicateVisualColumns");

            DataTable table = BuildPreviewTable(crane);

            Assert.Equal("13.1", table.Columns[1].ColumnName);
            Assert.Equal("17.6", table.Columns[2].ColumnName);
            Assert.Equal("180", table.Rows[0][1]);
            Assert.Equal(string.Empty, table.Rows[0][2]);
            Assert.Equal("160", table.Rows[1][1]);
            Assert.Equal("86", table.Rows[1][2]);
        }

        [Fact]
        public void CatalogPreviewColumnBinding_UsesIndexPathsForDecimalColumnHeaders()
        {
            MethodInfo? method = typeof(CraneCatalogBrowserDialog).GetMethod(
                "BuildPreviewColumnBindingPath",
                BindingFlags.NonPublic | BindingFlags.Static);

            Assert.NotNull(method);

            Assert.Equal("[0]", method.Invoke(null, new object[] { 0 }));
            Assert.Equal("[1]", method.Invoke(null, new object[] { 1 }));
            Assert.Equal("[11]", method.Invoke(null, new object[] { 11 }));
        }

        private static DataTable BuildPreviewTable(LoadChartCraneDto crane)
        {
            MethodInfo? method = typeof(CraneCatalogBrowserDialog).GetMethod(
                "BuildPreviewTable",
                BindingFlags.NonPublic | BindingFlags.Static);

            Assert.NotNull(method);

            object? result = method.Invoke(null, new object[] { crane });
            return Assert.IsType<DataTable>(result);
        }

        private static LoadChartCraneDto CreateCrane(
            IReadOnlyList<LoadChartPointDto> points,
            string storageMode = "Standard2D",
            string capacityPolicy = "Standard2D")
        {
            return new LoadChartCraneDto(
                Id: 0,
                Name: "Test Crane",
                Manufacturer: "",
                CraneType: "Mobile",
                OutriggerConfig: "100%",
                Counterweight: 0,
                VehicleLengthMeters: 0,
                VehicleHeightMeters: 0,
                VehicleWidthMeters: 0,
                OutriggerWidthMeters: null,
                OutriggerLengthMeters: null,
                MinRadius: points.Min(point => point.Radius),
                MaxRadius: points.Max(point => point.Radius),
                MinBoomLength: points.Min(point => point.BoomLength),
                MaxBoomLength: points.Max(point => point.BoomLength),
                MaxCapacity: points.Where(point => point.Capacity > 0).Max(point => point.Capacity),
                Points: points,
                LoadChartStorageMode: storageMode,
                LoadChartCapacityPolicy: capacityPolicy);
        }
    }
}