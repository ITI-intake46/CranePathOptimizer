using CraneOptimizer.AI.Configuration;
using CraneOptimizer.AI.DTOs;
using CraneOptimizer.AI.Runtime;
using CraneOptimizer.AI.Security;
using CraneOptimizer.AI.StructuredOutput;
using Xunit;

namespace CraneOptimizer.Application.Tests.AI
{
    public sealed class AiPlatformFoundationTests
    {
        [Fact]
        public void ProviderRegistry_ResolvesConfiguredDefaultProviderCaseInsensitively()
        {
            var settings = new StubSettingsService(new AiCopilotSettings
            {
                DefaultProviderId = "LOCAL-TEST"
            });
            var provider = new FakeProvider("local-test");
            var registry = new AiProviderRegistry(new[] { provider }, settings);

            var resolved = registry.TryResolve(null, out var result);

            Assert.True(resolved);
            Assert.Same(provider, result);
            Assert.Single(registry.Providers);
        }

        [Fact]
        public async Task ChatGateway_AppliesProviderAndCorrelationMetadataAndRecordsSafeTelemetry()
        {
            var settings = new StubSettingsService(new AiCopilotSettings
            {
                DefaultProviderId = "fake",
                TelemetryBufferCapacity = 20
            });
            var provider = new FakeProvider("fake", "grounded response");
            var registry = new AiProviderRegistry(new[] { provider }, settings);
            var telemetry = new InMemoryAiTelemetrySink(settings);
            var gateway = new AiChatGateway(
                registry,
                new DefaultAiRequestPolicy(settings),
                telemetry,
                new AiSensitiveDataRedactor());

            var response = await gateway.AskAsync(new AiChatRequestDto
            {
                Model = "test-model",
                SystemPrompt = "Use grounded evidence only.",
                UserPrompt = "Explain the result.",
                ContextJson = "{\"coverage\":0.92}"
            });

            Assert.True(response.Success);
            Assert.Equal("grounded response", response.Content);
            Assert.Equal("fake", response.ProviderId);
            Assert.False(string.IsNullOrWhiteSpace(response.CorrelationId));
            Assert.Equal(response.CorrelationId, provider.LastRequest?.CorrelationId);
            Assert.Equal("fake", provider.LastRequest?.ProviderId);

            var recorded = Assert.Single(telemetry.Snapshot());
            Assert.True(recorded.Success);
            Assert.Equal("fake", recorded.ProviderId);
            Assert.Equal("test-model", recorded.Model);
            Assert.Equal(0, recorded.ErrorCategory.Length);
            Assert.Equal("grounded response".Length, recorded.ResponseCharacters);
        }

        [Fact]
        public async Task ChatGateway_RejectsOversizedPromptBeforeCallingProvider()
        {
            var settings = new StubSettingsService(new AiCopilotSettings
            {
                DefaultProviderId = "fake",
                MaxUserPromptCharacters = 5
            });
            var provider = new FakeProvider("fake");
            var telemetry = new InMemoryAiTelemetrySink(settings);
            var gateway = new AiChatGateway(
                new AiProviderRegistry(new[] { provider }, settings),
                new DefaultAiRequestPolicy(settings),
                telemetry,
                new AiSensitiveDataRedactor());

            var response = await gateway.AskAsync(new AiChatRequestDto
            {
                Model = "test-model",
                UserPrompt = "123456"
            });

            Assert.False(response.Success);
            Assert.Contains("exceeds", response.ErrorMessage, StringComparison.OrdinalIgnoreCase);
            Assert.Equal(0, provider.CallCount);
            Assert.Equal("policy", Assert.Single(telemetry.Snapshot()).ErrorCategory);
        }

        [Fact]
        public async Task ChatGateway_StreamsThroughProviderAndPreservesFinalResult()
        {
            var settings = new StubSettingsService(new AiCopilotSettings
            {
                DefaultProviderId = "fake"
            });
            var provider = new FakeProvider("fake", "one two");
            var telemetry = new InMemoryAiTelemetrySink(settings);
            var gateway = new AiChatGateway(
                new AiProviderRegistry(new[] { provider }, settings),
                new DefaultAiRequestPolicy(settings),
                telemetry,
                new AiSensitiveDataRedactor());
            var updates = new List<AiStreamingUpdateDto>();

            var response = await gateway.AskStreamingAsync(
                new AiChatRequestDto
                {
                    Model = "test-model",
                    UserPrompt = "Stream this answer.",
                    PreferStreaming = true
                },
                updates.Add);

            Assert.True(response.Success);
            Assert.Equal("one two", response.Content);
            Assert.NotEmpty(updates);
            Assert.True(updates[^1].IsComplete);
            Assert.True(Assert.Single(telemetry.Snapshot()).Streaming);
        }

        [Fact]
        public void StructuredOutputParser_ExtractsFencedJsonAndDeserializesTypedPayload()
        {
            var parser = new AiStructuredOutputParser();

            var result = parser.Parse<SamplePayload>("""
                Here is the validated result:
                ```json
                { "name": "tower-crane", "count": 2 }
                ```
                """);

            Assert.True(result.Success, result.ErrorMessage);
            Assert.NotNull(result.Value);
            Assert.Equal("tower-crane", result.Value!.Name);
            Assert.Equal(2, result.Value.Count);
        }

        [Fact]
        public void SensitiveDataRedactor_RemovesCommonCredentialShapes()
        {
            var redactor = new AiSensitiveDataRedactor();
            const string bearer = "abcDEF0123456789.token.value";
            const string apiKey = "super-secret-value";
            const string openAiKey = "sk-abcdefghijklmnopqrstuvwxyz123456";

            var result = redactor.Redact(
                $"Authorization: Bearer {bearer}; api_key={apiKey}; provider={openAiKey}");

            Assert.DoesNotContain(bearer, result, StringComparison.Ordinal);
            Assert.DoesNotContain(apiKey, result, StringComparison.Ordinal);
            Assert.DoesNotContain(openAiKey, result, StringComparison.Ordinal);
            Assert.Contains("[REDACTED]", result, StringComparison.Ordinal);
        }

        private sealed record SamplePayload(string Name, int Count);

        private sealed class FakeProvider : IAiChatProvider
        {
            private readonly string _response;

            public FakeProvider(string id, string response = "ok")
            {
                _response = response;
                Descriptor = new AiProviderDescriptor(
                    id,
                    "Fake provider",
                    AiProviderCapabilities.Chat | AiProviderCapabilities.Streaming | AiProviderCapabilities.StructuredOutput,
                    IsLocal: true);
            }

            public AiProviderDescriptor Descriptor { get; }
            public int CallCount { get; private set; }
            public AiChatRequestDto? LastRequest { get; private set; }

            public Task<AiChatResponseDto> AskAsync(
                AiChatRequestDto request,
                CancellationToken cancellationToken = default)
            {
                CallCount++;
                LastRequest = request;
                return Task.FromResult(new AiChatResponseDto
                {
                    Success = true,
                    Content = _response
                });
            }

            public Task<AiChatResponseDto> AskStreamingAsync(
                AiChatRequestDto request,
                Action<AiStreamingUpdateDto> onUpdate,
                CancellationToken cancellationToken = default)
            {
                CallCount++;
                LastRequest = request;
                onUpdate(new AiStreamingUpdateDto
                {
                    Delta = _response,
                    AccumulatedContent = _response,
                    IsComplete = true
                });

                return Task.FromResult(new AiChatResponseDto
                {
                    Success = true,
                    Content = _response
                });
            }
        }

        private sealed class StubSettingsService : IAiCopilotSettingsService
        {
            public StubSettingsService(AiCopilotSettings settings)
            {
                Current = settings;
            }

            public AiCopilotSettings Current { get; }

            public AiCopilotSettings Load() => Current;

            public void Save()
            {
            }

            public void Update(Action<AiCopilotSettings> updateAction)
            {
                updateAction(Current);
            }
        }
    }
}
