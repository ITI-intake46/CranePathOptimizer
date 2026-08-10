namespace CraneOptimizer.AI.Agents.Contracts
{
    public interface IAiAgentDraftArtifactStore
    {
        Task<AiAgentDraftArtifactResult> WriteAsync(
            AiAgentDraftArtifactRequest request,
            CancellationToken cancellationToken = default);
    }
}
