namespace CraneOptimizer.UI.Features.F13_AiAdvisor.Workflow
{
    public sealed class WorkflowReadinessSnapshot
    {
        public string ActiveScreen { get; init; } = string.Empty;
        public string CurrentStage { get; init; } = string.Empty;
        public string Summary { get; init; } = string.Empty;
        public double CompletionRatio { get; init; }
        public int CompleteCount { get; init; }
        public int TotalCount { get; init; }
        public int MissingCriticalCount { get; init; }
        public IReadOnlyList<WorkflowReadinessItem> Items { get; init; } = Array.Empty<WorkflowReadinessItem>();
    }
}
