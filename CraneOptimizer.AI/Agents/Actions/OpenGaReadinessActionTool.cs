using CraneOptimizer.AI.Agents.Contracts;
using CraneOptimizer.AI.Host;

namespace CraneOptimizer.AI.Agents.Actions
{
    public sealed class OpenGaReadinessActionTool : UiNavigationAgentActionToolBase
    {
        public OpenGaReadinessActionTool(IUiInteractionContextService uiContext) : base(uiContext) { }

        public override AiAgentActionToolDefinition Definition { get; } = new()
        {
            Name = "open_ga_readiness",
            Title = "Review optimizer readiness",
            Description = "Open the analysis workspace to review deterministic GA pre-run eligibility and blocker diagnostics.",
            Target = "ANALYZE",
            ButtonLabel = "Open Readiness"
        };
    }
}
