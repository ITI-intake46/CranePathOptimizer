namespace CraneOptimizer.AI.Agents.Contracts
{
    public interface IAiAgentActionService
    {
        IReadOnlyList<AiAgentActionProposal> BuildProposals(AiAgentRunSnapshot run);

        Task<AiAgentActionExecutionSnapshot> ExecuteAsync(
            AiAgentActionProposal proposal,
            AiAgentRunSnapshot run,
            bool userApproved,
            string initiatedBy = "User",
            CancellationToken cancellationToken = default);

        AiAgentActionExecutionSnapshot Reject(
            AiAgentActionProposal proposal,
            AiAgentRunSnapshot run,
            string initiatedBy = "User");
    }
}
