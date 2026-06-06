using ProjectContext = CraneOptimizer.Application.Optimization.GeneticAlgorithms.DTOs.PlacementInput;
using CraneModel = CraneOptimizer.Domain.Entities.Crane;
using GAResult = CraneOptimizer.Application.Optimization.GeneticAlgorithms.DTOs.PlacementResult;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Settings;

namespace CraneOptimizer.Application.Optimization.GeneticAlgorithms.Interfaces
{
    /// <summary>
    /// Interface for executing multi-stage GA runs with dynamic position scaling and relocation weighting.
    /// </summary>
    public interface IMultiStageGAOrchestrator
    {
        Task<MultiStageGAResult> RunAsync(
            ProjectContext project,
            CraneModel crane,
            GASettings baseSettings,
            double coverageTarget,
            CancellationToken cancellationToken);
    }

    /// <summary>
    /// Represents the overall results of a multi-stage discovery execution.
    /// </summary>
    public record MultiStageGAResult(
        GAResult BestResult,
        int StagesExecuted,
        IReadOnlyList<GAResult> AllStageResults,
        string TerminationReason
    );
}
