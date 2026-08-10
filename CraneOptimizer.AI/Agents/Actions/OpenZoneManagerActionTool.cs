using CraneOptimizer.AI.Agents.Contracts;
using CraneOptimizer.AI.Host;

namespace CraneOptimizer.AI.Agents.Actions
{
    public sealed class OpenZoneManagerActionTool : UiNavigationAgentActionToolBase
    {
        public OpenZoneManagerActionTool(IUiInteractionContextService uiContext) : base(uiContext) { }

        public override AiAgentActionToolDefinition Definition { get; } = new()
        {
            Name = "open_zone_manager",
            Title = "Review crane-placement zones",
            Description = "Open the zones workspace to inspect allowed and forbidden crane-placement areas.",
            Target = "ZONES",
            ButtonLabel = "Open Zones"
        };
    }
}
