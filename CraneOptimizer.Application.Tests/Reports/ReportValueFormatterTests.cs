using CraneOptimizer.Application.Reports.Models;
using CraneOptimizer.Application.Reports.Services;

namespace CraneOptimizer.Application.Tests.Reports;

public class ReportValueFormatterTests
{
    private readonly ReportValueFormatter _formatter = new();

    [Fact]
    public void FormatValue_FormatsDoubleWithInvariantDecimalSeparator()
    {
        var result = _formatter.FormatValue(12.34567);

        Assert.Equal("12.346", result);
    }

    [Fact]
    public void FormatValue_FormatsDateTimeAsUtc()
    {
        var date = new DateTime(2026, 6, 18, 12, 30, 15, DateTimeKind.Local);

        var result = _formatter.FormatValue(date);

        Assert.Equal(date.ToUniversalTime().ToString("yyyy-MM-dd HH:mm:ss"), result);
    }

    [Fact]
    public void FormatValue_FormatsBooleanAsYesNo()
    {
        Assert.Equal("Yes", _formatter.FormatValue(true));
        Assert.Equal("No", _formatter.FormatValue(false));
    }

    [Fact]
    public void FormatValue_ReturnsEmptyStringForNull()
    {
        Assert.Equal(string.Empty, _formatter.FormatValue(null));
    }

    [Fact]
    public void FormatValue_FormatsPercentageWithTwoDecimals()
    {
        var column = new ReportColumn { Type = ReportColumnType.Percentage };

        var result = _formatter.FormatValue(12.3456, column);

        Assert.Equal("12.35", result);
    }
}
