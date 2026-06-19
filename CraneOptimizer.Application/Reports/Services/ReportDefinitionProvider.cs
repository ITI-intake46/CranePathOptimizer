using CraneOptimizer.Application.Reports.Interfaces;
using CraneOptimizer.Application.Reports.Models;

namespace CraneOptimizer.Application.Reports.Services;

public sealed class ReportDefinitionProvider : IReportDefinitionProvider
{
    private static readonly IReadOnlyList<ReportDefinition> Definitions =
    [
        new ReportDefinition
        {
            ReportId = "optimization-summary",
            DisplayName = "Optimization Summary Report",
            Description = "Summarizes crane optimization results, setup positions, failures, and element lift details.",
            SupportedFormats = [ReportFormat.Csv, ReportFormat.Pdf]
        }
    ];

    public IReadOnlyList<ReportDefinition> GetDefinitions()
    {
        return Definitions;
    }
}
