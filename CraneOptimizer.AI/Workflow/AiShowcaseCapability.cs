namespace CraneOptimizer.AI.Workflow
{
    /// <summary>
    /// Visible AI capability tile used by the in-shell showcase. It is deterministic
    /// product copy generated from live workflow evidence, not an autonomous action.
    /// </summary>
    public sealed class AiShowcaseCapability
    {
        public string Title { get; init; } = string.Empty;
        public string Badge { get; init; } = "AI";
        public string Purpose { get; init; } = string.Empty;
        public string Evidence { get; init; } = string.Empty;
        public string VisiblePower { get; init; } = string.Empty;
        public string DemoPrompt { get; init; } = string.Empty;
        public string RiskBoundary { get; init; } = "Advisory only; deterministic engineering checks remain authoritative.";
    }
}
