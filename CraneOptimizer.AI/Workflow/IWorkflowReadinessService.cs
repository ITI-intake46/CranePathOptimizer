using CraneOptimizer.AI.Host;

namespace CraneOptimizer.AI.Workflow
{
    public interface IWorkflowReadinessService
    {
        WorkflowReadinessSnapshot Evaluate(AiWorkflowContextSnapshot context, IUiInteractionContextService uiContext);
        IReadOnlyList<StructuredRecommendationCard> BuildStructuredCards(WorkflowReadinessSnapshot snapshot);
    }
}
