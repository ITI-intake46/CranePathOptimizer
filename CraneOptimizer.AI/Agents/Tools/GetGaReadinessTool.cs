using CraneOptimizer.AI.Agents.Contracts;
using CraneOptimizer.AI.Host;
using CraneOptimizer.AI.Workflow;

namespace CraneOptimizer.AI.Agents.Tools
{
    public sealed class GetGaReadinessTool : AiProjectToolBase
    {
        public override AiToolDefinition Definition { get; } = new()
        {
            Name = "get_ga_readiness",
            Description = "Reads AI-visible project prerequisites, advisory candidate signals, and the captured result without replacing the optimizer's built-in preflight.",
            InputSchema = "{\"type\":\"object\",\"properties\":{}}",
            OutputSchema = "{\"eligibilityKnown\":\"boolean\",\"corePrerequisitesComplete\":\"boolean\",\"advisoryTrustedLiftUnits\":\"integer|null\",\"hasResult\":\"boolean\"}",
            AccessMode = AiToolAccessMode.ReadOnly,
            PermissionLevel = "ProjectRead"
        };

        public override Task<AiToolExecutionResult> ExecuteAsync(AiAgentToolContext context, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var workflow = context.Workflow;
            var assessment = AiOptimizerEvidenceAssessor.Assess(workflow);
            var result = workflow.OptimizationResult;

            var facts = new Dictionary<string, string>
            {
                ["OptimizerEligibilityKnown"] = assessment.HasAuthoritativeOptimizerEligibility.ToString(),
                ["CorePrerequisitesComplete"] = assessment.CorePrerequisitesComplete.ToString(),
                ["MissingCorePrerequisiteCount"] = assessment.MissingCorePrerequisiteCount.ToString(),
                ["AdvisoryTrustedLiftUnits"] = assessment.AdvisoryTrustedLiftUnits?.ToString() ?? string.Empty,
                ["HasNoCandidateBlocker"] = "False",
                ["HasAdvisoryNoCandidateSignal"] = assessment.HasNoCandidateAdvisory.ToString(),
                ["HasEstimatedWeightAdvisory"] = assessment.HasEstimatedWeightAdvisory.ToString(),
                ["CandidateEvidenceConflict"] = assessment.CandidateEvidenceConflict.ToString(),
                ["CandidateScopeMismatch"] = assessment.CandidateScopeMismatch.ToString(),
                ["HasOptimizationResult"] = result.HasResult.ToString(),
                ["CoverageRatio"] = result.CoverageRatio.ToString("0.####"),
                ["LiftedElementCount"] = result.LiftedElementCount.ToString(),
                ["TotalElementCount"] = result.TotalElementCount.ToString(),
                ["HistoricalEvaluatedLiftUnits"] = assessment.HistoricalEvaluatedLiftUnits.ToString()
            };

            var candidateText = assessment.AdvisoryTrustedLiftUnits.HasValue
                ? assessment.AdvisoryTrustedLiftUnits.Value.ToString("N0")
                : "not exposed";
            var evidence = $"AI-visible core prerequisites complete: {assessment.CorePrerequisitesComplete}; advisory import/readiness candidate count: {candidateText}; " +
                           $"advisory no-candidate signal: {assessment.HasNoCandidateAdvisory}; candidate/result conflict: {assessment.CandidateEvidenceConflict}; " +
                           $"captured result available: {result.HasResult}; evaluated units in result: {assessment.HistoricalEvaluatedLiftUnits:N0}. " +
                           assessment.AuthorityNote;

            var actions = new List<string>();
            if (!assessment.HasProject)
                actions.Add("Open or create a project first.");
            else if (!assessment.HasIfcElements)
                actions.Add("Load an IFC model before checking optimizer readiness.");
            else if (!assessment.HasZones)
                actions.Add("Define allowed placement zones before checking optimizer readiness.");
            else if (!assessment.HasCrane)
                actions.Add("Add or select a crane before checking optimizer readiness.");
            else if (!assessment.HasCurrentLoadChartEvidence)
                actions.Add("Import or verify current load-chart evidence before checking optimizer readiness.");
            else
                actions.Add("Open ANALYSIS → Run Optimizer and use Current Run Readiness as the definitive run decision.");

            if (assessment.HasNoCandidateAdvisory)
                actions.Add("Treat the zero-candidate import/readiness signal as troubleshooting evidence only; do not use it to bypass or replace the optimizer's own preflight.");

            return Task.FromResult(Success(
                assessment.CorePrerequisitesComplete
                    ? "Core project prerequisites are visible to AI; current optimizer eligibility still requires the optimizer's built-in preflight."
                    : "One or more visible project prerequisites are missing before optimizer readiness should be checked.",
                evidence,
                facts,
                actions));
        }
    }
}
