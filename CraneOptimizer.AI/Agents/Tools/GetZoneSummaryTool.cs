using CraneOptimizer.AI.Agents.Contracts;

namespace CraneOptimizer.AI.Agents.Tools
{
    public sealed class GetZoneSummaryTool : AiProjectToolBase
    {
        public override AiToolDefinition Definition { get; } = new()
        {
            Name = "get_zone_summary",
            Description = "Reads allowed and forbidden crane-placement zones and their areas.",
            InputSchema = "{\"type\":\"object\",\"properties\":{}}",
            OutputSchema = "{\"total\":\"integer\",\"allowed\":\"integer\",\"forbidden\":\"integer\"}",
            AccessMode = AiToolAccessMode.ReadOnly,
            PermissionLevel = "ProjectRead"
        };

        public override Task<AiToolExecutionResult> ExecuteAsync(AiAgentToolContext context, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var workflow = context.Workflow;
            var facts = new Dictionary<string, string>
            {
                ["ZoneCount"] = workflow.ZoneCount.ToString(),
                ["AllowedZoneCount"] = workflow.AllowedZoneCount.ToString(),
                ["ForbiddenZoneCount"] = workflow.ForbiddenZoneCount.ToString(),
                ["AllowedZoneArea"] = workflow.AllowedZoneArea.ToString("0.###"),
                ["ForbiddenZoneArea"] = workflow.ForbiddenZoneArea.ToString("0.###")
            };

            var evidence = $"Zones: {workflow.ZoneCount:N0} total; {workflow.AllowedZoneCount:N0} allowed/green; " +
                           $"{workflow.ForbiddenZoneCount:N0} forbidden; allowed area {workflow.AllowedZoneArea:N2} m²; " +
                           $"forbidden area {workflow.ForbiddenZoneArea:N2} m².";

            return Task.FromResult(Success(
                workflow.ZoneCount > 0 ? "Placement zones are defined." : "No placement zones are defined.",
                evidence,
                facts,
                workflow.ZoneCount > 0 ? Array.Empty<string>() : new[] { "Define at least one allowed zone in ZONES." }));
        }
    }
}
