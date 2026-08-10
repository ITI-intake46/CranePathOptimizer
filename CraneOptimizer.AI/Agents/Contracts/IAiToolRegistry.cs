namespace CraneOptimizer.AI.Agents.Contracts
{
    public interface IAiToolRegistry
    {
        IReadOnlyList<AiToolDefinition> Definitions { get; }
        IAiProjectTool GetRequired(string name);
        bool TryGet(string name, out IAiProjectTool? tool);
    }
}
