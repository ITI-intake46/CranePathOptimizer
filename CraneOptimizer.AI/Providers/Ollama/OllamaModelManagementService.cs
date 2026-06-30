using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using CraneOptimizer.AI.DTOs;
using CraneOptimizer.AI.Interfaces;

namespace CraneOptimizer.AI.Providers.Ollama
{
    public sealed class OllamaModelManagementService : IAiModelManagementService, IDisposable
    {
        private static readonly Uri DefaultEndpoint = new("http://localhost:11434/");
        private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

        private readonly HttpClient _httpClient;
        private bool _disposed;

        public OllamaModelManagementService()
        {
            _httpClient = new HttpClient
            {
                BaseAddress = DefaultEndpoint,
                Timeout = TimeSpan.FromMinutes(20)
            };
        }

        public async Task<AiModelStatusDto> GetStatusAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                var version = await TryGetVersionAsync(cancellationToken).ConfigureAwait(false);
                var models = await TryGetModelsAsync(cancellationToken).ConfigureAwait(false);

                return new AiModelStatusDto
                {
                    IsAvailable = true,
                    Endpoint = DefaultEndpoint.ToString().TrimEnd('/'),
                    Version = version,
                    InstalledModels = models
                };
            }
            catch (HttpRequestException ex)
            {
                return Offline(ex.Message);
            }
            catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
            {
                return Offline("Ollama status check timed out. " + ex.Message);
            }
            catch (Exception ex)
            {
                return Offline(ex.Message);
            }
        }

        public async Task<AiChatResponseDto> PullModelAsync(string modelName, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(modelName))
            {
                return new AiChatResponseDto
                {
                    Success = false,
                    ErrorMessage = "Enter an Ollama model name before pulling, for example llama3.2 or qwen3."
                };
            }

            try
            {
                var payload = JsonSerializer.Serialize(new OllamaPullRequest(modelName.Trim(), Stream: false), JsonOptions);
                using var content = new StringContent(payload, Encoding.UTF8, "application/json");
                using var response = await _httpClient.PostAsync("api/pull", content, cancellationToken).ConfigureAwait(false);
                var responseJson = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

                if (!response.IsSuccessStatusCode)
                {
                    return new AiChatResponseDto
                    {
                        Success = false,
                        ErrorMessage = $"Ollama pull returned {(int)response.StatusCode} {response.ReasonPhrase}: {TrimForDisplay(responseJson)}"
                    };
                }

                return new AiChatResponseDto
                {
                    Success = true,
                    Content = $"Model '{modelName.Trim()}' is available in Ollama."
                };
            }
            catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
            {
                return new AiChatResponseDto
                {
                    Success = false,
                    ErrorMessage = "Model pull timed out. The download may still be running in Ollama; check `ollama list`. " + ex.Message
                };
            }
            catch (HttpRequestException ex)
            {
                return new AiChatResponseDto
                {
                    Success = false,
                    ErrorMessage = "Could not connect to Ollama at http://localhost:11434. Start Ollama before pulling a model. " + ex.Message
                };
            }
            catch (Exception ex)
            {
                return new AiChatResponseDto
                {
                    Success = false,
                    ErrorMessage = "Ollama model pull failed: " + ex.Message
                };
            }
        }

        private async Task<string> TryGetVersionAsync(CancellationToken cancellationToken)
        {
            using var response = await _httpClient.GetAsync("api/version", cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
                return string.Empty;

            var json = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            var version = JsonSerializer.Deserialize<OllamaVersionResponse>(json, JsonOptions);
            return version?.Version ?? string.Empty;
        }

        private async Task<IReadOnlyList<string>> TryGetModelsAsync(CancellationToken cancellationToken)
        {
            using var response = await _httpClient.GetAsync("api/tags", cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
                return Array.Empty<string>();

            var json = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            var tags = JsonSerializer.Deserialize<OllamaTagsResponse>(json, JsonOptions);
            return tags?.Models?
                .Select(model => string.IsNullOrWhiteSpace(model.Name) ? model.Model : model.Name)
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
                .ToArray() ?? Array.Empty<string>();
        }

        private static AiModelStatusDto Offline(string errorMessage)
        {
            return new AiModelStatusDto
            {
                IsAvailable = false,
                Endpoint = DefaultEndpoint.ToString().TrimEnd('/'),
                ErrorMessage = errorMessage,
                InstalledModels = Array.Empty<string>()
            };
        }

        private static string TrimForDisplay(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return string.Empty;

            return value.Length <= 500 ? value : value[..500] + "...";
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _httpClient.Dispose();
            _disposed = true;
        }

        private sealed record OllamaPullRequest(
            [property: JsonPropertyName("model")] string Model,
            [property: JsonPropertyName("stream")] bool Stream);

        private sealed record OllamaVersionResponse(
            [property: JsonPropertyName("version")] string? Version);

        private sealed record OllamaTagsResponse(
            [property: JsonPropertyName("models")] IReadOnlyList<OllamaModel>? Models);

        private sealed record OllamaModel(
            [property: JsonPropertyName("name")] string? Name,
            [property: JsonPropertyName("model")] string? Model);
    }
}
