namespace CraneOptimizer.AI.Knowledge.Rag
{
    /// <summary>
    /// AI-owned, immutable knowledge document projected from host snapshots. The AI
    /// project owns this contract so retrieval never depends on UI/domain assemblies.
    /// </summary>
    public sealed class AiKnowledgeDocument
    {
        public string Id { get; init; } = string.Empty;
        public string Title { get; init; } = string.Empty;
        public string SourceType { get; init; } = "ProjectSnapshot";
        public string SourceLocator { get; init; } = string.Empty;
        public string Content { get; init; } = string.Empty;
        public string TrustLevel { get; init; } = "Deterministic";
        public long ModelRevision { get; init; }
        public IReadOnlyDictionary<string, string> Metadata { get; init; } = new Dictionary<string, string>();
    }
}
