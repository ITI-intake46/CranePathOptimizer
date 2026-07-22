namespace CraneOptimizer.AI.Agents.Contracts
{
    public interface IAiAgentActionTool
    {
        AiAgentActionToolDefinition Definition { get; }

        Task<AiAgentActionToolResult> ExecuteAsync(
            AiAgentActionContext context,
            CancellationToken cancellationToken = default);
    }
}
