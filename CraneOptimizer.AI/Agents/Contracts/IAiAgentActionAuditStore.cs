namespace CraneOptimizer.AI.Agents.Contracts
{
    public interface IAiAgentActionAuditStore
    {
        IReadOnlyList<AiAgentActionExecutionSnapshot> GetRecent(int maxCount = 50);
        void Record(AiAgentActionExecutionSnapshot execution);
    }
}
