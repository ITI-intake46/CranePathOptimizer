using System.Collections.Generic;
using CraneOptimizer.Domain.Diagnostics;
using CraneOptimizer.Domain.Schemas;

namespace CraneOptimizer.Application.Services
{
    public interface IImportReadinessService
    {
        ImportReadinessSummary BuildSummary(IEnumerable<PrecastElement> elements, bool isCheckerEnabled = true);
    }
}
