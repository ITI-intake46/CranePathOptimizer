using CraneOptimizer.UI.Features.F13_AiAdvisor.Services;
using CraneOptimizer.UI.Shell.ViewModels;

namespace CraneOptimizer.UI.Features.F13_AiAdvisor.Workflow
{
    public interface IWorkflowReadinessService
    {
        WorkflowReadinessSnapshot Evaluate(MainViewModel shell, IUiInteractionContextService uiContext);
        IReadOnlyList<StructuredRecommendationCard> BuildStructuredCards(WorkflowReadinessSnapshot snapshot);
    }
}
