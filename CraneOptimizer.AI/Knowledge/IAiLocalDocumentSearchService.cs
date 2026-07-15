namespace CraneOptimizer.AI.Knowledge
{
    public interface IAiLocalDocumentSearchService
    {
        IReadOnlyList<AiDocumentSearchResult> Search(string query, int maxResults = 5);
    }
}
