using CraneOptimizer.Application.AI.DTOs;

namespace CraneOptimizer.UI.Features.F13_AiAdvisor.Knowledge
{
    public interface IAiPromptTemplateService
    {
        IReadOnlyList<AiPromptTemplate> GetAllTemplates();
        AiPromptTemplate SelectTemplate(AiOptimizationContextDto context, AiAdvisorResponseDto advisorResponse);
    }
}
