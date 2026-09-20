using CraneOptimizer.AI.Configuration;
using CraneOptimizer.AI.Runtime;

namespace CraneOptimizer.AI.Providers.OpenAiCompatible
{
    internal static class AiProviderProfileResolver
    {
        public static AiProviderProfileSettings Resolve(IAiCopilotSettingsService settingsService, string providerId)
        {
            ArgumentNullException.ThrowIfNull(settingsService);
            if (string.IsNullOrWhiteSpace(providerId))
                throw new ArgumentException("A provider id is required.", nameof(providerId));

            return settingsService.Current.ProviderProfiles.FirstOrDefault(profile =>
                       string.Equals(profile.Id, providerId, StringComparison.OrdinalIgnoreCase))
                   ?? throw new InvalidOperationException($"AI provider profile '{providerId}' is not configured.");
        }

        public static Uri BuildEndpoint(AiProviderProfileSettings profile, string relativePath)
        {
            if (string.IsNullOrWhiteSpace(profile.BaseUrl)
                || !Uri.TryCreate(profile.BaseUrl, UriKind.Absolute, out var baseUri))
            {
                throw new InvalidOperationException($"Configure a valid HTTP or HTTPS base URL for {profile.DisplayName}.");
            }

            if (baseUri.Scheme != Uri.UriSchemeHttps && baseUri.Scheme != Uri.UriSchemeHttp)
                throw new InvalidOperationException($"The {profile.DisplayName} endpoint must use HTTP or HTTPS.");
            var isLocalDestination = AiEndpointSecurityClassifier.IsLocalEndpoint(baseUri);
            if (!isLocalDestination && baseUri.Scheme != Uri.UriSchemeHttps)
            {
                throw new InvalidOperationException($"Remote endpoint for {profile.DisplayName} must use HTTPS.");
            }

            return new Uri(baseUri, (relativePath ?? string.Empty).TrimStart('/'));
        }
    }
}
