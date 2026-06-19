using System.Globalization;
using CraneOptimizer.Application.Reports.Interfaces;
using CraneOptimizer.Application.Reports.Models;

namespace CraneOptimizer.Application.Reports.Services;

public sealed class ReportValueFormatter : IReportValueFormatter
{
    public string FormatValue(object? value, ReportColumn? column = null)
    {
        if (value == null)
            return string.Empty;

        return value switch
        {
            double d => FormatDouble(d, column),
            float f => FormatDouble(f, column),
            decimal m => FormatDecimal(m, column),
            DateTime dt => dt.ToUniversalTime().ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
            bool b => b ? "Yes" : "No",
            IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
            _ => value.ToString() ?? string.Empty
        };
    }

    private static string FormatDouble(double value, ReportColumn? column)
    {
        if (double.IsNaN(value) || double.IsInfinity(value))
            return string.Empty;

        if (column?.Type == ReportColumnType.Percentage)
            return value.ToString("0.##", CultureInfo.InvariantCulture);

        return value.ToString("0.###", CultureInfo.InvariantCulture);
    }

    private static string FormatDecimal(decimal value, ReportColumn? column)
    {
        if (column?.Type == ReportColumnType.Percentage)
            return value.ToString("0.##", CultureInfo.InvariantCulture);

        return value.ToString("0.###", CultureInfo.InvariantCulture);
    }
}
