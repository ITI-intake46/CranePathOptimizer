using CraneOptimizer.AI.DTOs;

namespace CraneOptimizer.AI.Interfaces
{
    public interface IAiChatService
    {
        Task<AiChatResponseDto> AskAsync(AiChatRequestDto request, CancellationToken cancellationToken = default);
    }
}
