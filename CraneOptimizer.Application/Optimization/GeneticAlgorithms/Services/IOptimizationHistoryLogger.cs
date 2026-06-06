using System.Threading.Tasks;

namespace CraneOptimizer.Application.Optimization.GeneticAlgorithms.Services;

public interface IOptimizationHistoryLogger
{
    /// <summary>
    /// Appends one run entry to the project's persistent history file.
    /// Creates the file with a header row if it does not exist.
    /// Never overwrites existing rows.
    /// </summary>
    Task LogRunAsync(OptimizationRunEntry entry, string projectFolderPath);
}
