using CraneOptimizer.AI.Host;
using CraneOptimizer.Application.Services;
using CraneOptimizer.UI.Shell.ViewModels;

namespace CraneOptimizer.UI.AIIntegration.Contributors
{
    /// <summary>
    /// Exposes report-readiness and AI-assisted narrative drafting evidence.
    /// </summary>
    public sealed class ReportAiContextContributor : IAiScreenContextContributor
    {
        private readonly IImportReadinessService _readinessService;

        public ReportAiContextContributor(IImportReadinessService readinessService)
        {
            _readinessService = readinessService ?? throw new ArgumentNullException(nameof(readinessService));
        }

        public string ScreenName => "Reports";

        public AiScreenContextSnapshot BuildSnapshot(MainViewModel shell)
        {
            var result = shell.CranePlacement.Result;
            var readiness = _readinessService.BuildSummary(shell.Scene.GetElements());
            var hasResult = result != null;
            var reportReady = hasResult && result!.CoverageRatio >= 0.95 && readiness.GaBlockedElements == 0;
            var issues = new List<AiContextIssueSnapshot>();

            if (!hasResult)
            {
                issues.Add(AiContextContributorHelpers.Issue(
                    "ReportNoOptimizationResult",
                    "Info",
                    "Reports",
                    "No optimization result is available for a lifting-summary report.",
                    "Run optimization before producing final lifting-result deliverables.",
                    "ANALYZE → Run Optimizer"));
            }
            else if (result.CoverageRatio < 0.95)
            {
                issues.Add(AiContextContributorHelpers.Issue(
                    "ReportCoverageNeedsReview",
                    "Warning",
                    "Reports",
                    $"Optimization coverage is {result.CoverageRatio:P2}, below the 95% report-readiness guidance threshold.",
                    "Resolve or explicitly document unresolved elements before issuing a final report.",
                    "ANALYZE → GA Diagnostics"));
            }

            if (readiness.GaBlockedElements > 0)
            {
                issues.Add(AiContextContributorHelpers.Issue(
                    "ReportInputReadinessWarnings",
                    "Warning",
                    "Reports",
                    $"Import readiness still reports {readiness.GaBlockedElements:N0} GA-blocked element(s).",
                    "Fix or document blocked elements and model data assumptions before publishing reports.",
                    "IFC MODEL → Element Management",
                    readiness.GaBlockedElements));
            }

            return new AiScreenContextSnapshot
            {
                ScreenName = ScreenName,
                Stage = reportReady ? "Ready for reviewed report" : hasResult ? "Needs review" : "No result",
                Summary = reportReady
                    ? "A report can be drafted from deterministic evidence, but AI-written narrative remains review-required."
                    : "Report generation needs optimization/import evidence review before final deliverables.",
                Metrics = new[]
                {
                    AiContextContributorHelpers.Metric("Optimization result", hasResult ? "Available" : "Missing", hasResult ? $"Coverage {result!.CoverageRatio:P2}" : "Run ANALYZE first"),
                    AiContextContributorHelpers.Metric("Input readiness", readiness.OverallStatus.ToString(), $"GA blocked: {readiness.GaBlockedElements:N0}"),
                    AiContextContributorHelpers.Metric("AI narrative", hasResult ? "Draftable" : "Not ready", "AI-assisted narrative must be copied/reviewed; it is not certification."),
                    AiContextContributorHelpers.Metric("Diagnostics path", string.IsNullOrWhiteSpace(result?.DebugReportPath) ? "None" : "Available", result?.DebugReportPath ?? "No result diagnostics path")
                },
                Issues = issues,
                ActionHints = new[]
                {
                    AiContextContributorHelpers.Hint("Open reports", "REPORTS", "Build report after reviewing deterministic readiness and GA diagnostics."),
                    AiContextContributorHelpers.Hint("Copy AI report narrative", "AI_REPORT_NARRATIVE", "Draft an AI-assisted report note from deterministic context for explicit reviewer approval.", "CopyReportNarrative", "Low — copies draft text only; no project/report data is changed."),
                    AiContextContributorHelpers.Hint("Open diagnostics", "ANALYZE", "Review unresolved lift diagnostics before report issue.")
                },
                Facts = new Dictionary<string, string>
                {
                    ["ReportReady"] = reportReady.ToString(),
                    ["HasOptimizationResult"] = hasResult.ToString(),
                    ["CoverageRatio"] = result?.CoverageRatio.ToString("0.####") ?? string.Empty,
                    ["GaBlockedElements"] = readiness.GaBlockedElements.ToString(),
                    ["ReadinessStatus"] = readiness.OverallStatus.ToString(),
                    ["DiagnosticsPath"] = result?.DebugReportPath ?? string.Empty
                }
            };
        }
    }
}
