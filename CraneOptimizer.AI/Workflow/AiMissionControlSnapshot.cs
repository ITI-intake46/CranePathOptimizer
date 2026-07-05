namespace CraneOptimizer.AI.Workflow
{
    /// <summary>
    /// Product-facing AI Mission Control state. This is intentionally deterministic:
    /// it turns the current crane-planning evidence into a diagnosis, recovery plan,
    /// report wording, and what-if advisory surface that are visible immediately.
    /// </summary>
    public sealed class AiMissionControlSnapshot
    {
        public string DiagnosisTitle { get; init; } = "AI Mission Control";
        public string DiagnosisStatus { get; init; } = "Ready";
        public string RootCause { get; init; } = string.Empty;
        public string Evidence { get; init; } = string.Empty;
        public string RecommendedAction { get; init; } = string.Empty;
        public string Confidence { get; init; } = "Medium";
        public string Risk { get; init; } = string.Empty;
        public string RecoveryPlan { get; init; } = string.Empty;
        public string WhatIfAdvisor { get; init; } = string.Empty;
        public string ReportNarrative { get; init; } = string.Empty;
        public IReadOnlyList<AiMissionControlInsight> Insights { get; init; } = Array.Empty<AiMissionControlInsight>();
    }
}
