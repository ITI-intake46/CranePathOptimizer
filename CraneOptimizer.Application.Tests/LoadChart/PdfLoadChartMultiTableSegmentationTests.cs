using System;
using System.Collections;
using System.Collections.Generic;
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

        private static int InvokeResolveSequentialOrGeometryColumnForTesting(
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