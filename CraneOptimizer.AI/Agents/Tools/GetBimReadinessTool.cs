using CraneOptimizer.AI.Agents.Contracts;
using CraneOptimizer.AI.Host;

namespace CraneOptimizer.AI.Agents.Tools
{
    public sealed class GetBimReadinessTool : AiProjectToolBase
    {
        public override AiToolDefinition Definition { get; } = new()
        {
            Name = "get_bim_readiness",
            Description = "Inspects live BIM/model counts and trusted GA-ready lift-unit evidence without changing the model.",
            InputSchema = "{\"type\":\"object\",\"properties\":{}}",
            OutputSchema = "{\"sceneObjects\":\"integer\",\"planningObjects\":\"integer\",\"trustedLiftUnits\":\"integer\",\"estimatedWeightBlocked\":\"integer\"}",
            AccessMode = AiToolAccessMode.ReadOnly,
            PermissionLevel = "ProjectRead"
        };

        public override Task<AiToolExecutionResult> ExecuteAsync(AiAgentToolContext context, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var workflow = context.Workflow;
            var merged = MergeFacts(workflow);
            var planning = ReadInt(merged, AiWorkflowFactKeys.ReadinessElementCount, workflow.ElementCount);
            var trusted = ReadInt(merged, AiWorkflowFactKeys.TrustedGaLiftUnits,
                ReadInt(merged, AiWorkflowFactKeys.TrustedGroupedLiftUnits));
            var estimatedBlocked = ReadInt(merged, AiWorkflowFactKeys.EstimatedWeightBlockedCount);
            var missingTrusted = ReadInt(merged, AiWorkflowFactKeys.MissingTrustedWeightCount);
            var referenceExcluded = ReadInt(merged, AiWorkflowFactKeys.ReferenceGeometryExcludedCount);

            var facts = new Dictionary<string, string>
            {
                ["SceneObjects"] = workflow.ElementCount.ToString(),
                ["PlanningObjects"] = planning.ToString(),
                ["TrustedGaLiftUnits"] = trusted.ToString(),
                ["EstimatedWeightBlocked"] = estimatedBlocked.ToString(),
                ["MissingTrustedWeight"] = missingTrusted.ToString(),
                ["ReferenceGeometryExcluded"] = referenceExcluded.ToString()
            };

            var evidence = $"Scene BIM/model objects: {workflow.ElementCount:N0}; planning/readiness objects: {planning:N0}; " +
                           $"trusted GA-ready grouped lift units: {trusted:N0}; estimated-weight blocked: {estimatedBlocked:N0}; " +
                           $"missing trusted weight: {missingTrusted:N0}; reference geometry excluded: {referenceExcluded:N0}.";

            var actions = new List<string>();
            if (workflow.ElementCount == 0)
                actions.Add("Load an IFC model from IFC MODEL → Load IFC.");
            else if (trusted == 0)
                actions.Add("Review/export the element list and correct trusted mass, weight-source, lift-unit, and COG evidence before GA.");

            return Task.FromResult(Success(
                trusted > 0 ? "Trusted GA-ready BIM lift-unit evidence exists." : "BIM is loaded, but trusted GA-ready lift-unit evidence is not available.",
                evidence,
                facts,
                actions));
        }
    }
}
