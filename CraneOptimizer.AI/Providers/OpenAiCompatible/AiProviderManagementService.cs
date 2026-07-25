using System.Net.Http.Headers;
using System.Text.Json;
using CraneOptimizer.AI.Configuration;
using CraneOptimizer.AI.DTOs;
using CraneOptimizer.AI.Interfaces;
using CraneOptimizer.AI.Providers.Ollama;
using CraneOptimizer.AI.Runtime;
using CraneOptimizer.AI.Security.Credentials;

namespace CraneOptimizer.AI.Providers.OpenAiCompatible
{
    public sealed class AiProviderManagementService : IAiProviderManagementService, IDisposable
    {
        private readonly IAiCopilotSettingsService _settingsService;
        private readonly IAiProviderRegistry _providerRegistry;
        private readonly OllamaModelManagementService _ollamaManagement;
        private readonly IAiCredentialStore _credentialStore;
        private readonly HttpClient _httpClient;
        private bool _disposed;

        public AiProviderManagementService(
            IAiCopilotSettingsService settingsService,
            IAiProviderRegistry providerRegistry,
            OllamaModelManagementService ollamaManagement,
            IAiCredentialStore credentialStore)
            : this(settingsService, providerRegistry, ollamaManagement, credentialStore, new HttpClientHandler())
        {
        }

        public AiProviderManagementService(
            IAiCopilotSettingsService settingsService,
            IAiProviderRegistry providerRegistry,
            OllamaModelManagementService ollamaManagement,
            IAiCredentialStore credentialStore,
            HttpMessageHandler httpMessageHandler)
        {
            _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
            _providerRegistry = providerRegistry ?? throw new ArgumentNullException(nameof(providerRegistry));
            _ollamaManagement = ollamaManagement ?? throw new ArgumentNullException(nameof(ollamaManagement));
            _credentialStore = credentialStore ?? throw new ArgumentNullException(nameof(credentialStore));
            _httpClient = new HttpClient(httpMessageHandler ?? throw new ArgumentNullException(nameof(httpMessageHandler)), disposeHandler: true)
            {
                Timeout = TimeSpan.FromSeconds(30)
            };
        }

        public IReadOnlyList<AiProviderDescriptor> GetProviders()
        {
            var enabledIds = _settingsService.Current.ProviderProfiles
                .Where(profile => profile.Enabled)
                .Select(profile => profile.Id)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            return _providerRegistry.Providers
                .Where(provider => enabledIds.Contains(provider.Id))
                .ToArray();
        }

        public AiProviderProfileSettings GetProfile(string providerId)
        {
            var profile = AiProviderProfileResolver.Resolve(_settingsService, providerId);
            return Clone(profile);
        }

        public void UpdateProfile(AiProviderProfileSettings profile)
        {
            ArgumentNullException.ThrowIfNull(profile);
            if (string.IsNullOrWhiteSpace(profile.Id))
                throw new ArgumentException("The provider profile id is required.", nameof(profile));

            _settingsService.Update(settings =>
            {
                var existing = settings.ProviderProfiles.FirstOrDefault(item =>
                    string.Equals(item.Id, profile.Id, StringComparison.OrdinalIgnoreCase));
                var destinationChanged = existing != null && HasCloudDestinationChanged(existing, profile);
                if (existing == null)
                    settings.ProviderProfiles.Add(Clone(profile));
                else
                    CopyInto(profile, existing);

                if (destinationChanged)
                    settings.CloudConsentAcceptedProviderIds.Remove(profile.Id);

                if (string.Equals(profile.Id, AiProviderProfileSettings.OllamaId, StringComparison.OrdinalIgnoreCase)
                    && !string.IsNullOrWhiteSpace(profile.BaseUrl))
                    settings.OllamaEndpoint = profile.BaseUrl;
            });
        }

        public bool HasCredential(string providerId)
        {
            var profile = AiProviderProfileResolver.Resolve(_settingsService, providerId);
            return !RequiresCredential(profile) || _credentialStore.Contains(profile.CredentialTarget);
        }

        public void SaveCredential(string providerId, string apiKey)
        {
            var profile = AiProviderProfileResolver.Resolve(_settingsService, providerId);
            if (!RequiresCredential(profile))
                throw new InvalidOperationException($"{profile.DisplayName} does not use an API key.");
            _credentialStore.Save(profile.CredentialTarget, apiKey);
            _settingsService.Update(settings => settings.CloudConsentAcceptedProviderIds.Remove(profile.Id));
        }

        public void DeleteCredential(string providerId)
        {
            var profile = AiProviderProfileResolver.Resolve(_settingsService, providerId);
            if (RequiresCredential(profile))
                _credentialStore.Delete(profile.CredentialTarget);
            _settingsService.Update(settings => settings.CloudConsentAcceptedProviderIds.Remove(profile.Id));
        }

        public async Task<AiProviderStatusDto> GetStatusAsync(string providerId, CancellationToken cancellationToken = default)
        {
            var profile = AiProviderProfileResolver.Resolve(_settingsService, providerId);
            if (profile.IsLocal || profile.ProviderKind.Equals("Ollama", StringComparison.OrdinalIgnoreCase))
            {
                var status = await _ollamaManagement.GetStatusAsync(cancellationToken).ConfigureAwait(false);
                return new AiProviderStatusDto
                {
                    ProviderId = profile.Id,
                    DisplayName = profile.DisplayName,
                    IsAvailable = status.IsAvailable,
                    IsLocal = true,
                    RequiresCredential = false,
                    HasCredential = true,
                    Endpoint = status.Endpoint,
                    Models = status.InstalledModels,
                    ErrorMessage = status.ErrorMessage
                };
            }

            if (string.IsNullOrWhiteSpace(profile.BaseUrl))
                return Offline(profile, "Configure the provider base URL before testing the connection.");
            if (!HasCredential(profile.Id))
                return Offline(profile, "No protected API key is stored for this provider.");
            if (!profile.SupportsModelDiscovery || string.IsNullOrWhiteSpace(profile.ModelsPath))
                return await TestConfiguredModelAsync(profile, cancellationToken).ConfigureAwait(false);

            try
            {
                var endpoint = AiProviderProfileResolver.BuildEndpoint(profile, profile.ModelsPath);
                using var request = new HttpRequestMessage(HttpMethod.Get, endpoint);
                request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                var secret = RequiresCredential(profile) ? _credentialStore.Read(profile.CredentialTarget) : string.Empty;
                ApplyAuthentication(request, profile, secret);
                ApplyAdditionalHeaders(request, profile);
                using var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
                var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
                if (!response.IsSuccessStatusCode)
                {
                    return Offline(profile, response.StatusCode switch
                    {
                        System.Net.HttpStatusCode.Unauthorized => "The stored API key was rejected.",
                        System.Net.HttpStatusCode.TooManyRequests => "The provider rate limited the model-list request.",
                        _ => $"The provider returned {(int)response.StatusCode} {response.ReasonPhrase}."
                    });
                }

                var models = ParseModels(body);
                return new AiProviderStatusDto
                {
                    ProviderId = profile.Id,
                    DisplayName = profile.DisplayName,
                    IsAvailable = true,
                    IsLocal = false,
                    RequiresCredential = RequiresCredential(profile),
                    HasCredential = !RequiresCredential(profile) || SafeHasCredential(profile),
                    Endpoint = profile.BaseUrl?.TrimEnd('/') ?? string.Empty,
                    Models = models
                };
            }
            catch (OperationCanceledException ex) when (!cancellationToken.IsCancellationRequested)
            {
                return Offline(profile, "The provider connection test timed out. " + ex.Message);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                return Offline(profile, ex.Message);
            }
        }


        private async Task<AiProviderStatusDto> TestConfiguredModelAsync(
            AiProviderProfileSettings profile,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(profile.DefaultModel))
                return Offline(profile, "Enter a model or deployment ID before testing this provider.");
            if (!_providerRegistry.TryResolve(profile.Id, out var provider))
                return Offline(profile, "The provider is disabled or was not registered.");

            var response = await provider.AskAsync(new AiChatRequestDto
            {
                ProviderId = profile.Id,
                Model = profile.DefaultModel,
                SystemPrompt = "You are validating an AI provider connection. Reply with OK.",
                UserPrompt = "Reply with OK.",
                ContextJson = string.Empty,
                Temperature = profile.SendTemperature ? 0.0 : null,
                TimeoutSeconds = 30,
                PreferStreaming = false,
                Metadata = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["ContainsProjectContext"] = "false",
                    ["Purpose"] = "ProviderConnectionTest"
                }
            }, cancellationToken).ConfigureAwait(false);

            return response.Success
                ? new AiProviderStatusDto
                {
                    ProviderId = profile.Id,
                    DisplayName = profile.DisplayName,
                    IsAvailable = true,
                    IsLocal = false,
                    RequiresCredential = RequiresCredential(profile),
                    HasCredential = !RequiresCredential(profile) || SafeHasCredential(profile),
                    Endpoint = profile.BaseUrl?.TrimEnd('/') ?? string.Empty,
                    Models = new[] { profile.DefaultModel }
                }
                : Offline(profile, response.ErrorMessage ?? "The provider connection test failed.");
        }

        private AiProviderStatusDto Offline(AiProviderProfileSettings profile, string error) => new()
        {
            ProviderId = profile.Id,
            DisplayName = profile.DisplayName,
            IsAvailable = false,
            IsLocal = profile.IsLocal,
            RequiresCredential = RequiresCredential(profile),
            HasCredential = !RequiresCredential(profile) || SafeHasCredential(profile),
            Endpoint = profile.BaseUrl?.TrimEnd('/') ?? string.Empty,
            ErrorMessage = error
        };

        private bool SafeHasCredential(AiProviderProfileSettings profile)
        {
            try { return _credentialStore.Contains(profile.CredentialTarget); }
            catch { return false; }
        }

        private static IReadOnlyList<string> ParseModels(string json)
        {
            try
            {
                using var document = JsonDocument.Parse(json);
                var root = document.RootElement;
                var data = root.ValueKind == JsonValueKind.Array
                    ? root
                    : root.TryGetProperty("data", out var standardData) && standardData.ValueKind == JsonValueKind.Array
                        ? standardData
                        : root.TryGetProperty("models", out var modelsData) && modelsData.ValueKind == JsonValueKind.Array
                            ? modelsData
                            : default;
                if (data.ValueKind != JsonValueKind.Array)
                    return Array.Empty<string>();

                return data.EnumerateArray()
                    .Select(item => item.ValueKind switch
                    {
                        JsonValueKind.String => item.GetString(),
                        JsonValueKind.Object when item.TryGetProperty("id", out var id) && id.ValueKind == JsonValueKind.String => id.GetString(),
                        JsonValueKind.Object when item.TryGetProperty("name", out var name) && name.ValueKind == JsonValueKind.String => name.GetString(),
                        _ => null
                    })
                    .Where(id => !string.IsNullOrWhiteSpace(id))
                    .Select(id => id!)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(id => id, StringComparer.OrdinalIgnoreCase)
                    .ToArray();
            }
            catch (JsonException)
            {
                return Array.Empty<string>();
            }
        }

        private static bool RequiresCredential(AiProviderProfileSettings profile) =>
            !profile.AuthenticationMode.Equals("None", StringComparison.OrdinalIgnoreCase);

        private static void ApplyAuthentication(HttpRequestMessage message, AiProviderProfileSettings profile, string? secret)
        {
            if (!RequiresCredential(profile))
                return;
            if (string.IsNullOrWhiteSpace(secret))
                throw new InvalidOperationException("No provider API key is stored.");

            if (profile.AuthenticationMode.Equals("Bearer", StringComparison.OrdinalIgnoreCase))
                message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", secret);
            else
            {
                var headerName = string.IsNullOrWhiteSpace(profile.ApiKeyHeaderName) ? "api-key" : profile.ApiKeyHeaderName.Trim();
                if (!message.Headers.TryAddWithoutValidation(headerName, secret))
                    throw new InvalidOperationException($"The configured API-key header '{headerName}' is invalid.");
            }
        }

        private static void ApplyAdditionalHeaders(HttpRequestMessage message, AiProviderProfileSettings profile)
        {
            foreach (var header in profile.AdditionalHeaders ?? new Dictionary<string, string>())
            {
                if (string.IsNullOrWhiteSpace(header.Key) || string.IsNullOrWhiteSpace(header.Value))
                    continue;
                if (IsSensitiveHeaderName(header.Key))
                    continue;
                message.Headers.TryAddWithoutValidation(header.Key, header.Value);
            }
        }


        private static bool HasCloudDestinationChanged(AiProviderProfileSettings existing, AiProviderProfileSettings updated)
        {
            if (existing.IsLocal || updated.IsLocal)
                return false;

            return !string.Equals(existing.BaseUrl?.TrimEnd('/'), updated.BaseUrl?.TrimEnd('/'), StringComparison.OrdinalIgnoreCase)
                || !string.Equals(existing.ChatCompletionsPath?.TrimStart('/'), updated.ChatCompletionsPath?.TrimStart('/'), StringComparison.OrdinalIgnoreCase)
                || !string.Equals(existing.AuthenticationMode, updated.AuthenticationMode, StringComparison.OrdinalIgnoreCase)
                || !string.Equals(existing.ApiKeyHeaderName, updated.ApiKeyHeaderName, StringComparison.OrdinalIgnoreCase)
                || !HeadersEqual(existing.AdditionalHeaders, updated.AdditionalHeaders);
        }

        private static bool HeadersEqual(
            IReadOnlyDictionary<string, string>? left,
            IReadOnlyDictionary<string, string>? right)
        {
            left ??= new Dictionary<string, string>();
            right ??= new Dictionary<string, string>();
            if (left.Count != right.Count)
                return false;

            foreach (var pair in left)
            {
                if (!right.TryGetValue(pair.Key, out var value)
                    || !string.Equals(pair.Value, value, StringComparison.Ordinal))
                    return false;
            }
            return true;
        }


        private static bool IsSensitiveHeaderName(string name)
        {
            var normalized = (name ?? string.Empty).Replace("-", string.Empty, StringComparison.Ordinal).Trim();
            return normalized.Contains("authorization", StringComparison.OrdinalIgnoreCase)
                || normalized.Contains("apikey", StringComparison.OrdinalIgnoreCase)
                || normalized.Contains("token", StringComparison.OrdinalIgnoreCase)
                || normalized.Contains("secret", StringComparison.OrdinalIgnoreCase)
                || normalized.Contains("credential", StringComparison.OrdinalIgnoreCase)
                || normalized.Equals("contenttype", StringComparison.OrdinalIgnoreCase);
        }

        private static AiProviderProfileSettings Clone(AiProviderProfileSettings profile)
        {
            var clone = new AiProviderProfileSettings();
            CopyInto(profile, clone);
            return clone;
        }

        private static void CopyInto(AiProviderProfileSettings source, AiProviderProfileSettings target)
        {
            target.Id = source.Id.Trim();
            target.DisplayName = source.DisplayName.Trim();
            target.ProviderKind = source.ProviderKind.Trim();
            target.Enabled = source.Enabled;
            target.IsLocal = source.IsLocal;
            target.BaseUrl = source.BaseUrl?.Trim() ?? string.Empty;
            target.ChatCompletionsPath = source.ChatCompletionsPath?.Trim() ?? "chat/completions";
            target.ModelsPath = source.ModelsPath?.Trim() ?? string.Empty;
            target.SupportsModelDiscovery = source.SupportsModelDiscovery;
            target.SupportsStreaming = source.SupportsStreaming;
            target.SendTemperature = source.SendTemperature;
            target.AuthenticationMode = source.AuthenticationMode?.Trim() ?? "Bearer";
            target.ApiKeyHeaderName = source.ApiKeyHeaderName?.Trim() ?? "Authorization";
            target.CredentialTarget = source.CredentialTarget?.Trim() ?? string.Empty;
            target.DefaultModel = source.DefaultModel?.Trim() ?? string.Empty;
            target.AdditionalHeaders = new Dictionary<string, string>(source.AdditionalHeaders ?? new Dictionary<string, string>(), StringComparer.OrdinalIgnoreCase);
        }

        public void Dispose()
        {
            if (_disposed) return;
            _httpClient.Dispose();
            _disposed = true;
        }
    }
}
