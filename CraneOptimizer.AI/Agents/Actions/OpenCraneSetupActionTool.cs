using CraneOptimizer.AI.Agents.Contracts;
using CraneOptimizer.AI.Host;

namespace CraneOptimizer.AI.Agents.Actions
{
    public sealed class OpenCraneSetupActionTool : UiNavigationAgentActionToolBase
    {
        public OpenCraneSetupActionTool(IUiInteractionContextService uiContext) : base(uiContext) { }

        public override AiAgentActionToolDefinition Definition { get; } = new()
        {
            Name = "open_crane_setup",
            Title = "Select or add a mobile crane",
            Description = "Open the crane workspace to add or select the crane model used for inspection and optimization.",
            Target = "CRANES",
            ButtonLabel = "Open Cranes"
        };
    }
}
