namespace CraneOptimizer.AI.Agents.Contracts
{
    public interface IAiAgentActionToolRegistry
    {
        IReadOnlyList<AiAgentActionToolDefinition> Definitions { get; }
        bool TryGet(string name, out IAiAgentActionTool? tool);
        IAiAgentActionTool GetRequired(string name);
    }
}
