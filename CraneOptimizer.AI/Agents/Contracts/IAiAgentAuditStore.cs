namespace CraneOptimizer.AI.Agents.Contracts
{
    public interface IAiAgentAuditStore
    {
        IReadOnlyList<AiAgentRunSnapshot> GetRecent(int maxCount = 20);
        void Record(AiAgentRunSnapshot run);
    }
}
