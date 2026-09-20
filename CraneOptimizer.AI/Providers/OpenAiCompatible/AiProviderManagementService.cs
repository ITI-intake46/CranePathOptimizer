using System.Net;
using System.Net.Http;
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

            AiProviderProfileMigrations.Apply(profile);
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
                {
                    settings.OllamaEndpoint = profile.BaseUrl;
                }
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
            if (AiEndpointSecurityClassifier.IsLocalEndpoint(profile.BaseUrl)
                && profile.ProviderKind.Equals("Ollama", StringComparison.OrdinalIgnoreCase))
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
                    return Offline(profile, BuildStatusFailureMessage(response, body));

                if (!IsJsonPayload(response.Content.Headers.ContentType?.MediaType, body))
                {
                    return Offline(profile,
                        $"{profile.DisplayName} returned a non-JSON response from the models endpoint. " +
                        "Verify that the base URL points to the provider API root rather than its public website.");
                }

                if (!TryParseModels(body, out var models, out var parseError))
                {
                    return Offline(profile,
                        $"{profile.DisplayName} returned an unexpected models response. {parseError}");
                }

                if (models.Count == 0)
                {
                    var emptyMessage = $"{profile.DisplayName} returned a valid model-list response, but it contained no model IDs. " +
                                       "Verify account/model visibility or enter an exact model ID manually.";
                    if (RequiresNonEmptyDiscoveryResult(profile))
                        return Offline(profile, emptyMessage);

                    return Available(profile, models, emptyMessage);
                }

                return Available(profile, models);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                return Offline(profile, "The provider connection test timed out. Check the endpoint or try again.");
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (HttpRequestException ex)
            {
                return Offline(profile, $"Could not connect to {profile.DisplayName}. {SanitizeDiagnostic(ex.Message)}");
            }
            catch (Exception ex)
            {
                return Offline(profile, SanitizeDiagnostic(ex.Message));
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
                ? Available(profile, new[] { profile.DefaultModel })
                : Offline(profile, response.ErrorMessage ?? "The provider connection test failed.");
        }

        private AiProviderStatusDto Available(
            AiProviderProfileSettings profile,
            IReadOnlyList<string> models,
            string? warning = null) => new()
        {
            ProviderId = profile.Id,
            DisplayName = profile.DisplayName,
            IsAvailable = true,
            IsLocal = profile.IsLocal,
            RequiresCredential = RequiresCredential(profile),
            HasCredential = !RequiresCredential(profile) || SafeHasCredential(profile),
            Endpoint = profile.BaseUrl?.TrimEnd('/') ?? string.Empty,
            Models = models,
            ErrorMessage = warning
        };

        private AiProviderStatusDto Offline(AiProviderProfileSettings profile, string error) => new()
        {
            ProviderId = profile.Id,
            DisplayName = profile.DisplayName,
            IsAvailable = false,
            IsLocal = profile.IsLocal,
            RequiresCredential = RequiresCredential(profile),
            HasCredential = !RequiresCredential(profile) || SafeHasCredential(profile),
            Endpoint = profile.BaseUrl?.TrimEnd('/') ?? string.Empty,
            ErrorMessage = SanitizeDiagnostic(error)
        };

        private bool SafeHasCredential(AiProviderProfileSettings profile)
        {
            try { return _credentialStore.Contains(profile.CredentialTarget); }
            catch { return false; }
        }

        private static bool TryParseModels(
            string json,
            out IReadOnlyList<string> models,
            out string error)
        {
            models = Array.Empty<string>();
            error = string.Empty;
            try
            {
                using var document = JsonDocument.Parse(json);
                var root = document.RootElement;
                if (root.ValueKind == JsonValueKind.Object
                    && root.TryGetProperty("error", out var providerError))
                {
                    error = ExtractProviderError(providerError);
                    if (string.IsNullOrWhiteSpace(error))
                        error = "The response contained a provider error object.";
                    return false;
                }

                var data = root.ValueKind == JsonValueKind.Array
                    ? root
                    : root.ValueKind == JsonValueKind.Object
                      && root.TryGetProperty("data", out var standardData)
                      && standardData.ValueKind == JsonValueKind.Array
                        ? standardData
                        : root.ValueKind == JsonValueKind.Object
                          && root.TryGetProperty("models", out var modelsData)
                          && modelsData.ValueKind == JsonValueKind.Array
                            ? modelsData
                            : default;

                if (data.ValueKind != JsonValueKind.Array)
                {
                    error = "Expected a JSON array or an object containing a 'data' or 'models' array.";
                    return false;
                }

                models = data.EnumerateArray()
                    .Select(item => item.ValueKind switch
                    {
                        JsonValueKind.String => item.GetString(),
                        JsonValueKind.Object when item.TryGetProperty("id", out var id) && id.ValueKind == JsonValueKind.String => id.GetString(),
                        JsonValueKind.Object when item.TryGetProperty("name", out var name) && name.ValueKind == JsonValueKind.String => name.GetString(),
                        _ => null
                    })
                    .Where(id => !string.IsNullOrWhiteSpace(id))
                    .Select(id => id!.Trim())
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(id => id, StringComparer.OrdinalIgnoreCase)
                    .ToArray();
                return true;
            }
            catch (JsonException)
            {
                error = "The response was not valid JSON.";
                return false;
            }
        }

        private static bool IsJsonPayload(string? mediaType, string body)
        {
            if (string.Equals(mediaType, "text/html", StringComparison.OrdinalIgnoreCase)
                || string.Equals(mediaType, "application/xhtml+xml", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            if (!string.IsNullOrWhiteSpace(mediaType)
                && (string.Equals(mediaType, "application/json", StringComparison.OrdinalIgnoreCase)
                    || mediaType.EndsWith("+json", StringComparison.OrdinalIgnoreCase)))
            {
                return true;
            }

            var trimmed = body?.TrimStart() ?? string.Empty;
            return trimmed.StartsWith('{') || trimmed.StartsWith('[');
        }

        private static string BuildStatusFailureMessage(HttpResponseMessage response, string body)
        {
            var detail = IsJsonPayload(response.Content.Headers.ContentType?.MediaType, body)
                ? ExtractErrorMessage(body)
                : string.Empty;
            var retryAfter = response.Headers.RetryAfter?.Delta;
            var suffix = retryAfter.HasValue
                ? $" Retry after approximately {Math.Ceiling(retryAfter.Value.TotalSeconds):N0} seconds."
                : string.Empty;
            var message = response.StatusCode switch
            {
                HttpStatusCode.Unauthorized => "The stored API key was rejected.",
                HttpStatusCode.PaymentRequired => "The provider reports insufficient credits or quota.",
                HttpStatusCode.Forbidden => "The provider denied access. Check account permissions and model policy.",
                HttpStatusCode.NotFound => "The configured models endpoint was not found. Check the base URL and models path.",
                HttpStatusCode.TooManyRequests => "The provider rate limited the model-list request." + suffix,
                HttpStatusCode.BadGateway or HttpStatusCode.ServiceUnavailable => "The provider is temporarily unavailable." + suffix,
                _ => $"The provider returned {(int)response.StatusCode} {response.ReasonPhrase}."
            };
            return string.IsNullOrWhiteSpace(detail) ? message : message + " " + detail;
        }

        private static string ExtractErrorMessage(string body)
        {
            if (string.IsNullOrWhiteSpace(body))
                return string.Empty;
            try
            {
                using var document = JsonDocument.Parse(body);
                return document.RootElement.TryGetProperty("error", out var error)
                    ? ExtractProviderError(error)
                    : string.Empty;
            }
            catch (JsonException)
            {
                return string.Empty;
            }
        }

        private static string ExtractProviderError(JsonElement error)
        {
            if (error.ValueKind == JsonValueKind.String)
                return SanitizeDiagnostic(error.GetString());
            if (error.ValueKind == JsonValueKind.Object
                && error.TryGetProperty("message", out var message)
                && message.ValueKind == JsonValueKind.String)
            {
                return SanitizeDiagnostic(message.GetString());
            }
            return string.Empty;
        }

        private static bool RequiresNonEmptyDiscoveryResult(AiProviderProfileSettings profile) =>
            string.Equals(profile.Id, AiProviderProfileSettings.OpenRouterId, StringComparison.OrdinalIgnoreCase)
            || string.Equals(profile.Id, AiProviderProfileSettings.OpenAiId, StringComparison.OrdinalIgnoreCase);

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
            var existingLocal = AiEndpointSecurityClassifier.IsLocalEndpoint(existing.BaseUrl);
            var updatedLocal = AiEndpointSecurityClassifier.IsLocalEndpoint(updated.BaseUrl);
            if (existingLocal && updatedLocal)
                return false;

            return existingLocal != updatedLocal
                || !string.Equals(existing.BaseUrl?.TrimEnd('/'), updated.BaseUrl?.TrimEnd('/'), StringComparison.OrdinalIgnoreCase)
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

        private static string SanitizeDiagnostic(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return string.Empty;
            var trimmed = value.Trim();
            if (trimmed.Length > 400)
                trimmed = trimmed[..400] + "...";
            return trimmed.Replace("\r", " ", StringComparison.Ordinal)
                .Replace("\n", " ", StringComparison.Ordinal);
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
