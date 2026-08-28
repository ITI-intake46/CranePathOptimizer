using CraneOptimizer.Domain.Entities;
using CraneOptimizer.Domain.Units;
using CraneOptimizer.UI.Features.F4_LoadChart.Services;

namespace CraneOptimizer.Application.Tests.LoadChart
{
    public sealed class LoadChartCapacityUnitTests
    {
        [Theory]
        [InlineData(800d)]
        [InlineData(999d)]
        [InlineData(1000d)]
        [InlineData(1001d)]
        [InlineData(1200d)]
        [InlineData(55000d)]
        public void ParseRawGrid_AutoUnitlessCapacityIsUnresolvedRegardlessOfMagnitude(double rawCapacity)
        {
            var chart = LoadChartCsvService.ParseRawGrid(CreateSingleCapacityGrid(rawCapacity.ToString("G", System.Globalization.CultureInfo.InvariantCulture)));

            Assert.False(chart.IsCapacityUnitResolved);
            Assert.Null(chart.EffectiveCapacityUnit);
            Assert.Equal(LoadChartCapacityUnitResolutionStatus.MissingExplicitUnit, chart.CapacityUnitResolutionStatus);
            Assert.Equal(rawCapacity, chart.RawMaxCapacity, precision: 3);
            Assert.Equal(0d, chart.MaxCapacity, precision: 3);
            Assert.Null(chart.NormalizedMaxCapacityTons);
            Assert.All(chart.Points.Where(point => point.Radius == 3.0), point => Assert.True(point.Capacity < 0d));
        }

        [Fact]
        public void ParseCsv_DefaultAutoUnitlessCapacityIsUnresolved()
        {
            string path = Path.Combine(Path.GetTempPath(), $"CraneOptimizer-E01-{Guid.NewGuid():N}.csv");
            File.WriteAllText(path, "Radius (m),11.5" + Environment.NewLine + "3.0,800" + Environment.NewLine);

            try
            {
                var chart = LoadChartCsvService.ParseCsv(path);

                Assert.False(chart.IsCapacityUnitResolved);
                Assert.Equal(LoadChartCapacityUnitResolutionStatus.MissingExplicitUnit, chart.CapacityUnitResolutionStatus);
                Assert.Equal(800, chart.RawMaxCapacity, precision: 3);
                Assert.Equal(0d, chart.MaxCapacity, precision: 3);
            }
            finally
            {
                File.Delete(path);
            }
        }

        [Fact]
        public void ParseRawGrid_TonOverrideKeepsUnitlessCapacitiesAsTons()
        {
            var chart = LoadChartCsvService.ParseRawGrid(
                CreateSingleCapacityGrid("1200"),
                LoadChartCapacityUnitMode.Ton);

            Assert.True(chart.IsCapacityUnitResolved);
            Assert.Equal(MassUnit.Ton, chart.EffectiveCapacityUnit!.Value);
            Assert.Equal(LoadChartCapacityUnitResolutionStatus.ResolvedFromUserSelection, chart.CapacityUnitResolutionStatus);
            Assert.Equal(1200, chart.RawMaxCapacity, precision: 3);
            Assert.Equal(1200, chart.MaxCapacity, precision: 3);
        }

        [Fact]
        public void ParseRawGrid_KilogramOverrideConvertsUnitlessCapacitiesToTons()
        {
            var chart = LoadChartCsvService.ParseRawGrid(
                CreateSingleCapacityGrid("800"),
                LoadChartCapacityUnitMode.Kilogram);

            Assert.True(chart.IsCapacityUnitResolved);
            Assert.Equal(MassUnit.Kilogram, chart.EffectiveCapacityUnit!.Value);
            Assert.Equal(LoadChartCapacityUnitResolutionStatus.ResolvedFromUserSelection, chart.CapacityUnitResolutionStatus);
            Assert.Equal(800, chart.RawMaxCapacity, precision: 3);
            Assert.Equal(0.8, chart.MaxCapacity, precision: 3);
        }

        [Theory]
        [InlineData("800 kg")]
        [InlineData("800kg")]
        public void ParseRawGrid_ExplicitKilogramSuffixConvertsToTons(string sourceCapacity)
        {
            var chart = LoadChartCsvService.ParseRawGrid(CreateSingleCapacityGrid(sourceCapacity));

            Assert.True(chart.IsCapacityUnitResolved);
            Assert.Equal(MassUnit.Kilogram, chart.EffectiveCapacityUnit!.Value);
            Assert.Equal(LoadChartCapacityUnitResolutionStatus.ResolvedFromExplicitEvidence, chart.CapacityUnitResolutionStatus);
            Assert.Equal(800, chart.RawMaxCapacity, precision: 3);
            Assert.Equal(0.8, chart.MaxCapacity, precision: 3);
            Assert.Equal(0.8, chart.Points.Single(point => point.Radius == 3.0 && point.BoomLength == 11.5).Capacity, precision: 3);
        }

        [Theory]
        [InlineData("1200 t")]
        [InlineData("1200t")]
        [InlineData("1200 tonnes")]
        public void ParseRawGrid_ExplicitTonSuffixKeepsLargeCapacityInTons(string sourceCapacity)
        {
            var chart = LoadChartCsvService.ParseRawGrid(CreateSingleCapacityGrid(sourceCapacity));

            Assert.True(chart.IsCapacityUnitResolved);
            Assert.Equal(MassUnit.Ton, chart.EffectiveCapacityUnit!.Value);
            Assert.Equal(LoadChartCapacityUnitResolutionStatus.ResolvedFromExplicitEvidence, chart.CapacityUnitResolutionStatus);
            Assert.Equal(1200, chart.RawMaxCapacity, precision: 3);
            Assert.Equal(1200, chart.MaxCapacity, precision: 3);
        }

        [Fact]
        public void ParseRawGrid_AutoConflictingExplicitUnitsIsUnresolved()
        {
            var chart = LoadChartCsvService.ParseRawGrid(new List<List<string>>
            {
                new() { "Radius (m)", "11.5", "15.4" },
                new() { "3.0", "800 kg", "1200 t" }
            });

            Assert.False(chart.IsCapacityUnitResolved);
            Assert.Null(chart.EffectiveCapacityUnit);
            Assert.Equal(LoadChartCapacityUnitResolutionStatus.ConflictingExplicitUnits, chart.CapacityUnitResolutionStatus);
            Assert.Equal(1200, chart.RawMaxCapacity, precision: 3);
            Assert.Equal(0d, chart.MaxCapacity, precision: 3);
        }

        [Fact]
        public void ParseRawGrid_SelectedUnitConflictingWithExplicitEvidenceIsUnresolved()
        {
            var chart = LoadChartCsvService.ParseRawGrid(
                CreateSingleCapacityGrid("800 kg"),
                LoadChartCapacityUnitMode.Ton);

            Assert.False(chart.IsCapacityUnitResolved);
            Assert.Null(chart.EffectiveCapacityUnit);
            Assert.Equal(LoadChartCapacityUnitResolutionStatus.SelectedUnitConflictsWithExplicitEvidence, chart.CapacityUnitResolutionStatus);
            Assert.Equal(0d, chart.MaxCapacity, precision: 3);
        }

        [Fact]
        public void ParseExactVisualRawGrid_AutoUnitlessCapacityIsUnresolved()
        {
            var chart = LoadChartCsvService.ParseExactVisualRawGrid(new List<List<string>>
            {
                new() { "Radius (m)", "11.5", "15.4" },
                new() { "3.0", "800", "700" },
                new() { "3.5", "750", "650" }
            });

            Assert.True(chart.IsExactVisualChart);
            Assert.False(chart.IsCapacityUnitResolved);
            Assert.Equal(800, chart.RawMaxCapacity, precision: 3);
            Assert.Equal(0d, chart.MaxCapacity, precision: 3);
        }

        [Fact]
        public void BuildCanonicalExactEditGrid_UsesPersistedTonCapacitiesInsteadOfReinterpretingSourceKgValues()
        {
            var sourceRawGrid = new List<List<string>>
            {
                new() { "Radius (m)", "11.5" },
                new() { "3.0", "800 kg" },
                new() { "Number of lines", "4" }
            };
            var canonicalPoints = new List<TargetPoint>
            {
                new()
                {
                    Radius = 3.0,
                    BoomLength = 11.5,
                    Capacity = 0.8,
                    VisualColumnIndex = 1,
                    NumberOfLines = 4
                }
            };

            var canonicalEditGrid = LoadChartCsvService.BuildCanonicalExactEditGrid(sourceRawGrid, canonicalPoints);
            var reparsed = LoadChartCsvService.ParseExactVisualRawGrid(
                canonicalEditGrid,
                "saved exact chart",
                LoadChartCapacityUnitMode.Ton);

            Assert.Equal("800 kg", sourceRawGrid[1][1]);
            Assert.Equal("0.8", canonicalEditGrid[1][1]);
            Assert.Equal("4", canonicalEditGrid[2][1]);
            Assert.True(reparsed.IsCapacityUnitResolved);
            Assert.Equal(MassUnit.Ton, reparsed.EffectiveCapacityUnit!.Value);
            Assert.Equal(0.8, reparsed.MaxCapacity, precision: 3);
            Assert.Equal(0.8, reparsed.Points.Single(point => point.Radius == 3.0).Capacity, precision: 3);
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

            Assert.True(transposed.IsCapacityUnitResolved);
            Assert.Equal(MassUnit.Kilogram, transposed.EffectiveCapacityUnit!.Value);
            Assert.Equal(55, transposed.MaxCapacity, precision: 3);
        }

        [Fact]
        public void BuildCraneDto_UserSelectedKilogramsPersistsCanonicalTons()
        {
            var resolved = LoadChartCsvService.ParseRawGrid(
                CreateSingleCapacityGrid("800"),
                LoadChartCapacityUnitMode.Kilogram);

            var dto = LoadChartCatalogService.BuildCraneDto(
                model: "Test",
                manufacturer: "Test",
                outriggerConfig: "100%",
                counterWeight: 10,
                vehicleLength: 10,
                vehicleHeight: 4,
                vehicleWidth: 3,
                outriggerWidth: 8,
                outriggerLength: 8,
                parsedChart: resolved);

            Assert.Equal(0.8, dto.MaxCapacity, precision: 3);
            Assert.Equal(0.8, dto.Points.Single().Capacity, precision: 3);
        }

        [Fact]
        public void BuildCraneDto_RejectsUnresolvedCapacityUnit()
        {
            var unresolved = LoadChartCsvService.ParseRawGrid(CreateSingleCapacityGrid("800"));

            var exception = Assert.Throws<InvalidOperationException>(() => LoadChartCatalogService.BuildCraneDto(
                model: "Test",
                manufacturer: "Test",
                outriggerConfig: "100%",
                counterWeight: 10,
                vehicleLength: 10,
                vehicleHeight: 4,
                vehicleWidth: 3,
                outriggerWidth: 8,
                outriggerLength: 8,
                parsedChart: unresolved));

            Assert.Contains("capacity unit is unresolved", exception.Message, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void CapacityUnitSummary_ResolvedKilogramsShowsRawAndNormalizedValues()
        {
            var resolved = LoadChartCsvService.ParseRawGrid(CreateSingleCapacityGrid("800 kg"));

            string summary = LoadChartCsvService.GetCapacityUnitSummary(resolved);

            Assert.Contains("raw max 800 kg", summary, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("0.8 t", summary, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void ImportStatus_UnresolvedChartShowsRawValueAndDoesNotClaimTons()
        {
            var unresolved = LoadChartCsvService.ParseRawGrid(CreateSingleCapacityGrid("800"));
            var result = new LoadChartImportResult
            {
                Chart = unresolved,
                SourceKind = "PDF",
                SourceDescription = "synthetic"
            };

            string status = result.BuildStatusMessage("chart.pdf");

            Assert.Contains("raw max 800", status, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("unit unresolved", status, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("max 800 t", status, StringComparison.OrdinalIgnoreCase);
        }

        private static List<List<string>> CreateSingleCapacityGrid(string capacity)
        {
            return new List<List<string>>
            {
                new() { "Radius (m)", "11.5" },
                new() { "3.0", capacity }
            };
        }

    }
}
