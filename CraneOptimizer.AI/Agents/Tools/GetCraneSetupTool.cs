using CraneOptimizer.AI.Agents.Contracts;

namespace CraneOptimizer.AI.Agents.Tools
{
    public sealed class GetCraneSetupTool : AiProjectToolBase
    {
        public override AiToolDefinition Definition { get; } = new()
        {
            Name = "get_crane_setup",
            Description = "Reads crane count and load-chart evidence for the active project.",
            InputSchema = "{\"type\":\"object\",\"properties\":{}}",
            OutputSchema = "{\"cranes\":\"integer\",\"loadChartEvidence\":\"integer\"}",
            AccessMode = AiToolAccessMode.ReadOnly,
            PermissionLevel = "ProjectRead"
        };

        public override Task<AiToolExecutionResult> ExecuteAsync(AiAgentToolContext context, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var workflow = context.Workflow;
            var facts = new Dictionary<string, string>
            {
                ["CraneCount"] = workflow.CraneCount.ToString(),
                ["LoadChartEvidenceCount"] = workflow.LoadChartEvidenceCount.ToString(),
                ["HasLoadChartEvidence"] = workflow.HasLoadChartEvidence.ToString()
            };

            var evidence = $"Crane instances: {workflow.CraneCount:N0}; load-chart evidence sources: {workflow.LoadChartEvidenceCount:N0}; " +
                           $"load-chart evidence available: {workflow.HasLoadChartEvidence}.";

            var actions = new List<string>();
            if (workflow.CraneCount == 0)
                actions.Add("Add or select a mobile crane from CRANES → MOBILE CRANE.");
            if (!workflow.HasLoadChartEvidence)
                actions.Add("Import or verify manufacturer load-chart evidence before capacity-based optimization.");

            return Task.FromResult(Success(
                workflow.CraneCount > 0 ? "A crane is present in the project." : "No crane is selected or present in the project.",
                evidence,
                facts,
                actions));
        }
    }
}
