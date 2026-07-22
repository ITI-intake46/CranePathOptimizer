using CraneOptimizer.AI.DTOs;

namespace CraneOptimizer.AI.Runtime
{
    public interface IAiRequestPolicy
    {
        AiRequestPolicyDecision Evaluate(AiChatRequestDto request);
    }
}
