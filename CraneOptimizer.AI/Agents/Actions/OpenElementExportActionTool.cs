using CraneOptimizer.AI.Agents.Contracts;
using CraneOptimizer.AI.Host;

namespace CraneOptimizer.AI.Agents.Actions
{
    public sealed class OpenElementExportActionTool : UiNavigationAgentActionToolBase
    {
        public OpenElementExportActionTool(IUiInteractionContextService uiContext) : base(uiContext) { }

        public override AiAgentActionToolDefinition Definition { get; } = new()
        {
            Name = "open_element_export",
            Title = "Review trusted weight evidence",
            Description = "Open the IFC workspace to export the element list and inspect explicit trusted mass or weight sources.",
            Target = "IFC MODEL",
            ButtonLabel = "Open Element Export"
        };
    }
}
