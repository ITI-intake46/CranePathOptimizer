using CraneOptimizer.Application.AI.DTOs;

namespace CraneOptimizer.Application.AI.Interfaces
{
    public interface IAiModelManagementService
    {
        Task<AiModelStatusDto> GetStatusAsync(CancellationToken cancellationToken = default);
        Task<AiChatResponseDto> PullModelAsync(string modelName, CancellationToken cancellationToken = default);
    }
}
