using CraneOptimizer.AI.Agents.Contracts;
using CraneOptimizer.AI.Host;

namespace CraneOptimizer.AI.Agents.Actions
{
    public sealed class OpenElementManagementActionTool : UiNavigationAgentActionToolBase
    {
        public OpenElementManagementActionTool(IUiInteractionContextService uiContext) : base(uiContext) { }

        public override AiAgentActionToolDefinition Definition { get; } = new()
        {
            Name = "open_element_management",
            Title = "Inspect element identity and COG records",
            Description = "Open the IFC workspace to review missing assembly marks, element identity confidence, and center-of-gravity evidence.",
            Target = "IFC MODEL",
            ButtonLabel = "Open Elements"
        };
    }
}
