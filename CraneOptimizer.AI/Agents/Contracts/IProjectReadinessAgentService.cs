using CraneOptimizer.AI.Host;

namespace CraneOptimizer.AI.Agents.Contracts
{
    public interface IProjectReadinessAgentService
    {
        bool CanHandle(string? question);

        Task<AiAgentRunSnapshot> RunAsync(
            AiWorkflowContextSnapshot context,
            string question,
            string activeScreen,
            string nextAction,
            CancellationToken cancellationToken = default);
    }
}
