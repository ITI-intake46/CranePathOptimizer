namespace CraneOptimizer.AI.Agents.Contracts
{
    public enum AiAgentActionExecutionStatus
    {
        Planned,
        ApprovalRequired,
        Succeeded,
        Failed,
        Rejected,
        Cancelled
    }

    public sealed class AiAgentActionToolDefinition
    {
        public string Name { get; init; } = string.Empty;
        public string Title { get; init; } = string.Empty;
        public string Description { get; init; } = string.Empty;
        public string ActionType { get; init; } = "NavigateTab";
        public string Target { get; init; } = string.Empty;
        public string ButtonLabel { get; init; } = "Open";
        public string InputSchema { get; init; } = "{}";
        public AiToolAccessMode AccessMode { get; init; } = AiToolAccessMode.Navigation;
        public string PermissionLevel { get; init; } = "ProjectNavigate";
        public string Risk { get; init; } = "Low — navigation only; no project data is changed.";
        public string Impact { get; init; } = "No project or engineering state is changed.";
        public string Reversibility { get; init; } = "No persistent change is created.";
        public TimeSpan Timeout { get; init; } = TimeSpan.FromSeconds(5);
        public bool RequiresExplicitApproval => AccessMode == AiToolAccessMode.Mutating;
    }

    public sealed class AiAgentActionProposal
    {
        public string ProposalId { get; init; } = Guid.NewGuid().ToString("N");
        public string RunId { get; init; } = string.Empty;
        public string ToolName { get; init; } = string.Empty;
        public string Title { get; init; } = string.Empty;
        public string Description { get; init; } = string.Empty;
        public string ActionType { get; init; } = "NavigateTab";
        public string Target { get; init; } = string.Empty;
        public string ButtonLabel { get; init; } = "Open";
        public string Risk { get; init; } = string.Empty;
        public string Impact { get; init; } = string.Empty;
        public string Reversibility { get; init; } = string.Empty;
        public string InputJson { get; init; } = "{}";
        public AiToolAccessMode AccessMode { get; init; } = AiToolAccessMode.Navigation;
        public string PermissionLevel { get; init; } = "ProjectNavigate";
        public IReadOnlyList<string> EvidenceIds { get; init; } = Array.Empty<string>();
        public long ModelRevision { get; init; }
        public string ModelFingerprint { get; init; } = "model:none";
        public bool RequiresExplicitApproval => AccessMode == AiToolAccessMode.Mutating;
        public string ApprovalLabel => RequiresExplicitApproval ? "Approve once" : ButtonLabel;
        public string EvidenceLabel => EvidenceIds.Count == 0
            ? "No evidence references attached."
            : "Evidence: " + string.Join(", ", EvidenceIds.Select(id => $"[{id}]"));
        public string ApprovalScope => RequiresExplicitApproval
            ? "One-time approval scope: execute this proposal once for the captured agent run and model revision."
            : "User-invoked navigation only.";
        public string ToolArgumentsLabel =>
            $"Tool: {ToolName} · Action: {ActionType} · Target: {Target} · Permission: {PermissionLevel} · Input: {InputJson}";
    }

    public sealed class AiAgentActionContext
    {
        public required AiAgentRunSnapshot AgentRun { get; init; }
        public required AiAgentActionProposal Proposal { get; init; }
        public bool UserApproved { get; init; }
        public string InitiatedBy { get; init; } = "User";
    }

    public sealed class AiAgentActionToolResult
    {
        public bool Succeeded { get; init; }
        public string Summary { get; init; } = string.Empty;
        public string Error { get; init; } = string.Empty;
        public string ArtifactPath { get; init; } = string.Empty;
        public string ArtifactKind { get; init; } = string.Empty;
        public string ArtifactSha256 { get; init; } = string.Empty;
        public long ArtifactSizeBytes { get; init; }
        public string Governance { get; init; } = string.Empty;
    }

    public sealed class AiAgentActionExecutionSnapshot
    {
        public string AuditId { get; init; } = Guid.NewGuid().ToString("N");
        public string RunId { get; init; } = string.Empty;
        public string ProposalId { get; init; } = string.Empty;
        public string ToolName { get; init; } = string.Empty;
        public string ActionType { get; init; } = string.Empty;
        public string Target { get; init; } = string.Empty;
        public AiAgentActionExecutionStatus Status { get; init; } = AiAgentActionExecutionStatus.Planned;
        public DateTimeOffset StartedAt { get; init; } = DateTimeOffset.UtcNow;
        public DateTimeOffset CompletedAt { get; init; } = DateTimeOffset.UtcNow;
        public long DurationMilliseconds { get; init; }
        public bool UserInitiated { get; init; }
        public bool ExplicitApproval { get; init; }
        public string InitiatedBy { get; init; } = "User";
        public long ModelRevision { get; init; }
        public string ModelFingerprint { get; init; } = "model:none";
        public IReadOnlyList<string> EvidenceIds { get; init; } = Array.Empty<string>();
        public string Summary { get; init; } = string.Empty;
        public string Governance { get; init; } = string.Empty;
        public string ArtifactPath { get; init; } = string.Empty;
        public string ArtifactKind { get; init; } = string.Empty;
        public string ArtifactSha256 { get; init; } = string.Empty;
        public long ArtifactSizeBytes { get; init; }
        public bool HasArtifact => !string.IsNullOrWhiteSpace(ArtifactPath);
        public string ArtifactLabel => HasArtifact
            ? $"{ArtifactKind}: {ArtifactPath}"
            : string.Empty;
        public string ArtifactIntegrityLabel => HasArtifact
            ? $"{ArtifactSizeBytes:N0} byte(s) · SHA-256 {ShortHash(ArtifactSha256)}"
            : string.Empty;
        public string AuditMetadataLabel =>
            $"User: {InitiatedBy} · approval: {(ExplicitApproval ? "explicit" : "none")} · model rev {ModelRevision} · " +
            $"evidence: {(EvidenceIds.Count == 0 ? "none" : string.Join(", ", EvidenceIds.Select(id => $"[{id}]")))} · {DurationMilliseconds:N0} ms";

        public string StatusLabel => Status switch
        {
            AiAgentActionExecutionStatus.Succeeded => "Succeeded",
            AiAgentActionExecutionStatus.ApprovalRequired => "Approval required",
            AiAgentActionExecutionStatus.Rejected => "Rejected",
            AiAgentActionExecutionStatus.Failed => "Failed",
            AiAgentActionExecutionStatus.Cancelled => "Cancelled",
            _ => "Planned"
        };

        public string AuditLabel => string.IsNullOrWhiteSpace(AuditId)
            ? StatusLabel
            : $"{StatusLabel} · audit {AuditId[..Math.Min(8, AuditId.Length)]}";

        private static string ShortHash(string value)
            => string.IsNullOrWhiteSpace(value)
                ? "not recorded"
                : value[..Math.Min(16, value.Length)];
    }
}
