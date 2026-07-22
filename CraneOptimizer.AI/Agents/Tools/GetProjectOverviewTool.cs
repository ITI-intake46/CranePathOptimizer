using CraneOptimizer.AI.Agents.Contracts;

namespace CraneOptimizer.AI.Agents.Tools
{
    public sealed class GetProjectOverviewTool : AiProjectToolBase
    {
        public override AiToolDefinition Definition { get; } = new()
        {
            Name = "get_project_overview",
            Description = "Reads the active project identity, model revision, object counts, and workflow completion state.",
            InputSchema = "{\"type\":\"object\",\"properties\":{}}",
            OutputSchema = "{\"project\":\"string\",\"modelRevision\":\"integer\",\"bimObjects\":\"integer\",\"readiness\":\"string\"}",
            AccessMode = AiToolAccessMode.ReadOnly,
            PermissionLevel = "ProjectRead"
        };

        public override Task<AiToolExecutionResult> ExecuteAsync(AiAgentToolContext context, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var workflow = context.Workflow;
            var readiness = context.Readiness;
            var project = workflow.HasProject
                ? (string.IsNullOrWhiteSpace(workflow.CurrentProjectName) ? workflow.ProjectDisplayName : workflow.CurrentProjectName)
                : "No active project";

            var facts = new Dictionary<string, string>
            {
                ["Project"] = project,
                ["ModelRevision"] = workflow.ModelRevision.ToString(),
                ["ModelFingerprint"] = workflow.ModelFingerprint,
                ["BimObjects"] = workflow.ElementCount.ToString(),
                ["Zones"] = workflow.ZoneCount.ToString(),
                ["Cranes"] = workflow.CraneCount.ToString(),
                ["Readiness"] = $"{readiness.CompleteCount}/{readiness.TotalCount}",
                ["MissingCritical"] = readiness.MissingCriticalCount.ToString()
            };

            var evidence = $"Project: {project}; model revision {workflow.ModelRevision}; " +
                           $"{workflow.ElementCount:N0} BIM/model object(s), {workflow.ZoneCount:N0} zone(s), " +
                           $"{workflow.CraneCount:N0} crane(s); readiness {readiness.CompleteCount}/{readiness.TotalCount} " +
                           $"with {readiness.MissingCriticalCount} critical item(s) missing.";

            return Task.FromResult(Success(
                workflow.HasProject ? "Active project context captured." : "No active project context is available.",
                evidence,
                facts,
                workflow.HasProject ? Array.Empty<string>() : new[] { "Open or create a project from HOME." }));
        }
    }
}
