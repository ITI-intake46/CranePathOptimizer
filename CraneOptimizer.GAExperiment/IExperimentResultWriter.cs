using System.Collections.Generic;
using System.Threading.Tasks;

namespace CraneOptimizer.GAExperiment
{
    public interface IExperimentResultWriter
    {
        Task WriteResultAsync(ExperimentRunResult result, string csvPath);
        Task<HashSet<string>> ReadExistingRunIdsAsync(string csvPath);
        Task WriteSummaryAsync(IEnumerable<ExperimentRunResult> results, string summaryPath);
    }
}
