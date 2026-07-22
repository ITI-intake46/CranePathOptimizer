using CraneOptimizer.AI.Agents.Contracts;

namespace CraneOptimizer.AI.Agents.Runtime
{
    public sealed class DefaultAiAgentApprovalPolicy : IAiAgentApprovalPolicy
    {
        public AiApprovalDecision Evaluate(AiToolDefinition definition)
        {
            ArgumentNullException.ThrowIfNull(definition);

            if (definition.AccessMode == AiToolAccessMode.Mutating)
            {
                return new AiApprovalDecision
                {
                    CanExecute = false,
                    RequiresApproval = true,
                    Reason = "Mutating AI tools require explicit user approval before execution."
                };
            }

            return new AiApprovalDecision
            {
                CanExecute = true,
                RequiresApproval = false,
                Reason = "Read-only project inspection is allowed and cannot modify crane-planning state."
            };
        }
    }
}
