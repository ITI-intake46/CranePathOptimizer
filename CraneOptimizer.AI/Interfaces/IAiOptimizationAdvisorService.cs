using CraneOptimizer.AI.DTOs;

namespace CraneOptimizer.AI.Interfaces
{
    public interface IAiOptimizationAdvisorService
    {
        AiAdvisorResponseDto Analyze(AiOptimizationContextDto context);
    }
}
