namespace CraneOptimizer.AI.Workflow
{
    /// <summary>
    /// A visible, deterministic AI insight shown in Mission Control. These cards are
    /// generated from live application evidence and are safe for the AI to explain,
    /// copy, or use as prompt context. They do not mutate engineering state.
    /// </summary>
    public sealed class AiMissionControlInsight
    {
        public string Title { get; init; } = string.Empty;
        public string Status { get; init; } = "Info";
        public string Finding { get; init; } = string.Empty;
        public string Evidence { get; init; } = string.Empty;
        public string WhyItMatters { get; init; } = string.Empty;
        public string NextAction { get; init; } = string.Empty;
        public string Risk { get; init; } = string.Empty;
        public string Confidence { get; init; } = "Medium";
    }
}
