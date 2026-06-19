using CraneOptimizer.Application.Reports.Models;

namespace CraneOptimizer.Application.Reports.Interfaces;

public interface IReportBuilder<in TInput>
{
    string ReportId { get; }

    ReportDocument Build(TInput input);
}
