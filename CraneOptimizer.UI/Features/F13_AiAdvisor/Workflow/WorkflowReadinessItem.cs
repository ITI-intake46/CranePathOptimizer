namespace CraneOptimizer.UI.Features.F13_AiAdvisor.Workflow
{
    public sealed class WorkflowReadinessItem
    {
        public string Name { get; init; } = string.Empty;
        public bool IsComplete { get; init; }
        public bool IsCritical { get; init; }
        public string Severity { get; init; } = "Info";
        public string Evidence { get; init; } = string.Empty;
        public string Recommendation { get; init; } = string.Empty;
        public string NextClick { get; init; } = string.Empty;
    }
}
