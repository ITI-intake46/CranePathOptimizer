using CraneOptimizer.AI.Host;
using CraneOptimizer.Domain.Diagnostics;

namespace CraneOptimizer.UI.AIIntegration.Contributors
{
    internal static class AiContextContributorHelpers
    {
        public static AiContextMetricSnapshot Metric(string name, string value, string detail = "") => new()
        {
            Name = name,
            Value = value,
            Detail = detail
        };

        public static AiContextIssueSnapshot Issue(string code, string severity, string area, string evidence, string recommendation, string nextClick, int affectedCount = 0, string confidence = "High") => new()
        {
            Code = code,
            Severity = severity,
            Area = area,
            Evidence = evidence,
            Recommendation = recommendation,
            NextClick = nextClick,
            AffectedCount = affectedCount,
            Confidence = confidence
        };

        public static AiContextIssueSnapshot FromValidationMessage(ValidationMessage message, string nextClick) => new()
        {
            Code = message.Code,
            Severity = string.IsNullOrWhiteSpace(message.Severity) ? "Info" : message.Severity,
            Area = string.IsNullOrWhiteSpace(message.Category) ? "Readiness" : message.Category!,
            Evidence = message.AffectedCount > 0
                ? $"{message.Message} Affected: {message.AffectedCount:N0}."
                : message.Message,
            Recommendation = message.RecommendedAction ?? "Review deterministic diagnostics before proceeding.",
            NextClick = nextClick,
            AffectedCount = message.AffectedCount,
            Confidence = "High"
        };

        public static AiContextActionHintSnapshot Hint(string label, string target, string reason, string actionType = "NavigateTab", string risk = "Low — navigation only; no project data is changed.") => new()
        {
            Label = label,
            ActionType = actionType,
            Target = target,
            Reason = reason,
            Risk = risk
        };
    }
}
