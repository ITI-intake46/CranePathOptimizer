using CraneOptimizer.Application.Reports.Models;

namespace CraneOptimizer.Application.Reports.Interfaces;

public interface IReportValueFormatter
{
    string FormatValue(object? value, ReportColumn? column = null);
}
