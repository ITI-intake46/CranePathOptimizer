namespace CraneOptimizer.AI.Host
{
    /// <summary>
    /// UI-neutral snapshot of the current crane-planning workflow. The host UI is
    /// responsible for translating its live view models into this DTO.
    /// </summary>
    public sealed class AiWorkflowContextSnapshot
    {
        public string ProjectKey { get; init; } = "global";
        public string ProjectDisplayName { get; init; } = "Global";
        public bool HasProject { get; init; }
        public string CurrentProjectName { get; init; } = string.Empty;

        public int ElementCount { get; init; }
        public int ZoneCount { get; init; }
        public int CraneCount { get; init; }
        public bool HasLoadChartEvidence { get; init; }

        public AiOptimizationResultSnapshot OptimizationResult { get; init; } = new();
    }
}
