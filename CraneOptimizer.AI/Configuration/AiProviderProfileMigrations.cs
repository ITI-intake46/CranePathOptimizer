namespace CraneOptimizer.AI.Configuration
{
    /// <summary>
    /// Applies narrow, known-safe migrations to persisted provider profiles. Migrations only
    /// touch legacy values that were shipped by Crane Optimizer; intentional custom endpoints
    /// remain unchanged.
    /// </summary>
    public static class AiProviderProfileMigrations
    {
        public const string OpenRouterApiBaseUrl = "https://openrouter.ai/api/v1/";

        public static bool Apply(AiProviderProfileSettings profile)
        {
            ArgumentNullException.ThrowIfNull(profile);

            if (!string.Equals(profile.Id, AiProviderProfileSettings.OpenRouterId, StringComparison.OrdinalIgnoreCase)
                || string.IsNullOrWhiteSpace(profile.BaseUrl)
                || !Uri.TryCreate(profile.BaseUrl.Trim(), UriKind.Absolute, out var uri))
            {
                return false;
            }

            var isOpenRouterHost = string.Equals(uri.Host, "openrouter.ai", StringComparison.OrdinalIgnoreCase)
                || string.Equals(uri.Host, "www.openrouter.ai", StringComparison.OrdinalIgnoreCase);
            var isLegacyWebsiteRoot = isOpenRouterHost
                && (string.IsNullOrEmpty(uri.AbsolutePath) || uri.AbsolutePath == "/")
                && string.IsNullOrEmpty(uri.Query)
                && string.IsNullOrEmpty(uri.Fragment);

            if (!isLegacyWebsiteRoot)
                return false;

            profile.BaseUrl = OpenRouterApiBaseUrl;
            return true;
        }
    }
}
