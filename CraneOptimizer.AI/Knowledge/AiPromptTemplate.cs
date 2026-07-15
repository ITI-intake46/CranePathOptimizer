namespace CraneOptimizer.AI.Knowledge
{
    public sealed class AiPromptTemplate
    {
        public string Key { get; init; } = string.Empty;
        public string Title { get; init; } = string.Empty;
        public string AppliesWhen { get; init; } = string.Empty;
        public string Prompt { get; init; } = string.Empty;
    }
}
