using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using CraneOptimizer.AI.DTOs;
using CraneOptimizer.AI.Interfaces;

namespace CraneOptimizer.AI.Providers.Ollama
{
    public sealed class OllamaChatService : IAiStreamingChatService, IDisposable
    {
        private static readonly Uri DefaultEndpoint = new("http://localhost:11434/");
        private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

        private readonly HttpClient _httpClient;
        private bool _disposed;

        public OllamaChatService()
        {
            _httpClient = new HttpClient
            {
                BaseAddress = DefaultEndpoint,
                Timeout = Timeout.InfiniteTimeSpan
            };
        }

        public async Task<AiChatResponseDto> AskAsync(AiChatRequestDto request, CancellationToken cancellationToken = default)
        {
            return await AskStreamingAsync(new AiChatRequestDto
            {
                Model = request.Model,
                SystemPrompt = request.SystemPrompt,
                UserPrompt = request.UserPrompt,
                ContextJson = request.ContextJson,
                Temperature = request.Temperature,
                TimeoutSeconds = request.TimeoutSeconds,
                PreferStreaming = false
            }, _ => { }, cancellationToken).ConfigureAwait(false);
        }

        public async Task<AiChatResponseDto> AskStreamingAsync(
            AiChatRequestDto request,
            Action<AiStreamingUpdateDto> onUpdate,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(request.Model))
            {
                return new AiChatResponseDto
                {
                    Success = false,
                    ErrorMessage = "No Ollama model was selected. Enter a local model name, for example llama3.2, mistral, or qwen2.5."
                };
            }

            using var timeoutCts = request.TimeoutSeconds is > 0
                ? new CancellationTokenSource(TimeSpan.FromSeconds(request.TimeoutSeconds.Value))
                : null;
            using var linkedCts = timeoutCts == null
                ? CancellationTokenSource.CreateLinkedTokenSource(cancellationToken)
                : CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

            var effectiveToken = linkedCts.Token;

            try
            {
                var messages = new List<OllamaMessage>
                {
                    new("system", request.SystemPrompt),
                    new("user", BuildUserPrompt(request))
                };

                var payload = new OllamaChatRequest(
                    Model: request.Model.Trim(),
                    Messages: messages,
                    Stream: request.PreferStreaming,
                    Options: new OllamaOptions(Temperature: request.Temperature ?? 0.2));

                var json = JsonSerializer.Serialize(payload, JsonOptions);
                using var message = new HttpRequestMessage(HttpMethod.Post, "api/chat")
                {
                    Content = new StringContent(json, Encoding.UTF8, "application/json")
                };

                using var response = await _httpClient.SendAsync(
                    message,
                    request.PreferStreaming ? HttpCompletionOption.ResponseHeadersRead : HttpCompletionOption.ResponseContentRead,
                    effectiveToken).ConfigureAwait(false);

                if (!response.IsSuccessStatusCode)
                {
                    var responseJson = await response.Content.ReadAsStringAsync(effectiveToken).ConfigureAwait(false);
                    return new AiChatResponseDto
                    {
                        Success = false,
                        ErrorMessage = $"Ollama returned {(int)response.StatusCode} {response.ReasonPhrase}: {TrimForDisplay(responseJson)}"
                    };
                }

                return request.PreferStreaming
                    ? await ReadStreamingResponseAsync(response, onUpdate, effectiveToken).ConfigureAwait(false)
                    : await ReadBufferedResponseAsync(response, onUpdate, effectiveToken).ConfigureAwait(false);
            }
            catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
            {
                return new AiChatResponseDto
                {
                    Success = false,
                    ErrorMessage = "Ollama timed out. The local model may still be loading; try again or choose a smaller model. " + ex.Message
                };
            }
            catch (HttpRequestException ex)
            {
                return new AiChatResponseDto
                {
                    Success = false,
                    ErrorMessage = "Could not connect to Ollama at http://localhost:11434. Start Ollama and ensure a model is installed. " + ex.Message
                };
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                return new AiChatResponseDto
                {
                    Success = false,
                    ErrorMessage = "AI chat failed: " + ex.Message
                };
            }
        }

        private static async Task<AiChatResponseDto> ReadBufferedResponseAsync(
            HttpResponseMessage response,
            Action<AiStreamingUpdateDto> onUpdate,
            CancellationToken cancellationToken)
        {
            var responseJson = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            var result = JsonSerializer.Deserialize<OllamaChatResponse>(responseJson, JsonOptions);
            var text = result?.Message?.Content?.Trim();

            if (string.IsNullOrWhiteSpace(text))
            {
                return new AiChatResponseDto
                {
                    Success = false,
                    ErrorMessage = "Ollama returned an empty response. Check that the selected model is installed and responding."
                };
            }

            onUpdate(new AiStreamingUpdateDto
            {
                Delta = text,
                AccumulatedContent = text,
                IsComplete = true
            });

            return new AiChatResponseDto
            {
                Success = true,
                Content = text
            };
        }

        private static async Task<AiChatResponseDto> ReadStreamingResponseAsync(
            HttpResponseMessage response,
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
                if (string.IsNullOrWhiteSpace(line))
                    continue;

                OllamaChatResponse? chunk;
                try
                {
                    chunk = JsonSerializer.Deserialize<OllamaChatResponse>(line, JsonOptions);
                }
                catch (JsonException)
                {
                    continue;
                }

                var delta = chunk?.Message?.Content ?? string.Empty;
                if (!string.IsNullOrEmpty(delta))
                {
                    builder.Append(delta);
                    onUpdate(new AiStreamingUpdateDto
                    {
                        Delta = delta,
                        AccumulatedContent = builder.ToString(),
                        IsComplete = chunk?.Done == true
                    });
                }

                if (chunk?.Done == true)
                    break;
            }

            var text = builder.ToString().Trim();
            if (string.IsNullOrWhiteSpace(text))
            {
                return new AiChatResponseDto
                {
                    Success = false,
                    ErrorMessage = "Ollama returned an empty streaming response. Check that the selected model is installed and responding."
                };
            }

            onUpdate(new AiStreamingUpdateDto
            {
                Delta = string.Empty,
                AccumulatedContent = text,
                IsComplete = true
            });

            return new AiChatResponseDto
            {
                Success = true,
                Content = text
            };
        }

        private static string BuildUserPrompt(AiChatRequestDto request)
        {
            var builder = new StringBuilder();
            builder.AppendLine("User question:");
            builder.AppendLine(string.IsNullOrWhiteSpace(request.UserPrompt) ? "Explain the current crane optimization result." : request.UserPrompt.Trim());
            builder.AppendLine();
            builder.AppendLine("Internal application and optimization context. Use it as reference material, but do not describe it as JSON or explain its schema in your answer:");
            builder.AppendLine(request.ContextJson);
            return builder.ToString();
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

        private sealed record OllamaChatRequest(
            [property: JsonPropertyName("model")] string Model,
            [property: JsonPropertyName("messages")] IReadOnlyList<OllamaMessage> Messages,
            [property: JsonPropertyName("stream")] bool Stream,
            [property: JsonPropertyName("options")] OllamaOptions Options);

        private sealed record OllamaMessage(
            [property: JsonPropertyName("role")] string Role,
            [property: JsonPropertyName("content")] string Content);

        private sealed record OllamaOptions(
            [property: JsonPropertyName("temperature")] double Temperature);

        private sealed record OllamaChatResponse(
            [property: JsonPropertyName("message")] OllamaMessage? Message,
            [property: JsonPropertyName("done")] bool Done = false);
    }
}
