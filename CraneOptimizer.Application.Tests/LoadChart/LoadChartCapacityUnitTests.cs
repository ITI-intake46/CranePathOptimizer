using CraneOptimizer.Domain.Units;
using CraneOptimizer.UI.Features.F4_LoadChart.Services;

namespace CraneOptimizer.Application.Tests.LoadChart
{
    public sealed class LoadChartCapacityUnitTests
    {
        [Fact]
        public void ParseRawGrid_AutoTreatsLargeUnitlessCapacitiesAsKilograms()
        {
            var chart = LoadChartCsvService.ParseRawGrid(CreateKilogramStyleGrid());

            Assert.Equal(MassUnit.Kilogram, chart.EffectiveCapacityUnit);
            Assert.Equal(55000, chart.RawMaxCapacity, precision: 3);
            Assert.Equal(55, chart.MaxCapacity, precision: 3);
            Assert.Equal(55, chart.Points.Single(point => point.Radius == 3.0 && point.BoomLength == 11.5).Capacity, precision: 3);
        }

        [Fact]
        public void ParseRawGrid_TonOverrideKeepsLargeUnitlessCapacitiesAsTons()
        {
            var chart = LoadChartCsvService.ParseRawGrid(
                CreateKilogramStyleGrid(),
                LoadChartCapacityUnitMode.Ton);

            Assert.Equal(MassUnit.Ton, chart.EffectiveCapacityUnit);
            Assert.Equal(55000, chart.RawMaxCapacity, precision: 3);
            Assert.Equal(55000, chart.MaxCapacity, precision: 3);
        }

        [Fact]
        public void ParseRawGrid_KilogramOverrideConvertsUnitlessCapacitiesToTons()
        {
            var chart = LoadChartCsvService.ParseRawGrid(
                CreateSmallKilogramStyleGrid(),
                LoadChartCapacityUnitMode.Kilogram);

            Assert.Equal(MassUnit.Kilogram, chart.EffectiveCapacityUnit);
            Assert.Equal(550, chart.RawMaxCapacity, precision: 3);
            Assert.Equal(0.55, chart.MaxCapacity, precision: 3);
        }

        [Fact]
        public void ParseRawGrid_AutoKeepsNormalTonChartsAsTons()
        {
            var chart = LoadChartCsvService.ParseRawGrid(new List<List<string>>
            {
                new() { "Radius (m)", "13.1", "17.6" },
                new() { "2.5", "180", "" },
                new() { "3", "160", "86" }
            });

            Assert.Equal(MassUnit.Ton, chart.EffectiveCapacityUnit);
            Assert.Equal(180, chart.RawMaxCapacity, precision: 3);
            Assert.Equal(180, chart.MaxCapacity, precision: 3);
        }

        [Fact]
        public void ParseRawGrid_ExplicitKilogramSuffixConvertsToTons()
        {
            var chart = LoadChartCsvService.ParseRawGrid(new List<List<string>>
            {
                new() { "Radius (m)", "11.5" },
                new() { "3", "55000 kg" }
            });

            Assert.Equal(MassUnit.Kilogram, chart.EffectiveCapacityUnit);
            Assert.Equal(55000, chart.RawMaxCapacity, precision: 3);
            Assert.Equal(55, chart.MaxCapacity, precision: 3);
        }

        [Fact]
        public void TransposePreview_PreservesSelectedCapacityUnitMode()
        {
            var chart = LoadChartCsvService.ParseRawGrid(
                new List<List<string>>
                {
                    new() { "Radius (m)", "3.0", "3.5" },
                    new() { "11.5", "55000", "53400" },
                    new() { "15.4", "40000", "40000" }
                },
                LoadChartCapacityUnitMode.Kilogram);

            var transposed = LoadChartCsvService.TransposePreview(
                chart.PreviewTable,
                chart.PreviewHeaders,
                LoadChartCapacityUnitMode.Kilogram);

            Assert.Equal(MassUnit.Kilogram, transposed.EffectiveCapacityUnit);
            Assert.Equal(55, transposed.MaxCapacity, precision: 3);
        }

        private static List<List<string>> CreateKilogramStyleGrid()
        {
            return new List<List<string>>
            {
                new() { "Radius (m)", "11.5", "15.4", "19.4" },
                new() { "3.0", "55000", "40000", "32000" },
                new() { "3.5", "53400", "40000", "32000" }
            };
        }

        private static List<List<string>> CreateSmallKilogramStyleGrid()
        {
            return new List<List<string>>
            {
                new() { "Radius (m)", "11.5" },
                new() { "3.0", "550" }
            };
        }
    }
}