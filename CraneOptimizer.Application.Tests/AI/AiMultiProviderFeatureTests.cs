using System.Net;
using System.Net.Http;
using System.Text;
using CraneOptimizer.AI.Configuration;
using CraneOptimizer.AI.DTOs;
using CraneOptimizer.AI.Providers.OpenAiCompatible;
using CraneOptimizer.AI.Providers.Ollama;
using CraneOptimizer.AI.Runtime;
using CraneOptimizer.AI.Security.Credentials;

namespace CraneOptimizer.Application.Tests.AI
{
    public sealed class AiMultiProviderFeatureTests
    {
        [Fact]
        public void DefaultProviderProfiles_IncludeLocalCloudAndCustomOptionsWithoutSecretFields()
        {
            var profiles = AiProviderProfileSettings.CreateDefaults();

            Assert.Contains(profiles, profile => profile.Id == AiProviderProfileSettings.OllamaId && profile.IsLocal);
            Assert.Contains(profiles, profile => profile.Id == AiProviderProfileSettings.OpenRouterId && !profile.IsLocal);
            Assert.Contains(profiles, profile => profile.Id == AiProviderProfileSettings.OpenAiId && !profile.IsLocal);
            Assert.Contains(profiles, profile => profile.Id == AiProviderProfileSettings.AzureOpenAiId && !profile.IsLocal);
            Assert.Contains(profiles, profile => profile.Id == AiProviderProfileSettings.CustomOpenAiId && !profile.IsLocal);
            Assert.All(profiles.Where(profile => !profile.IsLocal), profile => Assert.False(string.IsNullOrWhiteSpace(profile.CredentialTarget)));
            Assert.All(profiles, profile => Assert.DoesNotContain(profile.GetType().GetProperties(), property =>
                property.Name.Contains("Secret", StringComparison.OrdinalIgnoreCase)
                || property.Name.Equals("ApiKey", StringComparison.OrdinalIgnoreCase)));
        }

        [Fact]
        public async Task OpenAiCompatibleProvider_UsesBearerCredentialAndParsesChatCompletion()
        {
            var settings = CreateSettings();
            var credentials = new InMemoryCredentialStore();
            credentials.Save("CraneOptimizer/AI/TestCloud", "test-secret");
            string? capturedScheme = null;
            string? capturedCredential = null;
            string? capturedUri = null;
            var handler = new StubHandler(request =>
            {
                capturedScheme = request.Headers.Authorization?.Scheme;
                capturedCredential = request.Headers.Authorization?.Parameter;
                capturedUri = request.RequestUri?.ToString();
                return JsonResponse("""
                    {"choices":[{"message":{"role":"assistant","content":"Grounded cloud answer"}}]}
                    """);
            });
            using var provider = new OpenAiCompatibleChatProvider("test-cloud", settings, credentials, handler);

            var response = await provider.AskAsync(new AiChatRequestDto
            {
                ProviderId = "test-cloud",
                Model = "vendor/model",
                SystemPrompt = "Use evidence.",
                UserPrompt = "Explain.",
                ContextJson = "{\"status\":\"blocked\"}",
                PreferStreaming = false
            });

            Assert.True(response.Success, response.ErrorMessage);
            Assert.Equal("Grounded cloud answer", response.Content);
            Assert.Equal("Bearer", capturedScheme);
            Assert.Equal("test-secret", capturedCredential);
            Assert.Equal("https://example.test/v1/chat/completions", capturedUri);
        }


        [Fact]
        public async Task OpenAiCompatibleProvider_SupportsCustomApiKeyHeaderAndAdditionalHeaders()
        {
            var settings = CreateSettings();
            var profile = settings.Current.ProviderProfiles.Single();
            profile.AuthenticationMode = "ApiKeyHeader";
            profile.ApiKeyHeaderName = "x-provider-key";
            profile.AdditionalHeaders["X-Organization"] = "engineering";
            var credentials = new InMemoryCredentialStore();
            credentials.Save(profile.CredentialTarget, "custom-secret");
            string? apiKey = null;
            string? organization = null;
            var handler = new StubHandler(request =>
            {
                apiKey = request.Headers.TryGetValues("x-provider-key", out var keyValues) ? keyValues.Single() : null;
                organization = request.Headers.TryGetValues("X-Organization", out var values) ? values.Single() : null;
                return JsonResponse("{\"choices\":[{\"message\":{\"content\":\"ok\"}}]}");
            });
            using var provider = new OpenAiCompatibleChatProvider("test-cloud", settings, credentials, handler);

            var response = await provider.AskAsync(new AiChatRequestDto
            {
                ProviderId = "test-cloud",
                Model = "vendor/model",
                UserPrompt = "Test headers."
            });

            Assert.True(response.Success, response.ErrorMessage);
            Assert.Equal("custom-secret", apiKey);
            Assert.Equal("engineering", organization);
        }


        [Fact]
        public async Task OpenAiCompatibleProvider_RejectsCredentialedCloudProviderOverPlainHttp()
        {
            var settings = CreateSettings();
            var profile = settings.Current.ProviderProfiles.Single();
            profile.BaseUrl = "http://external.example.test/v1/";
            var credentials = new InMemoryCredentialStore();
            credentials.Save(profile.CredentialTarget, "protected-secret");
            var handler = new StubHandler(_ => throw new InvalidOperationException("Network should not be called."));
            using var provider = new OpenAiCompatibleChatProvider("test-cloud", settings, credentials, handler);

            var response = await provider.AskAsync(new AiChatRequestDto
            {
                ProviderId = "test-cloud",
                Model = "vendor/model",
                UserPrompt = "Test transport protection."
            });

            Assert.False(response.Success);
            Assert.Contains("HTTPS", response.ErrorMessage, StringComparison.OrdinalIgnoreCase);
            Assert.Equal(0, handler.CallCount);
        }

        [Fact]
        public async Task OpenAiCompatibleProvider_AllowsExplicitNoAuthenticationProfile()
        {
            var settings = CreateSettings();
            var profile = settings.Current.ProviderProfiles.Single();
            profile.AuthenticationMode = "None";
            profile.CredentialTarget = string.Empty;
            var credentials = new InMemoryCredentialStore();
            var handler = new StubHandler(request =>
            {
                Assert.Null(request.Headers.Authorization);
                return JsonResponse("{\"choices\":[{\"message\":{\"content\":\"anonymous gateway ok\"}}]}");
            });
            using var provider = new OpenAiCompatibleChatProvider("test-cloud", settings, credentials, handler);

            var response = await provider.AskAsync(new AiChatRequestDto
            {
                ProviderId = "test-cloud",
                Model = "gateway/model",
                UserPrompt = "Test an internal compatible gateway."
            });

            Assert.True(response.Success, response.ErrorMessage);
            Assert.Equal("anonymous gateway ok", response.Content);
            Assert.Equal(1, handler.CallCount);
        }

        [Fact]
        public async Task OpenAiCompatibleProvider_DoesNotPersistOrSendSensitiveAdditionalHeaders()
        {
            var settings = CreateSettings();
            var profile = settings.Current.ProviderProfiles.Single();
            profile.AdditionalHeaders["Authorization"] = "must-not-be-used";
            profile.AdditionalHeaders["X-Api-Key"] = "must-not-be-used";
            profile.AdditionalHeaders["X-Client-Title"] = "Crane Optimizer";
            var credentials = new InMemoryCredentialStore();
            credentials.Save(profile.CredentialTarget, "protected-secret");
            var handler = new StubHandler(request =>
            {
                Assert.Equal("protected-secret", request.Headers.Authorization?.Parameter);
                Assert.False(request.Headers.Contains("X-Api-Key"));
                Assert.Equal("Crane Optimizer", request.Headers.GetValues("X-Client-Title").Single());
                return JsonResponse("{\"choices\":[{\"message\":{\"content\":\"safe headers\"}}]}");
            });
            using var provider = new OpenAiCompatibleChatProvider("test-cloud", settings, credentials, handler);

            var response = await provider.AskAsync(new AiChatRequestDto
            {
                ProviderId = "test-cloud",
                Model = "vendor/model",
                UserPrompt = "Test header filtering."
            });

            Assert.True(response.Success, response.ErrorMessage);
            Assert.Equal("safe headers", response.Content);
        }

        [Fact]
        public async Task OpenAiCompatibleProvider_ParsesSseStreamingAndCompletes()
        {
            var settings = CreateSettings();
            var credentials = new InMemoryCredentialStore();
            credentials.Save("CraneOptimizer/AI/TestCloud", "test-secret");
            var sse = "data: {\"choices\":[{\"delta\":{\"content\":\"Hello \"}}]}\n\n" +
                      "data: {\"choices\":[{\"delta\":{\"content\":\"world\"}}]}\n\n" +
                      "data: [DONE]\n\n";
            var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(sse, Encoding.UTF8, "text/event-stream")
            });
            using var provider = new OpenAiCompatibleChatProvider("test-cloud", settings, credentials, handler);
            var updates = new List<AiStreamingUpdateDto>();

            var response = await provider.AskStreamingAsync(new AiChatRequestDto
            {
                ProviderId = "test-cloud",
                Model = "vendor/model",
                UserPrompt = "Stream.",
                PreferStreaming = true
            }, updates.Add);

            Assert.True(response.Success, response.ErrorMessage);
            Assert.Equal("Hello world", response.Content);
            Assert.NotEmpty(updates);
            Assert.True(updates[^1].IsComplete);
            Assert.Equal("Hello world", updates[^1].AccumulatedContent);
        }

        [Fact]
        public async Task OpenAiCompatibleProvider_FallsBackToBufferedParsingWhenStreamingEndpointReturnsJson()
        {
            var settings = CreateSettings();
            var credentials = new InMemoryCredentialStore();
            credentials.Save("CraneOptimizer/AI/TestCloud", "test-secret");
            var handler = new StubHandler(_ => JsonResponse("{\"choices\":[{\"message\":{\"content\":\"buffered despite stream request\"}}]}"));
            using var provider = new OpenAiCompatibleChatProvider("test-cloud", settings, credentials, handler);
            var updates = new List<AiStreamingUpdateDto>();

            var response = await provider.AskStreamingAsync(new AiChatRequestDto
            {
                ProviderId = "test-cloud",
                Model = "vendor/model",
                UserPrompt = "Stream if supported.",
                PreferStreaming = true
            }, updates.Add);

            Assert.True(response.Success, response.ErrorMessage);
            Assert.Equal("buffered despite stream request", response.Content);
            Assert.Single(updates);
            Assert.True(updates[0].IsComplete);
        }

        [Fact]
        public async Task OpenAiCompatibleProvider_RejectsMissingCredentialBeforeNetworkCall()
        {
            var settings = CreateSettings();
            var credentials = new InMemoryCredentialStore();
            var handler = new StubHandler(_ => throw new InvalidOperationException("Network should not be called."));
            using var provider = new OpenAiCompatibleChatProvider("test-cloud", settings, credentials, handler);

            var response = await provider.AskAsync(new AiChatRequestDto
            {
                ProviderId = "test-cloud",
                Model = "vendor/model",
                UserPrompt = "Explain."
            });

            Assert.False(response.Success);
            Assert.Contains("No API key", response.ErrorMessage, StringComparison.OrdinalIgnoreCase);
            Assert.Equal(0, handler.CallCount);
        }


        [Fact]
        public async Task ProviderManagement_DiscoversStandardModelListWithoutProjectContext()
        {
            var settings = CreateSettings();
            var credentials = new InMemoryCredentialStore();
            credentials.Save("CraneOptimizer/AI/TestCloud", "test-secret");
            var provider = new StubProvider("test-cloud", isLocal: false);
            var registry = new AiProviderRegistry(new IAiChatProvider[] { provider }, settings);
            var handler = new StubHandler(request =>
            {
                Assert.Equal(HttpMethod.Get, request.Method);
                Assert.Equal("https://example.test/v1/models", request.RequestUri?.ToString());
                Assert.Equal("test-secret", request.Headers.Authorization?.Parameter);
                return JsonResponse("{\"data\":[{\"id\":\"vendor/a\"},{\"id\":\"vendor/b\"}]}");
            });
            using var ollama = new OllamaModelManagementService(settings);
            using var management = new AiProviderManagementService(settings, registry, ollama, credentials, handler);

            var status = await management.GetStatusAsync("test-cloud");

            Assert.True(status.IsAvailable, status.ErrorMessage);
            Assert.Equal(new[] { "vendor/a", "vendor/b" }, status.Models.ToArray());
            Assert.Equal(1, handler.CallCount);
        }

        [Fact]
        public void AiStudioXaml_ExposesProviderModelAndProtectedConfigurationControls()
        {
            var root = FindRepositoryRoot();
            Assert.NotNull(root);
            var xaml = File.ReadAllText(Path.Combine(root!, "CraneOptimizer.AI", "Views", "AiAdvisorWindow.xaml"));
            var dialog = File.ReadAllText(Path.Combine(root!, "CraneOptimizer.AI", "Views", "AiProviderSettingsDialog.xaml"));

            Assert.Contains("SelectedProviderId", xaml);
            Assert.Contains("ConfigureProviderCommand", xaml);
            Assert.Contains("ProviderPrivacyLabel", xaml);
            Assert.Contains("PasswordBox", dialog);
            Assert.Contains("Windows Credential Manager", dialog);
            Assert.Contains("AdditionalHeadersTextBox", dialog);
            Assert.Contains("SendTemperatureCheckBox", dialog);
        }


        [Fact]
        public void CloudRequestPolicy_RequiresConsentForProjectContextButAllowsContextFreeTests()
        {
            var settings = CreateSettings();
            var policy = new DefaultAiRequestPolicy(settings);

            var denied = policy.Evaluate(new AiChatRequestDto
            {
                ProviderId = "test-cloud",
                Model = "vendor/model",
                UserPrompt = "Explain this project.",
                ContextJson = "{\"project\":\"Bahy\"}",
                Metadata = new Dictionary<string, string> { ["ContainsProjectContext"] = "true" }
            });
            var allowedTest = policy.Evaluate(new AiChatRequestDto
            {
                ProviderId = "test-cloud",
                Model = "vendor/model",
                UserPrompt = "Reply with OK.",
                Metadata = new Dictionary<string, string> { ["ContainsProjectContext"] = "false" }
            });

            Assert.False(denied.IsAllowed);
            Assert.Contains("consent", denied.ErrorMessage, StringComparison.OrdinalIgnoreCase);
            Assert.True(allowedTest.IsAllowed, allowedTest.ErrorMessage);

            settings.Current.CloudConsentAcceptedProviderIds.Add("test-cloud");
            var allowedGrounded = policy.Evaluate(new AiChatRequestDto
            {
                ProviderId = "test-cloud",
                Model = "vendor/model",
                UserPrompt = "Explain this project.",
                ContextJson = "{\"project\":\"Bahy\"}",
                Metadata = new Dictionary<string, string> { ["ContainsProjectContext"] = "true" }
            });
            Assert.True(allowedGrounded.IsAllowed, allowedGrounded.ErrorMessage);
        }

        [Fact]
        public void ProviderRegistry_DoesNotSilentlyFallbackWhenRequestedCloudProviderIsUnavailable()
        {
            var settings = CreateSettings();
            var local = new StubProvider("ollama");
            var registry = new AiProviderRegistry(new IAiChatProvider[] { local }, settings);

            Assert.False(registry.TryResolve("test-cloud", out _));
            Assert.True(registry.TryResolve("ollama", out var resolved));
            Assert.Same(local, resolved);
        }

        [Fact]
        public void ProviderSettingsJson_ContainsCredentialReferenceButNeverAnApiKeyValue()
        {
            var settings = CreateSettings().Current;
            var json = System.Text.Json.JsonSerializer.Serialize(settings);

            Assert.Contains("CraneOptimizer/AI/TestCloud", json, StringComparison.Ordinal);
            Assert.DoesNotContain("test-secret", json, StringComparison.Ordinal);
            Assert.DoesNotContain("\"ApiKey\"", json, StringComparison.OrdinalIgnoreCase);
        }

        private static StubSettingsService CreateSettings()
        {
            var settings = new AiCopilotSettings
            {
                DefaultProviderId = "test-cloud",
                ProviderProfiles = new List<AiProviderProfileSettings>
                {
                    new()
                    {
                        Id = "test-cloud",
                        DisplayName = "Test Cloud",
                        BaseUrl = "https://example.test/v1/",
                        ChatCompletionsPath = "chat/completions",
                        ModelsPath = "models",
                        AuthenticationMode = "Bearer",
                        ApiKeyHeaderName = "Authorization",
                        CredentialTarget = "CraneOptimizer/AI/TestCloud",
                        DefaultModel = "vendor/model",
                        SupportsStreaming = true,
                        SupportsModelDiscovery = true
                    }
                }
            };
            return new StubSettingsService(settings);
        }

        private static HttpResponseMessage JsonResponse(string json) => new(HttpStatusCode.OK)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };

        private static string? FindRepositoryRoot()
        {
            var current = new DirectoryInfo(AppContext.BaseDirectory);
            while (current != null)
            {
                if (File.Exists(Path.Combine(current.FullName, "CraneOptimizer.sln")))
                    return current.FullName;
                current = current.Parent;
            }

            current = new DirectoryInfo(Directory.GetCurrentDirectory());
            while (current != null)
            {
                if (File.Exists(Path.Combine(current.FullName, "CraneOptimizer.sln")))
                    return current.FullName;
                current = current.Parent;
            }
            return null;
        }


        private sealed class StubProvider : IAiChatProvider
        {
            public StubProvider(string id, bool isLocal = true) => Descriptor = new AiProviderDescriptor(id, id, AiProviderCapabilities.Chat, isLocal);
            public AiProviderDescriptor Descriptor { get; }
            public Task<AiChatResponseDto> AskAsync(AiChatRequestDto request, CancellationToken cancellationToken = default) =>
                Task.FromResult(new AiChatResponseDto { Success = true, Content = "ok", ProviderId = Descriptor.Id });
            public Task<AiChatResponseDto> AskStreamingAsync(AiChatRequestDto request, Action<AiStreamingUpdateDto> onUpdate, CancellationToken cancellationToken = default) =>
                AskAsync(request, cancellationToken);
        }

        private sealed class StubSettingsService : IAiCopilotSettingsService
        {
            public StubSettingsService(AiCopilotSettings settings) => Current = settings;
            public AiCopilotSettings Current { get; }
            public AiCopilotSettings Load() => Current;
            public void Save() { }
            public void Update(Action<AiCopilotSettings> updateAction) => updateAction(Current);
        }

        private sealed class InMemoryCredentialStore : IAiCredentialStore
        {
            private readonly Dictionary<string, string> _values = new(StringComparer.OrdinalIgnoreCase);
            public bool Contains(string targetName) => _values.ContainsKey(targetName);
            public string? Read(string targetName) => _values.TryGetValue(targetName, out var value) ? value : null;
            public void Save(string targetName, string secret) => _values[targetName] = secret;
            public void Delete(string targetName) => _values.Remove(targetName);
        }

        private sealed class StubHandler : HttpMessageHandler
        {
            private readonly Func<HttpRequestMessage, HttpResponseMessage> _handler;
            public StubHandler(Func<HttpRequestMessage, HttpResponseMessage> handler) => _handler = handler;
            public int CallCount { get; private set; }

            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                CallCount++;
                return Task.FromResult(_handler(request));
            }
        }
    }
}
