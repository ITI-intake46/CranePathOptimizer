namespace CraneOptimizer.AI.Workflow
{
    /// <summary>
    /// One-click prompt chip that lets evaluators immediately see the AI behaving
    /// as an engineering-aware copilot without hunting for a good question.
    /// </summary>
    public sealed class AiShowcasePrompt
    {
        public string Label { get; init; } = string.Empty;
        public string Question { get; init; } = string.Empty;
        public string WhyItShowsAiPower { get; init; } = string.Empty;
    }
}
