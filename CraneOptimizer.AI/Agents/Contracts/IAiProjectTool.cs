namespace CraneOptimizer.AI.Agents.Contracts
{
    public interface IAiProjectTool
    {
        AiToolDefinition Definition { get; }

        Task<AiToolExecutionResult> ExecuteAsync(
            AiAgentToolContext context,
            CancellationToken cancellationToken = default);
    }
}
