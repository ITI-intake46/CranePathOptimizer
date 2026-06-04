using System.Reflection;
using CraneOptimizer.UI.Features.F4_LoadChart.Services;
using Xunit;

namespace CraneOptimizer.Application.Tests.LoadChart
{
    public sealed class PdfLoadChartSplitHeaderReconstructionTests
    {
        [Fact]
        public void ReconstructsHiddenPhysicalLaneUnderSpanningBoomHeader()
        {
            var method = typeof(LoadChartPdfService).GetMethod(
                "ReconstructPhysicalLaneHeaderValuesForTesting",
                BindingFlags.Static | BindingFlags.NonPublic);

            Assert.NotNull(method);

            var result = (IEnumerable<double>)method!.Invoke(
                null,
                new object[]
                {
                    new double[] { 13.1, 17.6, 22.1, 26.6 },
                    new double[] { 100.0, 140.0, 180.0, 220.0 },
                    new double[] { 84.0, 118.0, 159.0, 200.0, 241.0 }
                })!;

            var actual = result.ToArray();
            var expected = new[] { 13.1, 13.1, 17.6, 22.1, 26.6 };

            Assert.Equal(expected.Length, actual.Length);

            for (int i = 0; i < expected.Length; i++)
            {
                Assert.Equal(expected[i], actual[i], precision: 3);
            }
        }

        [Fact]
        public void RejectsInvalidLaneGeometryWhenLaneCountIsTooSmall()
        {
            var method = typeof(LoadChartPdfService).GetMethod(
                "ReconstructPhysicalLaneHeaderValuesForTesting",
                BindingFlags.Static | BindingFlags.NonPublic);

            Assert.NotNull(method);

            var result = (IEnumerable<double>)method!.Invoke(
                null,
                new object[]
                {
                    new double[] { 13.1, 17.6, 22.1, 26.6 },
                    new double[] { 100.0, 140.0, 180.0, 220.0 },
                    new double[] { 100.0, 140.0, 180.0 }
                })!;

            Assert.Empty(result);
        }

        [Fact]
        public void LabelsOnlyTheExclusiveLaneThatContainsTheLocalMarker()
        {
            var method = typeof(LoadChartPdfService).GetMethod(
                "BuildVisualHeaderLabelsForTesting",
                BindingFlags.Static | BindingFlags.NonPublic);

            Assert.NotNull(method);

            var result = (IEnumerable<string>)method!.Invoke(
                null,
                new object[]
                {
                    new double[] { 13.1, 13.1, 17.6, 22.1 },
                    new double[] { 84.0, 118.0, 151.0, 184.0 },
                    new string[] { "360â”¬â–‘", "*" },
                    new double[] { 131.5, 100.0 },
                    new double[] { 739.0, 689.0 },
                    698.5,
                    681.0
                })!;

            Assert.Equal(
                new[] { "13.1 *", "13.1", "17.6", "22.1" },
                result.ToArray());
        }

        [Fact]
        public void AllowsOperationMarkersWhenTheyAreActuallyLocalSubheaderMarkers()
        {
            var method = typeof(LoadChartPdfService).GetMethod(
                "BuildVisualHeaderLabelsForTesting",
                BindingFlags.Static | BindingFlags.NonPublic);

            Assert.NotNull(method);

            var result = (IEnumerable<string>)method!.Invoke(
                null,
                new object[]
                {
                    new double[] { 13.1, 13.1, 17.6 },
                    new double[] { 84.0, 118.0, 151.0 },
                    new string[] { "360Â°" },
                    new double[] { 118.0 },
                    new double[] { 689.0 },
                    698.5,
                    681.0
                })!;

            Assert.Equal(
                new[] { "13.1", "13.1 360Â°", "17.6" },
                result.ToArray());
        }

        [Fact]
        public void LeavesDuplicateVisualBoomLabelsNumericWhenNoMarkerEvidenceExists()
        {
            var method = typeof(LoadChartPdfService).GetMethod(
                "BuildVisualHeaderLabelsForTesting",
                BindingFlags.Static | BindingFlags.NonPublic);

            Assert.NotNull(method);

            var result = (IEnumerable<string>)method!.Invoke(
                null,
                new object[]
                {
                    new double[] { 13.1, 13.1, 17.6 },
                    new double[] { 84.0, 118.0, 151.0 },
                    Array.Empty<string>(),
                    Array.Empty<double>(),
                    Array.Empty<double>(),
                    null!,
                    null!
                })!;

            Assert.Equal(
                new[] { "13.1", "13.1", "17.6" },
                result.ToArray());
        }
    }
}