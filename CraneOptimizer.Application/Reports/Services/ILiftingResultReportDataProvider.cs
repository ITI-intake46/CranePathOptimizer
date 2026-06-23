using CraneOptimizer.Application.Reports.DTOs;

namespace CraneOptimizer.Application.Reports.Services;

public interface ILiftingResultReportDataProvider
{
    IReadOnlyList<LiftingResultReportRowDto> BuildRows(
        string templateName,
        string itemsIncluded,
        IEnumerable<LiftingResultSourceRowDto> sourceRows);
}
