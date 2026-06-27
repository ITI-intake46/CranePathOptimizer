using CraneOptimizer.Application.AI.DTOs;

namespace CraneOptimizer.Application.AI.Interfaces
{
    public interface IAiOptimizationAdvisorService
    {
        AiAdvisorResponseDto Analyze(AiOptimizationContextDto context);
    }
}
