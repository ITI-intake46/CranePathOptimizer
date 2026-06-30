using CraneOptimizer.AI.DTOs;

namespace CraneOptimizer.AI.Knowledge
{
    public interface IAiPromptTemplateService
    {
        IReadOnlyList<AiPromptTemplate> GetAllTemplates();
        AiPromptTemplate SelectTemplate(AiOptimizationContextDto context, AiAdvisorResponseDto advisorResponse);
    }
}
