namespace CraneOptimizer.AI.Knowledge
{
    public interface IAiReportAppendixService
    {
        string BuildAppendixMarkdown(string question, string answer, string model, string contextPreview);
    }
}
