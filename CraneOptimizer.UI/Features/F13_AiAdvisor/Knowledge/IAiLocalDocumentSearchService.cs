namespace CraneOptimizer.UI.Features.F13_AiAdvisor.Knowledge
{
    public interface IAiLocalDocumentSearchService
    {
        IReadOnlyList<AiDocumentSearchResult> Search(string query, int maxResults = 5);
    }
}
