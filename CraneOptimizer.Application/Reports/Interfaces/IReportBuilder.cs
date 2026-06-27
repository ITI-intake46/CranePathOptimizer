using CraneOptimizer.Application.Reports.Models;

namespace CraneOptimizer.Application.Reports.Interfaces;

[System.Obsolete("Unused legacy headless reporting foundation. UI uses UserFacingReportBuilder instead.")]
public interface IReportBuilder<in TInput>
{
    string ReportId { get; }

    ReportDocument Build(TInput input);
}
