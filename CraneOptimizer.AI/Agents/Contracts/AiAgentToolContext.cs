using CraneOptimizer.AI.Host;
using CraneOptimizer.AI.Workflow;

namespace CraneOptimizer.AI.Agents.Contracts
{
    public sealed class AiAgentToolContext
    {
        public required AiWorkflowContextSnapshot Workflow { get; init; }
        public required WorkflowReadinessSnapshot Readiness { get; init; }
        public string Question { get; init; } = string.Empty;
        public string ActiveScreen { get; init; } = string.Empty;
        public string NextAction { get; init; } = string.Empty;
        public string InputJson { get; init; } = "{}";
    }
}
