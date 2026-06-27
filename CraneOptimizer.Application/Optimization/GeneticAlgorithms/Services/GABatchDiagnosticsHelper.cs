using CraneOptimizer.Application.Optimization.GeneticAlgorithms.DTOs;
using System.Globalization;

namespace CraneOptimizer.Application.Optimization.GeneticAlgorithms.Services
{
    public static class GABatchDiagnosticsHelper
    {
        public static readonly string[] HeaderPrefix = new[]
        {
            "ExperimentBatchId", "ExperimentName", "ProjectName", "CraneName",
            "ParameterSetId", "ParameterSetName", "RunIndex", "RandomSeed",
            "OptimizationRunId", "StageRunId", "StageIndex", "DiagnosticsSchemaVersion"
        };

        public static string[] GetRowPrefix(PlacementInput input, bool isStageFile, string schemaVersion = "1.0")
        {
            var inv = CultureInfo.InvariantCulture;
            return new[]
            {
                input.Settings?.ExperimentBatchId ?? "N/A",
                input.Settings?.ExperimentName ?? "N/A",
                input.ProjectName ?? "N/A",
                input.CraneSpec?.Crane?.Name ?? "N/A",
                input.Settings?.ParameterSetId ?? "N/A",
                input.Settings?.ParameterSetName ?? "N/A",
                input.Settings?.RunIndex?.ToString(inv) ?? "N/A",
                input.Settings?.RandomSeed?.ToString(inv) ?? "N/A",
                input.Settings?.OptimizationRunId ?? "N/A",
                isStageFile ? (input.Settings?.StageRunId ?? "N/A") : "N/A",
                isStageFile ? (input.Settings?.StageIndex.ToString(inv) ?? "1") : "N/A",
                schemaVersion
            };
        }
    }
}
