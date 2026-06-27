using CraneOptimizer.Application.AI.DTOs;

namespace CraneOptimizer.Application.AI.Interfaces
{
    /// <summary>
    /// Optional extension for AI chat providers that can return incremental response tokens.
    /// Consumers should still depend on IAiChatService for basic operation and dynamically
    /// use this interface when available.
    /// </summary>
    public interface IAiStreamingChatService : IAiChatService
    {
        Task<AiChatResponseDto> AskStreamingAsync(
            AiChatRequestDto request,
            Action<AiStreamingUpdateDto> onUpdate,
            CancellationToken cancellationToken = default);
    }
}
