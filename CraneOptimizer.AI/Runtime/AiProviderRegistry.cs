using CraneOptimizer.AI.Configuration;

namespace CraneOptimizer.AI.Runtime
{
    public sealed class AiProviderRegistry : IAiProviderRegistry
    {
        private readonly IReadOnlyDictionary<string, IAiChatProvider> _providers;
        private readonly IAiCopilotSettingsService _settingsService;

        public AiProviderRegistry(
            IEnumerable<IAiChatProvider> providers,
            IAiCopilotSettingsService settingsService)
        {
            ArgumentNullException.ThrowIfNull(providers);
            _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));

            var providerMap = new Dictionary<string, IAiChatProvider>(StringComparer.OrdinalIgnoreCase);
            foreach (var provider in providers)
            {
                if (provider == null || string.IsNullOrWhiteSpace(provider.Descriptor.Id))
                    continue;

                if (!providerMap.TryAdd(provider.Descriptor.Id.Trim(), provider))
                {
                    throw new InvalidOperationException(
                        $"Multiple AI providers are registered with id '{provider.Descriptor.Id}'. Provider ids must be unique.");
                }
            }

            if (providerMap.Count == 0)
                throw new InvalidOperationException("At least one AI chat provider must be registered.");

            _providers = providerMap;
            Providers = providerMap.Values
                .Select(provider => provider.Descriptor)
                .OrderBy(descriptor => descriptor.IsLocal ? 0 : 1)
                .ThenBy(descriptor => descriptor.DisplayName, StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }

        public IReadOnlyList<AiProviderDescriptor> Providers { get; }

        public bool TryResolve(string? providerId, out IAiChatProvider provider)
        {
            var requestedId = string.IsNullOrWhiteSpace(providerId)
                ? _settingsService.Current.DefaultProviderId
                : providerId.Trim();

            if (_providers.TryGetValue(requestedId, out var resolved)
                && IsEnabled(requestedId))
            {
                provider = resolved;
                return true;
            }

            provider = null!;
            return false;
        }

        private bool IsEnabled(string providerId)
        {
            var profile = _settingsService.Current.ProviderProfiles.FirstOrDefault(item =>
                string.Equals(item.Id, providerId, StringComparison.OrdinalIgnoreCase));
            return profile?.Enabled != false;
        }
    }
}
