using CraneOptimizer.Application.Optimization.GeneticAlgorithms.DTOs;

namespace CraneOptimizer.Application.Optimization.GeneticAlgorithms.Interfaces
{
    /// <summary>
    /// Contract for generating per-project GA CSV diagnostics (run history, detailed failure logs, failure breakdowns).
    /// </summary>
    public interface IGACsvDiagnosticsService
    {
        /// <summary>
        /// Runs a post-run deterministic diagnostic check on all selected elements, generates report CSVs, 
        /// persists them inside the designated folder, and returns the path to the detailed diagnostics report.
        /// </summary>
        /// <param name="input">GA input containing elements, specs, settings, and metadata.</param>
        /// <param name="finalResult">Final GA placement outcome.</param>
        /// <returns>Absolute path to the newly written detailed diagnostics CSV file.</returns>
        string GenerateDiagnostics(PlacementInput input, PlacementResult finalResult);
    }
}
