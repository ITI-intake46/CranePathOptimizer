using CraneOptimizer.AI.Agents.Contracts;
using CraneOptimizer.AI.Host;

namespace CraneOptimizer.AI.Agents.Actions
{
    public sealed class OpenLoadChartImportActionTool : UiNavigationAgentActionToolBase
    {
        public OpenLoadChartImportActionTool(IUiInteractionContextService uiContext) : base(uiContext) { }

        public override AiAgentActionToolDefinition Definition { get; } = new()
        {
            Name = "open_load_chart_import",
            Title = "Import or verify load-chart evidence",
            Description = "Open the crane workspace to import or verify manufacturer load-chart evidence before capacity-based analysis.",
            Target = "CRANES",
            ButtonLabel = "Open Load Charts"
        };
    }
}
