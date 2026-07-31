using CraneOptimizer.AI.Configuration;
using CraneOptimizer.AI.DTOs;
using CraneOptimizer.AI.Runtime;

namespace CraneOptimizer.AI.Interfaces
{
    public interface IAiProviderManagementService
    {
        IReadOnlyList<AiProviderDescriptor> GetProviders();
        AiProviderProfileSettings GetProfile(string providerId);
        void UpdateProfile(AiProviderProfileSettings profile);
        bool HasCredential(string providerId);
        void SaveCredential(string providerId, string apiKey);
        void DeleteCredential(string providerId);
        Task<AiProviderStatusDto> GetStatusAsync(string providerId, CancellationToken cancellationToken = default);
    }
}
