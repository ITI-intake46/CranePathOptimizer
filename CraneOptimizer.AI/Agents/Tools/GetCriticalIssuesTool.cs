using CraneOptimizer.AI.Agents.Contracts;

namespace CraneOptimizer.AI.Agents.Tools
{
    public sealed class GetCriticalIssuesTool : AiProjectToolBase
    {
        public override AiToolDefinition Definition { get; } = new()
        {
            Name = "get_critical_issues",
            Description = "Collects deterministic host-reported error and warning issues for review. Severity alone does not establish optimizer run eligibility.",
            InputSchema = "{\"type\":\"object\",\"properties\":{}}",
            OutputSchema = "{\"criticalIssues\":\"array\"}",
            AccessMode = AiToolAccessMode.ReadOnly,
            PermissionLevel = "ProjectRead"
        };

        public override Task<AiToolExecutionResult> ExecuteAsync(AiAgentToolContext context, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var issues = context.Workflow.ScreenContexts
                .SelectMany(screen => screen.Issues.Select(issue => new { Screen = screen.ScreenName, Issue = issue }))
                .Where(item => item.Issue.Severity.Equals("Error", StringComparison.OrdinalIgnoreCase)
                               || item.Issue.Severity.Equals("Warning", StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(item => SeverityRank(item.Issue.Severity))
                .ThenByDescending(item => item.Issue.AffectedCount)
                .ThenBy(item => item.Issue.Code, StringComparer.OrdinalIgnoreCase)
                .ToArray();

            var top = issues.Take(8).ToArray();
            var evidence = top.Length == 0
                ? "No deterministic error or warning issues are currently exposed by host screen snapshots."
                : string.Join(Environment.NewLine, top.Select((item, index) =>
                    $"{index + 1}. [{item.Issue.Severity}] {item.Issue.Code} ({item.Screen})" +
                    (IsOptimizerAdvisoryIssue(item.Issue.Code) ? " [AI advisory for optimizer eligibility]" : string.Empty) +
                    $" — {item.Issue.Evidence}"));

            var facts = new Dictionary<string, string>
            {
                ["CriticalIssueCount"] = issues.Length.ToString(),
                ["ErrorCount"] = issues.Count(item => item.Issue.Severity.Equals("Error", StringComparison.OrdinalIgnoreCase)).ToString(),
                ["WarningCount"] = issues.Count(item => item.Issue.Severity.Equals("Warning", StringComparison.OrdinalIgnoreCase)).ToString()
            };

            var actions = top
                .Select(item => string.IsNullOrWhiteSpace(item.Issue.NextClick)
                    ? item.Issue.Recommendation
                    : $"{item.Issue.NextClick}: {item.Issue.Recommendation}")
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Take(5)
                .ToArray();

            return Task.FromResult(Success(
                issues.Length == 0 ? "No host-reported error/warning issues were found." : $"{issues.Length:N0} host-reported error/warning issue(s) require review; not all are optimizer blockers.",
                evidence,
                facts,
                actions));
        }

        private static bool IsOptimizerAdvisoryIssue(string? code)
        {
            if (string.IsNullOrWhiteSpace(code)) return false;
            return code.Equals("GaCannotStartNoCandidates", StringComparison.OrdinalIgnoreCase)
                   || code.Equals("GaPreRunNoCandidates", StringComparison.OrdinalIgnoreCase)
                   || code.Equals("NoGaCandidates", StringComparison.OrdinalIgnoreCase)
                   || code.Equals("EstimatedWeightBlockedFromGA", StringComparison.OrdinalIgnoreCase)
                   || code.Equals("MissingTrustedWeight", StringComparison.OrdinalIgnoreCase)
                   || code.Equals("ElementWeightReviewNeeded", StringComparison.OrdinalIgnoreCase)
                   || code.Equals("SelectedElementWeightNotTrusted", StringComparison.OrdinalIgnoreCase);
        }

        private static int SeverityRank(string severity) => severity.Equals("Error", StringComparison.OrdinalIgnoreCase) ? 2 : 1;
    }
}
