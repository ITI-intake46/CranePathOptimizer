namespace CraneOptimizer.AI.Knowledge.Rag
{
    public sealed class AiGroundingPacket
    {
        public static AiGroundingPacket Empty { get; } = new();

        public string Query { get; init; } = string.Empty;
        public string ProjectKey { get; init; } = "global";
        public string ModelFingerprint { get; init; } = "model:none";
        public long ModelRevision { get; init; }
        public string PromptContext { get; init; } = string.Empty;
        public IReadOnlyList<AiKnowledgeCitation> Citations { get; init; } = Array.Empty<AiKnowledgeCitation>();
        public int IndexedDocumentCount { get; init; }
        public int IndexedChunkCount { get; init; }

        public bool HasEvidence => Citations.Count > 0;
        public string CoverageLabel => HasEvidence
            ? $"{Citations.Count} cited source(s) from {IndexedDocumentCount} live project document(s)"
            : "No relevant live project evidence retrieved";
    }
}
