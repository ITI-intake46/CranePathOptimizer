using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using CraneOptimizer.AI.Configuration;
using CraneOptimizer.AI.DTOs;
using CraneOptimizer.AI.Runtime;
using CraneOptimizer.AI.Security.Credentials;

namespace CraneOptimizer.AI.Providers.OpenAiCompatible
{
    /// <summary>
    /// Raw HTTP adapter for OpenAI-compatible chat-completions APIs. Provider-specific details
    /// are supplied by a profile, allowing OpenRouter, OpenAI, Azure OpenAI and compatible
    /// gateways to share one governed implementation without storing secrets in JSON.
    /// </summary>
    public sealed class OpenAiCompatibleChatProvider : IAiChatProvider, IDisposable
    {
        private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

        private readonly string _providerId;
        private readonly IAiCopilotSettingsService _settingsService;
        private readonly IAiCredentialStore _credentialStore;
        private readonly HttpClient _httpClient;
        private bool _disposed;

        public OpenAiCompatibleChatProvider(
            string providerId,
            IAiCopilotSettingsService settingsService,
            IAiCredentialStore credentialStore)
            : this(providerId, settingsService, credentialStore, new HttpClientHandler())
        {
        }

        public OpenAiCompatibleChatProvider(
            string providerId,
            IAiCopilotSettingsService settingsService,
            IAiCredentialStore credentialStore,
            HttpMessageHandler httpMessageHandler)
        {
            _providerId = string.IsNullOrWhiteSpace(providerId)
                ? throw new ArgumentException("A provider id is required.", nameof(providerId))
                : providerId.Trim();
            _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
            _credentialStore = credentialStore ?? throw new ArgumentNullException(nameof(credentialStore));
            _httpClient = new HttpClient(httpMessageHandler ?? throw new ArgumentNullException(nameof(httpMessageHandler)), disposeHandler: true)
            {
                Timeout = Timeout.InfiniteTimeSpan
            };

            var profile = AiProviderProfileResolver.Resolve(_settingsService, _providerId);
            Descriptor = new AiProviderDescriptor(
                profile.Id,
                profile.DisplayName,
                AiProviderCapabilities.Chat
                | (profile.SupportsStreaming ? AiProviderCapabilities.Streaming : AiProviderCapabilities.None)
                | AiProviderCapabilities.StructuredOutput,
                profile.IsLocal,
                string.IsNullOrWhiteSpace(profile.BaseUrl) ? null : profile.BaseUrl.TrimEnd('/'));
        }

        public AiProviderDescriptor Descriptor { get; }

        public Task<AiChatResponseDto> AskAsync(AiChatRequestDto request, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(request);
            return SendAsync(request, null, cancellationToken);
        }

        public Task<AiChatResponseDto> AskStreamingAsync(
            AiChatRequestDto request,
            Action<AiStreamingUpdateDto> onUpdate,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(request);
            ArgumentNullException.ThrowIfNull(onUpdate);
            return SendAsync(request, onUpdate, cancellationToken);
        }

        private async Task<AiChatResponseDto> SendAsync(
            AiChatRequestDto request,
            Action<AiStreamingUpdateDto>? onUpdate,
            CancellationToken cancellationToken)
        {
            var profile = AiProviderProfileResolver.Resolve(_settingsService, _providerId);
            if (!profile.Enabled)
                return Failure(request, $"{profile.DisplayName} is disabled in AI provider settings.");
            if (string.IsNullOrWhiteSpace(request.Model))
                return Failure(request, $"Select a model for {profile.DisplayName} before sending the request.");

            string? secret;
            try
            {
                secret = RequiresCredential(profile) ? _credentialStore.Read(profile.CredentialTarget) : string.Empty;
            }
            catch (Exception ex)
            {
                return Failure(request, $"Could not access the protected credential for {profile.DisplayName}: {ex.Message}");
            }

            if (RequiresCredential(profile) && string.IsNullOrWhiteSpace(secret))
                return Failure(request, $"No API key is stored for {profile.DisplayName}. Open AI Studio, choose Configure, and save a key.");

            Uri endpoint;
            try
            {
                endpoint = AiProviderProfileResolver.BuildEndpoint(profile, profile.ChatCompletionsPath);
            }
            catch (Exception ex)
            {
                return Failure(request, ex.Message);
            }

            using var timeoutCts = request.TimeoutSeconds is > 0
                ? new CancellationTokenSource(TimeSpan.FromSeconds(request.TimeoutSeconds.Value))
                : null;
            using var linkedCts = timeoutCts == null
                ? CancellationTokenSource.CreateLinkedTokenSource(cancellationToken)
                : CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

            var streaming = onUpdate != null && request.PreferStreaming && profile.SupportsStreaming;
            try
            {
                var messages = new List<ChatMessage>();
                if (!string.IsNullOrWhiteSpace(request.SystemPrompt))
                    messages.Add(new ChatMessage("system", request.SystemPrompt.Trim()));
                messages.Add(new ChatMessage("user", BuildUserPrompt(request)));

                var payload = new ChatCompletionRequest(
                    Model: request.Model.Trim(),
                    Messages: messages,
                    Stream: streaming,
                    Temperature: profile.SendTemperature ? request.Temperature : null);

                using var message = new HttpRequestMessage(HttpMethod.Post, endpoint)
                {
                    Content = new StringContent(JsonSerializer.Serialize(payload, JsonOptions), Encoding.UTF8, "application/json")
                };
                message.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue(streaming ? "text/event-stream" : "application/json"));
                ApplyAuthentication(message, profile, secret);
                ApplyAdditionalHeaders(message, profile);

                using var response = await _httpClient.SendAsync(
                    message,
                    streaming ? HttpCompletionOption.ResponseHeadersRead : HttpCompletionOption.ResponseContentRead,
                    linkedCts.Token).ConfigureAwait(false);

                if (!response.IsSuccessStatusCode)
                    return await BuildFailureAsync(response, request, profile, linkedCts.Token).ConfigureAwait(false);

                var isEventStream = string.Equals(
                    response.Content.Headers.ContentType?.MediaType,
                    "text/event-stream",
                    StringComparison.OrdinalIgnoreCase);
                return streaming && isEventStream
                    ? await ReadStreamingAsync(response, request, onUpdate!, linkedCts.Token).ConfigureAwait(false)
                    : await ReadBufferedAsync(response, request, onUpdate, linkedCts.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException ex) when (!cancellationToken.IsCancellationRequested)
            {
                return Failure(request, $"{profile.DisplayName} timed out. Increase the provider timeout or choose a faster model. {ex.Message}");
            }
            catch (HttpRequestException ex)
            {
                return Failure(request, $"Could not connect to {profile.DisplayName} at {endpoint.GetLeftPart(UriPartial.Authority)}. {ex.Message}");
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                return Failure(request, $"{profile.DisplayName} request failed: {ex.Message}");
            }
        }

        private static async Task<AiChatResponseDto> ReadBufferedAsync(
            HttpResponseMessage response,
            AiChatRequestDto request,
            Action<AiStreamingUpdateDto>? onUpdate,
            CancellationToken cancellationToken)
        {
            var json = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            if (TryExtractProviderError(json, out var providerError))
                return Failure(request, providerError);
            var text = ExtractBufferedContent(json);
            if (string.IsNullOrWhiteSpace(text))
                return Failure(request, "The provider returned a successful response without visible assistant text.");

            onUpdate?.Invoke(new AiStreamingUpdateDto
            {
                Delta = text,
                AccumulatedContent = text,
                IsComplete = true
            });
            return Success(request, text);
        }

        private static async Task<AiChatResponseDto> ReadStreamingAsync(
            HttpResponseMessage response,
            AiChatRequestDto request,
            Action<AiStreamingUpdateDto> onUpdate,
            CancellationToken cancellationToken)
        {
            var builder = new StringBuilder();
            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            using var reader = new StreamReader(stream, Encoding.UTF8);

            while (!reader.EndOfStream)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var line = await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false);
                if (string.IsNullOrWhiteSpace(line) || line.StartsWith(':'))
                    continue;
                if (!line.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
                    continue;

                var data = line[5..].Trim();
                if (data.Equals("[DONE]", StringComparison.OrdinalIgnoreCase))
                    break;

                if (TryExtractProviderError(data, out var streamError))
                    return Failure(request, streamError);

                var delta = ExtractStreamingDelta(data);
                if (string.IsNullOrEmpty(delta))
                    continue;

                builder.Append(delta);
                onUpdate(new AiStreamingUpdateDto
                {
                    Delta = delta,
                    AccumulatedContent = builder.ToString(),
                    IsComplete = false
                });
            }

            var text = builder.ToString().Trim();
            if (string.IsNullOrWhiteSpace(text))
                return Failure(request, "The provider returned an empty streaming response.");

            onUpdate(new AiStreamingUpdateDto
            {
                Delta = string.Empty,
                AccumulatedContent = text,
                IsComplete = true
            });
            return Success(request, text);
        }

        private static string ExtractBufferedContent(string json)
        {
            try
            {
                using var document = JsonDocument.Parse(json);
                var root = document.RootElement;
                if (!root.TryGetProperty("choices", out var choices) || choices.ValueKind != JsonValueKind.Array || choices.GetArrayLength() == 0)
                    return string.Empty;
                var choice = choices[0];
                if (!choice.TryGetProperty("message", out var message))
                    return string.Empty;
                if (message.TryGetProperty("content", out var content))
                {
                    var text = ExtractTextContent(content);
                    if (!string.IsNullOrWhiteSpace(text))
                        return text;
                }
                return message.TryGetProperty("refusal", out var refusal) && refusal.ValueKind == JsonValueKind.String
                    ? refusal.GetString() ?? string.Empty
                    : string.Empty;
            }
            catch (JsonException)
            {
                return string.Empty;
            }
        }

        private static string ExtractStreamingDelta(string json)
        {
            try
            {
                using var document = JsonDocument.Parse(json);
                var root = document.RootElement;
                if (!root.TryGetProperty("choices", out var choices) || choices.ValueKind != JsonValueKind.Array || choices.GetArrayLength() == 0)
                    return string.Empty;
                var choice = choices[0];
                if (!choice.TryGetProperty("delta", out var delta) || !delta.TryGetProperty("content", out var content))
                    return string.Empty;
                return ExtractTextContent(content);
            }
            catch (JsonException)
            {
                return string.Empty;
            }
        }


        private static bool TryExtractProviderError(string json, out string errorMessage)
        {
            errorMessage = string.Empty;
            try
            {
                using var document = JsonDocument.Parse(json);
                if (!document.RootElement.TryGetProperty("error", out var error))
                    return false;

                if (error.ValueKind == JsonValueKind.String)
                    errorMessage = Trim(error.GetString());
                else if (error.ValueKind == JsonValueKind.Object
                         && error.TryGetProperty("message", out var message)
                         && message.ValueKind == JsonValueKind.String)
                    errorMessage = Trim(message.GetString());

                if (string.IsNullOrWhiteSpace(errorMessage))
                    errorMessage = "The provider interrupted the streaming response with an error.";
                return true;
            }
            catch (JsonException)
            {
                return false;
            }
        }

        private static string ExtractTextContent(JsonElement content)
        {
            if (content.ValueKind == JsonValueKind.String)
                return content.GetString() ?? string.Empty;
            if (content.ValueKind != JsonValueKind.Array)
                return string.Empty;

            var builder = new StringBuilder();
            foreach (var part in content.EnumerateArray())
            {
                if (part.ValueKind == JsonValueKind.String)
                {
                    builder.Append(part.GetString());
                    continue;
                }

                if (part.ValueKind == JsonValueKind.Object
                    && part.TryGetProperty("text", out var text)
                    && text.ValueKind == JsonValueKind.String)
                {
                    builder.Append(text.GetString());
                }
            }
            return builder.ToString();
        }

        private static async Task<AiChatResponseDto> BuildFailureAsync(
            HttpResponseMessage response,
            AiChatRequestDto request,
            AiProviderProfileSettings profile,
            CancellationToken cancellationToken)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            var detail = ExtractErrorMessage(body);
            var retryAfter = response.Headers.RetryAfter?.Delta;
            var suffix = retryAfter.HasValue ? $" Retry after approximately {Math.Ceiling(retryAfter.Value.TotalSeconds):N0} seconds." : string.Empty;
            var message = response.StatusCode switch
            {
                HttpStatusCode.Unauthorized => $"{profile.DisplayName} rejected the API key. Replace the stored credential and test the connection.",
                HttpStatusCode.PaymentRequired => $"{profile.DisplayName} reports insufficient credits or quota.",
                HttpStatusCode.Forbidden => $"{profile.DisplayName} denied this request. Check account permissions, provider policy, and model access.",
                HttpStatusCode.NotFound => $"{profile.DisplayName} could not find the endpoint or model '{request.Model}'.",
                HttpStatusCode.RequestTimeout => $"{profile.DisplayName} timed out while processing the request.",
                HttpStatusCode.TooManyRequests => $"{profile.DisplayName} rate limited the request.{suffix}",
                HttpStatusCode.BadGateway or HttpStatusCode.ServiceUnavailable => $"{profile.DisplayName} or the selected upstream model is temporarily unavailable.{suffix}",
                _ => $"{profile.DisplayName} returned {(int)response.StatusCode} {response.ReasonPhrase}."
            };

            if (!string.IsNullOrWhiteSpace(detail))
                message += " " + detail;
            return Failure(request, message);
        }

        private static string ExtractErrorMessage(string body)
        {
            if (string.IsNullOrWhiteSpace(body))
                return string.Empty;
            try
            {
                using var document = JsonDocument.Parse(body);
                if (document.RootElement.TryGetProperty("error", out var error))
                {
                    if (error.ValueKind == JsonValueKind.String)
                        return Trim(error.GetString());
                    if (error.ValueKind == JsonValueKind.Object
                        && error.TryGetProperty("message", out var message)
                        && message.ValueKind == JsonValueKind.String)
                        return Trim(message.GetString());
                }
            }
            catch (JsonException)
            {
                // Avoid surfacing raw HTML or provider payloads that might contain sensitive data.
            }
            return string.Empty;
        }

        private static string BuildUserPrompt(AiChatRequestDto request)
        {
            var builder = new StringBuilder();
            builder.AppendLine("User question:");
            builder.AppendLine(string.IsNullOrWhiteSpace(request.UserPrompt)
                ? "Explain the current crane optimization result."
                : request.UserPrompt.Trim());
            if (!string.IsNullOrWhiteSpace(request.ContextJson))
            {
                builder.AppendLine();
                builder.AppendLine("Grounded application context:");
                builder.AppendLine(request.ContextJson);
            }
            return builder.ToString().Trim();
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
            {
                message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", secret);
                return;
            }

            var headerName = string.IsNullOrWhiteSpace(profile.ApiKeyHeaderName) ? "api-key" : profile.ApiKeyHeaderName.Trim();
            if (!message.Headers.TryAddWithoutValidation(headerName, secret))
                throw new InvalidOperationException($"The configured API-key header '{headerName}' is invalid.");
        }

        private static void ApplyAdditionalHeaders(HttpRequestMessage message, AiProviderProfileSettings profile)
        {
            foreach (var header in profile.AdditionalHeaders ?? new Dictionary<string, string>())
            {
                if (string.IsNullOrWhiteSpace(header.Key) || string.IsNullOrWhiteSpace(header.Value))
                    continue;
                if (IsSensitiveHeaderName(header.Key))
                    continue;
                message.Headers.TryAddWithoutValidation(header.Key.Trim(), header.Value.Trim());
            }
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

        private static string Trim(string? value) =>
            string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim().Length <= 400 ? value.Trim() : value.Trim()[..400] + "...";

        private static AiChatResponseDto Success(AiChatRequestDto request, string text) => new()
        {
            Success = true,
            Content = text,
            ProviderId = request.ProviderId,
            CorrelationId = request.CorrelationId
        };

        private static AiChatResponseDto Failure(AiChatRequestDto request, string error) => new()
        {
            Success = false,
            ErrorMessage = error,
            ProviderId = request.ProviderId,
            CorrelationId = request.CorrelationId
        };

        public void Dispose()
        {
            if (_disposed)
                return;
            _httpClient.Dispose();
            _disposed = true;
        }

        private sealed record ChatCompletionRequest(
            [property: JsonPropertyName("model")] string Model,
            [property: JsonPropertyName("messages")] IReadOnlyList<ChatMessage> Messages,
            [property: JsonPropertyName("stream")] bool Stream,
            [property: JsonPropertyName("temperature")] double? Temperature);

        private sealed record ChatMessage(
            [property: JsonPropertyName("role")] string Role,
            [property: JsonPropertyName("content")] string Content);
    }
}
