namespace CraneOptimizer.AI.Knowledge.Rag
{
    public sealed class AiKnowledgeChunk
    {
        public string Id { get; init; } = string.Empty;
        public AiKnowledgeDocument Document { get; init; } = new();
        public int Ordinal { get; init; }
        public string Text { get; init; } = string.Empty;
    }
}
