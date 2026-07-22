using CraneOptimizer.AI.Agents.Contracts;
using CraneOptimizer.AI.Services;
using CraneOptimizer.AI.Host;

namespace CraneOptimizer.AI.Agents.Tools
{
    public sealed class GetGaReadinessTool : AiProjectToolBase
    {
        public override AiToolDefinition Definition { get; } = new()
        {
            Name = "get_ga_readiness",
            Description = "Reads deterministic GA pre-run readiness, candidate counts, and the latest optimization result.",
            InputSchema = "{\"type\":\"object\",\"properties\":{}}",
            OutputSchema = "{\"canRun\":\"boolean\",\"trustedLiftUnits\":\"integer\",\"hasResult\":\"boolean\"}",
            AccessMode = AiToolAccessMode.ReadOnly,
            PermissionLevel = "ProjectRead"
        };

        public override Task<AiToolExecutionResult> ExecuteAsync(AiAgentToolContext context, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var workflow = context.Workflow;
            var merged = MergeFacts(workflow);
            var trusted = ReadInt(merged, AiWorkflowFactKeys.TrustedGaLiftUnits,
                ReadInt(merged, AiWorkflowFactKeys.TrustedGroupedLiftUnits));
            var blocker = AiWorkflowEvidenceTextBuilder.HasNoGaCandidateBlocker(workflow);
            var result = workflow.OptimizationResult;

            var facts = new Dictionary<string, string>
            {
                ["CanRunOptimizer"] = (!blocker && trusted > 0 && workflow.CraneCount > 0 && workflow.ZoneCount > 0).ToString(),
                ["TrustedGaLiftUnits"] = trusted.ToString(),
                ["HasNoCandidateBlocker"] = blocker.ToString(),
                ["HasOptimizationResult"] = result.HasResult.ToString(),
                ["CoverageRatio"] = result.CoverageRatio.ToString("0.####"),
                ["LiftedElementCount"] = result.LiftedElementCount.ToString(),
                ["TotalElementCount"] = result.TotalElementCount.ToString()
            };

            var evidence = $"GA pre-run blocker active: {blocker}; trusted GA-ready lift units: {trusted:N0}; " +
                           $"zones: {workflow.ZoneCount:N0}; cranes: {workflow.CraneCount:N0}; result available: {result.HasResult}; " +
                           $"coverage: {result.CoverageRatio:P2}.";

            var actions = new List<string>();
            if (blocker || trusted == 0)
                actions.Add("Keep the optimizer gate closed until trusted grouped lift-unit evidence exists.");
            if (workflow.CraneCount == 0)
                actions.Add("Add or select a mobile crane before manual inspection or optimization.");
            if (workflow.ZoneCount == 0)
                actions.Add("Define allowed placement zones before running GA.");

            return Task.FromResult(Success(
                blocker ? "GA is deterministically blocked before execution." : "No zero-candidate GA blocker is currently detected.",
                evidence,
                facts,
                actions));
        }
    }
}
