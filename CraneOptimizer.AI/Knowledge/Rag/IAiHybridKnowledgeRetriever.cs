namespace CraneOptimizer.AI.Knowledge.Rag
{
    public interface IAiHybridKnowledgeRetriever
    {
        IReadOnlyList<AiKnowledgeCitation> Retrieve(
            IReadOnlyList<AiKnowledgeDocument> documents,
            string query,
            int maxResults = 6);
    }
}
