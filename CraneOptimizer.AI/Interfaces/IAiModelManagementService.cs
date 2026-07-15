using CraneOptimizer.AI.DTOs;

namespace CraneOptimizer.AI.Interfaces
{
    public interface IAiModelManagementService
    {
        Task<AiModelStatusDto> GetStatusAsync(CancellationToken cancellationToken = default);
        Task<AiChatResponseDto> PullModelAsync(string modelName, CancellationToken cancellationToken = default);
    }
}
