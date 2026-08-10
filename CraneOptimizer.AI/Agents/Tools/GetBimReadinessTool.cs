using CraneOptimizer.AI.Agents.Contracts;
using CraneOptimizer.AI.Host;
using CraneOptimizer.AI.Workflow;

namespace CraneOptimizer.AI.Agents.Tools
{
    public sealed class GetBimReadinessTool : AiProjectToolBase
    {
        public override AiToolDefinition Definition { get; } = new()
        {
            Name = "get_bim_readiness",
            Description = "Inspects live BIM/model counts and AI-visible import/readiness lift-unit signals without changing the model.",
            InputSchema = "{\"type\":\"object\",\"properties\":{}}",
            OutputSchema = "{\"sceneObjects\":\"integer\",\"planningObjects\":\"integer\",\"advisoryLiftUnits\":\"integer|null\",\"estimatedWeightBlocked\":\"integer\"}",
            AccessMode = AiToolAccessMode.ReadOnly,
            PermissionLevel = "ProjectRead"
        };

        public override Task<AiToolExecutionResult> ExecuteAsync(AiAgentToolContext context, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var workflow = context.Workflow;
            var merged = MergeFacts(workflow);
            var assessment = AiOptimizerEvidenceAssessor.Assess(workflow);
            var planning = ReadInt(merged, AiWorkflowFactKeys.ReadinessElementCount, workflow.ElementCount);
            var estimatedBlocked = ReadInt(merged, AiWorkflowFactKeys.EstimatedWeightBlockedCount);
            var missingTrusted = ReadInt(merged, AiWorkflowFactKeys.MissingTrustedWeightCount);
            var referenceExcluded = ReadInt(merged, AiWorkflowFactKeys.ReferenceGeometryExcludedCount);

            var facts = new Dictionary<string, string>
            {
                ["SceneObjects"] = workflow.ElementCount.ToString(),
                ["PlanningObjects"] = planning.ToString(),
                ["AdvisoryTrustedLiftUnits"] = assessment.AdvisoryTrustedLiftUnits?.ToString() ?? string.Empty,
                ["EstimatedWeightAdvisoryCount"] = estimatedBlocked.ToString(),
                ["MissingTrustedWeight"] = missingTrusted.ToString(),
                ["ReferenceGeometryExcluded"] = referenceExcluded.ToString(),
                ["CandidateEvidenceConflict"] = assessment.CandidateEvidenceConflict.ToString()
            };

            var candidateText = assessment.AdvisoryTrustedLiftUnits.HasValue
                ? assessment.AdvisoryTrustedLiftUnits.Value.ToString("N0")
                : "not exposed";
            var evidence = $"Scene BIM/model objects: {workflow.ElementCount:N0}; planning/readiness objects: {planning:N0}; " +
                           $"AI-visible import/readiness lift-unit signal: {candidateText}; estimated-weight warnings: {estimatedBlocked:N0}; " +
                           $"missing trusted weight: {missingTrusted:N0}; reference geometry excluded: {referenceExcluded:N0}. " +
                           "These import/readiness facts are troubleshooting evidence, not the optimizer dialog's authoritative current-run preflight.";

            var actions = new List<string>();
            if (workflow.ElementCount == 0)
                actions.Add("Load an IFC model from IFC MODEL → Load IFC.");
            else if (assessment.HasNoCandidateAdvisory || assessment.HasEstimatedWeightAdvisory)
                actions.Add("Keep the import/readiness warning available for troubleshooting. If the optimizer's own readiness check confirms a blocker, review the element list and trusted mass/weight evidence.");

            return Task.FromResult(Success(
                workflow.ElementCount == 0
                    ? "No BIM/model objects are loaded."
                    : assessment.HasNoCandidateAdvisory
                        ? "BIM is loaded; AI import/readiness evidence contains a no-candidate warning that requires optimizer-side verification."
                        : "BIM/model evidence is available for AI review.",
                evidence,
                facts,
                actions));
        }
    }
}
