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
                Reason = definition.AccessMode == AiToolAccessMode.Navigation
                    ? "Navigation is permitted only after an explicit user action."
                    : "Read-only project inspection is allowed and cannot modify crane-planning state."
            };
        }

        public AiApprovalDecision Evaluate(AiAgentActionToolDefinition definition, bool userApproved)
        {
            ArgumentNullException.ThrowIfNull(definition);

            if (definition.AccessMode == AiToolAccessMode.Mutating && !userApproved)
            {
                return new AiApprovalDecision
                {
                    CanExecute = false,
                    RequiresApproval = true,
                    Reason = "This action may modify project state and is blocked until the user explicitly approves it."
                };
            }

            if (definition.AccessMode == AiToolAccessMode.Navigation && !userApproved)
            {
                return new AiApprovalDecision
                {
                    CanExecute = false,
                    RequiresApproval = true,
                    Reason = "Navigation actions execute only after the user selects the action card."
                };
            }

            return new AiApprovalDecision
            {
                CanExecute = true,
                RequiresApproval = false,
                Reason = definition.AccessMode == AiToolAccessMode.Mutating
                    ? "Explicit one-time user approval was supplied for this action."
                    : "The user explicitly invoked a non-mutating navigation action."
            };
        }
    }
}
