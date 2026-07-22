using CraneOptimizer.AI.Configuration;

namespace CraneOptimizer.AI.Runtime
{
    public sealed class AiProviderRegistry : IAiProviderRegistry
    {
        private readonly IReadOnlyDictionary<string, IAiChatProvider> _providers;
        private readonly string _defaultProviderId;

        public AiProviderRegistry(
            IEnumerable<IAiChatProvider> providers,
            IAiCopilotSettingsService settingsService)
        {
            ArgumentNullException.ThrowIfNull(providers);
            ArgumentNullException.ThrowIfNull(settingsService);

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
            _defaultProviderId = string.IsNullOrWhiteSpace(settingsService.Current.DefaultProviderId)
                ? providerMap.Keys.First()
                : settingsService.Current.DefaultProviderId.Trim();

            Providers = providerMap.Values
                .Select(provider => provider.Descriptor)
                .OrderBy(descriptor => descriptor.DisplayName, StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }

        public IReadOnlyList<AiProviderDescriptor> Providers { get; }

        public bool TryResolve(string? providerId, out IAiChatProvider provider)
        {
            var requestedId = string.IsNullOrWhiteSpace(providerId)
                ? _defaultProviderId
                : providerId.Trim();

            if (_providers.TryGetValue(requestedId, out var resolved))
            {
                provider = resolved;
                return true;
            }

            provider = null!;
            return false;
        }
    }
}
