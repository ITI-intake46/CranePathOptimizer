using CraneOptimizer.AI.Configuration;
using CraneOptimizer.AI.DTOs;
using CraneOptimizer.AI.Runtime;

namespace CraneOptimizer.Application.Tests.AI;

public sealed class AiEndpointSecurityAcceptanceTests
{
    [Theory]
    [InlineData("http://localhost:11434/")]
    [InlineData("https://localhost:11434/")]
    [InlineData("http://127.0.0.1:11434/")]
    [InlineData("http://[::1]:11434/")]
    public void Classifier_TreatsOnlyLoopbackDestinationsAsLocal(string endpoint)
    {
        Assert.True(AiEndpointSecurityClassifier.IsLocalEndpoint(endpoint));
    }

    [Theory]
    [InlineData("http://10.0.0.15:11434/")]
    [InlineData("https://ollama.example.test/")]
    [InlineData("https://192.168.1.7:11434/")]
    public void Classifier_DoesNotTrustRemoteOllamaByProviderName(string endpoint)
    {
        Assert.False(AiEndpointSecurityClassifier.IsLocalEndpoint(endpoint));
    }

    [Fact]
    public void RemoteOllamaOverHttp_IsDeniedEvenWhenStoredProfileSaysLocal()
    {
        var settings = SettingsWithRemoteOllama("http://10.0.0.15:11434/", storedIsLocal: true);
        var policy = new DefaultAiRequestPolicy(new StubSettingsService(settings));

        var decision = policy.Evaluate(ProjectContextRequest());

        Assert.False(decision.IsAllowed);
        Assert.Contains("HTTPS", decision.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void RemoteOllamaOverHttps_RequiresExplicitCloudConsent()
    {
        var settings = SettingsWithRemoteOllama("https://ollama.example.test/", storedIsLocal: true);
        var policy = new DefaultAiRequestPolicy(new StubSettingsService(settings));

        var denied = policy.Evaluate(ProjectContextRequest());
        Assert.False(denied.IsAllowed);
        Assert.Contains("consent", denied.ErrorMessage, StringComparison.OrdinalIgnoreCase);

        settings.CloudConsentAcceptedProviderIds.Add(AiProviderProfileSettings.OllamaId);
        var allowed = policy.Evaluate(ProjectContextRequest());
        Assert.True(allowed.IsAllowed, allowed.ErrorMessage);
    }

    private static AiCopilotSettings SettingsWithRemoteOllama(string endpoint, bool storedIsLocal)
    {
        var settings = new AiCopilotSettings
        {
            DefaultProviderId = AiProviderProfileSettings.OllamaId,
            ProviderProfiles = new List<AiProviderProfileSettings>()
        };
        settings.ProviderProfiles.Add(new AiProviderProfileSettings
        {
            Id = AiProviderProfileSettings.OllamaId,
            DisplayName = "Ollama",
            ProviderKind = "Ollama",
            BaseUrl = endpoint,
            IsLocal = storedIsLocal,
            DefaultModel = "test"
        });
        return settings;
    }

    private static AiChatRequestDto ProjectContextRequest() => new()
    {
        ProviderId = AiProviderProfileSettings.OllamaId,
        Model = "test",
        UserPrompt = "Explain this project.",
        ContextJson = "{\"project\":\"acceptance\"}",
        Metadata = new Dictionary<string, string> { ["ContainsProjectContext"] = "true" }
    };

    private sealed class StubSettingsService : IAiCopilotSettingsService
    {
        public StubSettingsService(AiCopilotSettings settings) => Current = settings;
        public AiCopilotSettings Current { get; }
        public AiCopilotSettings Load() => Current;
        public void Save() { }
        public void Update(Action<AiCopilotSettings> updateAction) => updateAction(Current);
    }
}
