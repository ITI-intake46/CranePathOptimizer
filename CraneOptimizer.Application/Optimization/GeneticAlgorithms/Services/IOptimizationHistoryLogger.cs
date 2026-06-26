using System.Threading.Tasks;

namespace CraneOptimizer.Application.Optimization.GeneticAlgorithms.Services;

public interface IOptimizationHistoryLogger
{
    /// <summary>
    /// Appends one run entry to the project's persistent optimization history file.
    /// Creates the file with a header row if it does not exist.
    /// Never overwrites existing rows.
    /// </summary>
    /// <param name="entry">The run data to append.</param>
    /// <param name="historyDirectory">
    /// Absolute path to the directory where <c>optimization_history.csv</c> is written.
    /// Must be a pre-resolved path (e.g. from <c>IProjectOutputPathService.GetGaHistoryDirectory()</c>).
    /// </param>
    Task LogRunAsync(OptimizationRunEntry entry, string historyDirectory);
}
