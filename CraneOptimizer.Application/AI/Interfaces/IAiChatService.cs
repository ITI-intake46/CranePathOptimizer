using CraneOptimizer.Application.AI.DTOs;

namespace CraneOptimizer.Application.AI.Interfaces
{
    public interface IAiChatService
    {
        Task<AiChatResponseDto> AskAsync(AiChatRequestDto request, CancellationToken cancellationToken = default);
    }
}
