namespace CraneOptimizer.AI.Host
{
    /// <summary>
    /// UI-neutral context contributed by one feature surface. These snapshots are
    /// deterministic evidence packs that an LLM may explain but must not override.
    /// </summary>
    public sealed class AiScreenContextSnapshot
    {
        public string ScreenName { get; init; } = string.Empty;
        public string Stage { get; init; } = string.Empty;
        public string Summary { get; init; } = string.Empty;
        public IReadOnlyList<AiContextMetricSnapshot> Metrics { get; init; } = Array.Empty<AiContextMetricSnapshot>();
        public IReadOnlyList<AiContextIssueSnapshot> Issues { get; init; } = Array.Empty<AiContextIssueSnapshot>();
        public IReadOnlyList<AiContextActionHintSnapshot> ActionHints { get; init; } = Array.Empty<AiContextActionHintSnapshot>();
        public IReadOnlyDictionary<string, string> Facts { get; init; } = new Dictionary<string, string>();
    }

    public sealed class AiContextMetricSnapshot
    {
        public string Name { get; init; } = string.Empty;
        public string Value { get; init; } = string.Empty;
        public string Detail { get; init; } = string.Empty;
    }

    public sealed class AiContextIssueSnapshot
    {
        public string Code { get; init; } = string.Empty;
        public string Severity { get; init; } = "Info";
        public string Area { get; init; } = string.Empty;
        public string Evidence { get; init; } = string.Empty;
        public string Recommendation { get; init; } = string.Empty;
        public string NextClick { get; init; } = string.Empty;
        public int AffectedCount { get; init; }
        public string Confidence { get; init; } = "High";
    }

    public sealed class AiContextActionHintSnapshot
    {
        public string Label { get; init; } = string.Empty;
        public string ActionType { get; init; } = "NavigateTab";
        public string Target { get; init; } = string.Empty;
        public string Reason { get; init; } = string.Empty;
        public string Risk { get; init; } = "Low — user-confirmed navigation or text export only.";
    }
}
