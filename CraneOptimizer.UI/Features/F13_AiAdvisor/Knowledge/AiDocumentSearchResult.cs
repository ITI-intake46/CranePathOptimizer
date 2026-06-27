namespace CraneOptimizer.UI.Features.F13_AiAdvisor.Knowledge
{
    public sealed class AiDocumentSearchResult
    {
        public string Title { get; init; } = string.Empty;
        public string RelativePath { get; init; } = string.Empty;
        public string Snippet { get; init; } = string.Empty;
        public int Score { get; init; }
    }
}
