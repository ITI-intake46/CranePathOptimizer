using CraneOptimizer.AI.Agents.Contracts;
using CraneOptimizer.AI.Workflow;

namespace CraneOptimizer.AI.Agents.Tools
{
    public sealed class GetCraneSetupTool : AiProjectToolBase
    {
        public override AiToolDefinition Definition { get; } = new()
        {
            Name = "get_crane_setup",
            Description = "Reads crane count and current crane/load-chart evidence from the AI host snapshot.",
            InputSchema = "{\"type\":\"object\",\"properties\":{}}",
            OutputSchema = "{\"cranes\":\"integer\",\"hasCurrentLoadChartEvidence\":\"boolean\"}",
            AccessMode = AiToolAccessMode.ReadOnly,
            PermissionLevel = "ProjectRead"
        };

        public override Task<AiToolExecutionResult> ExecuteAsync(AiAgentToolContext context, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var workflow = context.Workflow;
            var assessment = AiOptimizerEvidenceAssessor.Assess(workflow);
            var hasCurrentLoadChart = assessment.HasCurrentLoadChartEvidence;

            var facts = new Dictionary<string, string>
            {
                ["CraneCount"] = workflow.CraneCount.ToString(),
                ["LoadChartEvidenceCount"] = workflow.LoadChartEvidenceCount.ToString(),
                ["HasLoadChartEvidence"] = hasCurrentLoadChart.ToString(),
                ["HasCurrentLoadChartEvidence"] = hasCurrentLoadChart.ToString()
            };

            var evidence = $"Crane instances: {workflow.CraneCount:N0}; current crane specification/capacity evidence visible to AI: {hasCurrentLoadChart}. " +
                           "Historical optimization-result capacity/radius values are not treated as proof of current load-chart readiness.";

            var actions = new List<string>();
            if (workflow.CraneCount == 0)
                actions.Add("Add or select a mobile crane from CRANES → MOBILE CRANE.");
            if (!hasCurrentLoadChart)
                actions.Add("Import or verify the current manufacturer load chart before relying on capacity-based conclusions.");

            return Task.FromResult(Success(
                workflow.CraneCount == 0
                    ? "No crane is selected or present in the project."
                    : hasCurrentLoadChart
                        ? "A crane and current crane/load-chart evidence are visible to the AI."
                        : "A crane is present, but current crane/load-chart evidence is not confirmed by the AI snapshot.",
                evidence,
                facts,
                actions));
        }
    }
}
