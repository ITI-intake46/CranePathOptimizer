using CraneOptimizer.AI.Agents.Contracts;
using CraneOptimizer.AI.Host;

namespace CraneOptimizer.AI.Agents.Actions
{
    public abstract class UiNavigationAgentActionToolBase : IAiAgentActionTool
    {
        private readonly IUiInteractionContextService _uiContext;

        protected UiNavigationAgentActionToolBase(IUiInteractionContextService uiContext)
        {
            _uiContext = uiContext ?? throw new ArgumentNullException(nameof(uiContext));
        }

        public abstract AiAgentActionToolDefinition Definition { get; }

        public Task<AiAgentActionToolResult> ExecuteAsync(
            AiAgentActionContext context,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(context);
            cancellationToken.ThrowIfCancellationRequested();

            if (!context.UserApproved)
            {
                return Task.FromResult(new AiAgentActionToolResult
                {
                    Succeeded = false,
                    Error = "The navigation action was not explicitly invoked by the user."
                });
            }

            var reason = $"{context.Proposal.Description} Agent run: {context.AgentRun.RunId}. " +
                         $"Evidence: {string.Join(", ", context.Proposal.EvidenceIds.Select(id => $"[{id}]"))}.";
            _uiContext.RequestTabNavigation(Definition.Target, reason);

            return Task.FromResult(new AiAgentActionToolResult
            {
                Succeeded = true,
                Summary = $"Opened {Definition.Target} for the recommended next step."
            });
        }
    }
}
