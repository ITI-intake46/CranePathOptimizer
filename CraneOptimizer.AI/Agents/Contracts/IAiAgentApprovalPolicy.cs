namespace CraneOptimizer.AI.Agents.Contracts
{
    public sealed class AiApprovalDecision
    {
        public bool CanExecute { get; init; }
        public bool RequiresApproval { get; init; }
        public string Reason { get; init; } = string.Empty;
    }

    public interface IAiAgentApprovalPolicy
    {
        AiApprovalDecision Evaluate(AiToolDefinition definition);
    }
}
