using System.Diagnostics;
using CraneOptimizer.AI.DTOs;
using CraneOptimizer.AI.Interfaces;
using CraneOptimizer.AI.Security;

namespace CraneOptimizer.AI.Runtime
{
    /// <summary>
    /// Governed entry point for all model calls. It applies request policy, resolves a provider,
    /// preserves correlation metadata, and records content-free operational telemetry.
    /// </summary>
    public sealed class AiChatGateway : IAiStreamingChatService
    {
        private readonly IAiProviderRegistry _providerRegistry;
        private readonly IAiRequestPolicy _requestPolicy;
        private readonly IAiTelemetrySink _telemetrySink;
        private readonly IAiSensitiveDataRedactor _redactor;

        public AiChatGateway(
            IAiProviderRegistry providerRegistry,
            IAiRequestPolicy requestPolicy,
            IAiTelemetrySink telemetrySink,
            IAiSensitiveDataRedactor redactor)
        {
            _providerRegistry = providerRegistry ?? throw new ArgumentNullException(nameof(providerRegistry));
            _requestPolicy = requestPolicy ?? throw new ArgumentNullException(nameof(requestPolicy));
            _telemetrySink = telemetrySink ?? throw new ArgumentNullException(nameof(telemetrySink));
            _redactor = redactor ?? throw new ArgumentNullException(nameof(redactor));
        }

        public Task<AiChatResponseDto> AskAsync(
            AiChatRequestDto request,
            CancellationToken cancellationToken = default)
        {
            return ExecuteAsync(request, onUpdate: null, cancellationToken);
        }

        public Task<AiChatResponseDto> AskStreamingAsync(
            AiChatRequestDto request,
            Action<AiStreamingUpdateDto> onUpdate,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(onUpdate);
            return ExecuteAsync(request, onUpdate, cancellationToken);
        }

        private async Task<AiChatResponseDto> ExecuteAsync(
            AiChatRequestDto request,
            Action<AiStreamingUpdateDto>? onUpdate,
            CancellationToken cancellationToken)
        {
            var correlationId = string.IsNullOrWhiteSpace(request?.CorrelationId)
                ? Guid.NewGuid().ToString("N")
                : request.CorrelationId.Trim();
            var startedAt = DateTimeOffset.UtcNow;
            var stopwatch = Stopwatch.StartNew();
            var providerId = request?.ProviderId?.Trim() ?? string.Empty;
            AiChatResponseDto? response = null;

            try
            {
                var decision = _requestPolicy.Evaluate(request!);
                if (!decision.IsAllowed)
                {
                    response = Failure(decision.ErrorMessage, providerId, correlationId);
                    return response;
                }

                if (!_providerRegistry.TryResolve(request.ProviderId, out var provider))
                {
                    response = Failure(
                        $"AI provider '{request.ProviderId}' is not registered. Select an available provider and retry.",
                        providerId,
                        correlationId);
                    return response;
                }

                providerId = provider.Descriptor.Id;
                var governedRequest = CopyRequest(request, providerId, correlationId, onUpdate != null);
                response = onUpdate == null
                    ? await provider.AskAsync(governedRequest, cancellationToken).ConfigureAwait(false)
                    : await provider.AskStreamingAsync(governedRequest, onUpdate, cancellationToken).ConfigureAwait(false);

                return WithExecutionMetadata(response, providerId, correlationId, stopwatch.ElapsedMilliseconds, _redactor);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                response = Failure("The governed AI runtime failed: " + _redactor.Redact(ex.Message), providerId, correlationId);
                return response;
            }
            finally
            {
                stopwatch.Stop();
                var finalResponse = response;
                _telemetrySink.Record(new AiTelemetryEvent(
                    CorrelationId: correlationId,
                    ProviderId: providerId,
                    Model: request?.Model ?? string.Empty,
                    StartedAt: startedAt,
                    ElapsedMilliseconds: stopwatch.ElapsedMilliseconds,
                    Success: finalResponse?.Success == true,
                    Streaming: onUpdate != null,
                    SystemPromptCharacters: request?.SystemPrompt?.Length ?? 0,
                    UserPromptCharacters: request?.UserPrompt?.Length ?? 0,
                    ContextCharacters: request?.ContextJson?.Length ?? 0,
                    ResponseCharacters: finalResponse?.Content?.Length ?? 0,
                    ErrorCategory: CategorizeError(finalResponse?.ErrorMessage)));
            }
        }

        private static AiChatRequestDto CopyRequest(
            AiChatRequestDto request,
            string providerId,
            string correlationId,
            bool streaming)
        {
            return new AiChatRequestDto
            {
                ProviderId = providerId,
                CorrelationId = correlationId,
                Model = request.Model,
                SystemPrompt = request.SystemPrompt,
                UserPrompt = request.UserPrompt,
                ContextJson = request.ContextJson,
                Temperature = request.Temperature,
                TimeoutSeconds = request.TimeoutSeconds,
                PreferStreaming = streaming && request.PreferStreaming,
                Metadata = request.Metadata
            };
        }

        private static AiChatResponseDto WithExecutionMetadata(
            AiChatResponseDto? response,
            string providerId,
            string correlationId,
            long elapsedMilliseconds,
            IAiSensitiveDataRedactor redactor)
        {
            if (response == null)
                return Failure("The AI provider returned no response.", providerId, correlationId);

            return new AiChatResponseDto
            {
                Success = response.Success,
                Content = response.Content,
                ErrorMessage = redactor.Redact(response.ErrorMessage),
                ProviderId = providerId,
                CorrelationId = correlationId,
                ElapsedMilliseconds = elapsedMilliseconds
            };
        }

        private static AiChatResponseDto Failure(string message, string providerId, string correlationId)
        {
            return new AiChatResponseDto
            {
                Success = false,
                ErrorMessage = message,
                ProviderId = providerId,
                CorrelationId = correlationId
            };
        }

        private static string CategorizeError(string? errorMessage)
        {
            if (string.IsNullOrWhiteSpace(errorMessage))
                return string.Empty;

            if (errorMessage.Contains("timeout", StringComparison.OrdinalIgnoreCase)
                || errorMessage.Contains("timed out", StringComparison.OrdinalIgnoreCase))
                return "timeout";

            if (errorMessage.Contains("connect", StringComparison.OrdinalIgnoreCase)
                || errorMessage.Contains("offline", StringComparison.OrdinalIgnoreCase))
                return "connectivity";

            if (errorMessage.Contains("policy", StringComparison.OrdinalIgnoreCase)
                || errorMessage.Contains("exceeds", StringComparison.OrdinalIgnoreCase)
                || errorMessage.Contains("select", StringComparison.OrdinalIgnoreCase))
                return "policy";

            return "provider";
        }
    }
}
