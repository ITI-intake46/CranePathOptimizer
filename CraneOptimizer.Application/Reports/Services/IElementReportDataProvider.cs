using CraneOptimizer.Application.Reports.DTOs;
using CraneOptimizer.Domain.Schemas;

namespace CraneOptimizer.Application.Reports.Services;

public interface IElementReportDataProvider
{
    IReadOnlyList<ElementReportRowDto> BuildRows(
        string templateName,
        IEnumerable<PrecastElement> scopedElements);
}
