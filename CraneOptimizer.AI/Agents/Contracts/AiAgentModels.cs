using CraneOptimizer.AI.Knowledge.Rag;

namespace CraneOptimizer.AI.Agents.Contracts
{
    public enum AiToolAccessMode
    {
        ReadOnly,
        Mutating
    }

    public enum AiAgentRunStatus
    {
        Planned,
        Running,
        Succeeded,
        PartiallySucceeded,
        ApprovalRequired,
        Failed,
        Cancelled
    }

    public enum AiAgentStepStatus
    {
        Planned,
        Running,
        Succeeded,
        Failed,
        Skipped,
        ApprovalRequired
    }

    public sealed class AiToolDefinition
    {
        public string Name { get; init; } = string.Empty;
        public string Description { get; init; } = string.Empty;
        public string InputSchema { get; init; } = "{}";
        public string OutputSchema { get; init; } = "{}";
        public AiToolAccessMode AccessMode { get; init; } = AiToolAccessMode.ReadOnly;
        public string PermissionLevel { get; init; } = "ProjectRead";
        public TimeSpan Timeout { get; init; } = TimeSpan.FromSeconds(10);
        public bool RequiresApproval => AccessMode == AiToolAccessMode.Mutating;
    }

    public sealed class AiAgentPlanStep
    {
        public int Sequence { get; init; }
        public string ToolName { get; init; } = string.Empty;
        public string Purpose { get; init; } = string.Empty;
        public string InputJson { get; init; } = "{}";
        public bool RequiresApproval { get; init; }
    }

    public sealed class AiAgentTraceEntry
    {
        public int Sequence { get; init; }
        public string ToolName { get; init; } = string.Empty;
        public string Purpose { get; init; } = string.Empty;
        public AiAgentStepStatus Status { get; init; }
        public string Summary { get; init; } = string.Empty;
        public string Governance { get; init; } = string.Empty;
        public long DurationMilliseconds { get; init; }

        public string StatusLabel => Status switch
        {
            AiAgentStepStatus.Succeeded => "Succeeded",
            AiAgentStepStatus.Failed => "Failed",
            AiAgentStepStatus.Skipped => "Skipped",
            AiAgentStepStatus.ApprovalRequired => "Approval required",
            AiAgentStepStatus.Running => "Running",
            _ => "Planned"
        };

        public string DurationLabel => DurationMilliseconds <= 0
            ? string.Empty
            : $"{DurationMilliseconds:N0} ms";
    }

    public sealed class AiToolExecutionResult
    {
        public string ToolName { get; init; } = string.Empty;
        public bool Succeeded { get; init; }
        public string Summary { get; init; } = string.Empty;
        public string Evidence { get; init; } = string.Empty;
        public IReadOnlyDictionary<string, string> Facts { get; init; } = new Dictionary<string, string>();
        public IReadOnlyList<string> RecommendedActions { get; init; } = Array.Empty<string>();
        public IReadOnlyList<AiKnowledgeCitation> Citations { get; init; } = Array.Empty<AiKnowledgeCitation>();
        public string Error { get; init; } = string.Empty;
    }

    public sealed class AiAgentRunSnapshot
    {
        public string RunId { get; init; } = Guid.NewGuid().ToString("N");
        public string AgentName { get; init; } = "Project Readiness Agent";
        public string Goal { get; init; } = string.Empty;
        public AiAgentRunStatus Status { get; init; } = AiAgentRunStatus.Planned;
        public DateTimeOffset StartedAt { get; init; } = DateTimeOffset.UtcNow;
        public DateTimeOffset CompletedAt { get; init; } = DateTimeOffset.UtcNow;
        public long ModelRevision { get; init; }
        public string ModelFingerprint { get; init; } = "model:none";
        public string ActiveScreen { get; init; } = string.Empty;
        public IReadOnlyList<AiAgentPlanStep> Plan { get; init; } = Array.Empty<AiAgentPlanStep>();
        public IReadOnlyList<AiAgentTraceEntry> Trace { get; init; } = Array.Empty<AiAgentTraceEntry>();
        public IReadOnlyList<AiToolExecutionResult> ToolResults { get; init; } = Array.Empty<AiToolExecutionResult>();
        public string Conclusion { get; init; } = string.Empty;
        public IReadOnlyList<string> RecommendedActions { get; init; } = Array.Empty<string>();
        public IReadOnlyList<AiKnowledgeCitation> Citations { get; init; } = Array.Empty<AiKnowledgeCitation>();
        public string GovernanceSummary { get; init; } = string.Empty;
        public string VisibleAnswer { get; init; } = string.Empty;

        public bool HasTrace => Trace.Count > 0;
        public bool HasFailures => Trace.Any(entry => entry.Status == AiAgentStepStatus.Failed);
        public bool RequiresApproval => Trace.Any(entry => entry.Status == AiAgentStepStatus.ApprovalRequired);
        public int SuccessfulToolCount => Trace.Count(entry => entry.Status == AiAgentStepStatus.Succeeded);
        public int PlannedToolCount => Plan.Count;
        public string StatusLabel => Status switch
        {
            AiAgentRunStatus.Succeeded => "Completed",
            AiAgentRunStatus.PartiallySucceeded => "Completed with warnings",
            AiAgentRunStatus.ApprovalRequired => "Awaiting approval",
            AiAgentRunStatus.Failed => "Failed",
            AiAgentRunStatus.Cancelled => "Cancelled",
            AiAgentRunStatus.Running => "Running",
            _ => "Planned"
        };

        public static AiAgentRunSnapshot Empty { get; } = new()
        {
            RunId = string.Empty,
            AgentName = "Project Readiness Agent",
            Goal = "No agent run has been executed yet.",
            Status = AiAgentRunStatus.Planned,
            StartedAt = DateTimeOffset.MinValue,
            CompletedAt = DateTimeOffset.MinValue,
            GovernanceSummary = "No tools have been invoked."
        };
    }
}
