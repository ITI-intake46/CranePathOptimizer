namespace CraneOptimizer.AI.Knowledge.Rag
{
    public sealed class AiKnowledgeCitation
    {
        public string Id { get; init; } = string.Empty;
        public string SourceId { get; init; } = string.Empty;
        public string SourceLabel { get; init; } = string.Empty;
        public string SourceType { get; init; } = string.Empty;
        public string SourceLocator { get; init; } = string.Empty;
        public string Snippet { get; init; } = string.Empty;
        public double Score { get; init; }
        public string Confidence { get; init; } = "Medium";
        public string TrustLevel { get; init; } = "Deterministic";
        public long ModelRevision { get; init; }

        public string ScoreLabel => $"Relevance {Score:P0} · {TrustLevel} · {Confidence} confidence";
    }
}
