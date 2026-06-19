using CraneOptimizer.Application.Reports.Models;

namespace CraneOptimizer.Application.Reports.Interfaces;

public interface IReportDefinitionProvider
{
    IReadOnlyList<ReportDefinition> GetDefinitions();
}
