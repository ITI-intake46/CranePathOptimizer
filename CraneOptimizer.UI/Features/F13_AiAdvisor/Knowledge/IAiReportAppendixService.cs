namespace CraneOptimizer.UI.Features.F13_AiAdvisor.Knowledge
{
    public interface IAiReportAppendixService
    {
        string BuildAppendixMarkdown(string question, string answer, string model, string contextPreview);
    }
}
