namespace CraneOptimizer.AI.Workflow
{
    /// <summary>
    /// Aggregates the high-visibility AI story shown in the companion panel: agent
    /// stack, decision trace, demo prompts, and a copyable presentation brief.
    /// </summary>
    public sealed class AiShowcaseSnapshot
    {
        public string HeroHeadline { get; init; } = "AI Studio: live crane-planning intelligence";
        public string HeroSubheadline { get; init; } = string.Empty;
        public string PowerScoreLabel { get; init; } = "AI visibility: warming up";
        public string AgentStackSummary { get; init; } = string.Empty;
        public string Narrative { get; init; } = string.Empty;
        public string PresentationBrief { get; init; } = string.Empty;
        public IReadOnlyList<AiShowcaseCapability> Capabilities { get; init; } = Array.Empty<AiShowcaseCapability>();
        public IReadOnlyList<AiShowcasePrompt> DemoPrompts { get; init; } = Array.Empty<AiShowcasePrompt>();
        public IReadOnlyList<string> DecisionTrace { get; init; } = Array.Empty<string>();
    }
}
