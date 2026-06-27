using CraneOptimizer.Domain.Schemas;

namespace CraneOptimizer.Application.Reports.Services;

public interface IReportableElementResolver
{
    IReadOnlyList<PrecastElement> ResolveReportableElements(IEnumerable<PrecastElement> sourceElements);
}
