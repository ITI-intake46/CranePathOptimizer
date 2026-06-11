using CraneOptimizer.UI.Features.F4_LoadChart.Services;

namespace CraneOptimizer.Application.Tests.LoadChart
{
    public sealed class UserEnteredNumberParsingTests
    {
        [Theory]
        [InlineData("5.005", 5.005)]
        [InlineData("7.004", 7.004)]
        [InlineData("5,005", 5.005)]
        [InlineData("1,234.56", 1234.56)]
        [InlineData("1.234,56", 1234.56)]
        public void TryParseUserEnteredNumber_PreservesDecimalMeasurements(string input, double expected)
        {
            bool parsed = LoadChartCsvService.TryParseUserEnteredNumber(input, out double value);

            Assert.True(parsed);
            Assert.Equal(expected, value, precision: 6);
        }
    }
}
