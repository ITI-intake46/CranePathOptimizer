using CraneOptimizer.AI.DTOs;

namespace CraneOptimizer.AI.Runtime
{
    public interface IAiChatProvider
    {
        AiProviderDescriptor Descriptor { get; }

        Task<AiChatResponseDto> AskAsync(
            AiChatRequestDto request,
            CancellationToken cancellationToken = default);

        Task<AiChatResponseDto> AskStreamingAsync(
            AiChatRequestDto request,
            Action<AiStreamingUpdateDto> onUpdate,
            CancellationToken cancellationToken = default);
    }
}
