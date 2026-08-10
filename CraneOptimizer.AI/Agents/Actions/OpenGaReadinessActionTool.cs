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
            Title = "Verify optimizer readiness",
            Description = "Open the analysis workspace so the user can reach Run Optimizer and inspect the application-owned Current Run Readiness indicator.",
            Target = "ANALYZE",
            ButtonLabel = "Open Analysis"
        };
    }
}
