namespace CraneOptimizer.AI.Agents.Contracts
{
    public sealed class AiAgentDraftArtifactRequest
    {
        public string RunId { get; init; } = string.Empty;
        public string ProjectName { get; init; } = "Project";
        public string FileStem { get; init; } = "readiness-remediation";
        public string Title { get; init; } = "Project readiness remediation draft";
        public string Content { get; init; } = string.Empty;
    }

    public sealed class AiAgentDraftArtifactResult
    {
        public bool Succeeded { get; init; }
        public string ArtifactPath { get; init; } = string.Empty;
        public string ArtifactKind { get; init; } = "Markdown draft";
        public long SizeBytes { get; init; }
        public string Sha256 { get; init; } = string.Empty;
        public string Error { get; init; } = string.Empty;
    }
}
