using System.Collections.Generic;

namespace CraneOptimizer.AI.Configuration
{
    /// <summary>
    /// User-facing AI Assist and AI Studio preferences persisted outside the project database.
    /// Secrets are never persisted here; provider API keys are stored through IAiCredentialStore.
    /// </summary>
    public sealed class AiCopilotSettings
    {
        public string DefaultProviderId { get; set; } = AiProviderProfileSettings.OllamaId;
        public string DefaultModelName { get; set; } = "llama3.2:latest";
        public string OllamaEndpoint { get; set; } = "http://localhost:11434/";
        public List<AiProviderProfileSettings> ProviderProfiles { get; set; } = AiProviderProfileSettings.CreateDefaults();
        public HashSet<string> CloudConsentAcceptedProviderIds { get; set; } = new(StringComparer.OrdinalIgnoreCase);

        public bool IsCompanionVisible { get; set; } = true;
        public bool IsAiAssistDockVisible { get; set; }
        public bool IsSectionRailExpanded { get; set; } = true;
        public string DockPreference { get; set; } = "Right";
        public bool AutoCheckOllamaOnStartup { get; set; }
        public bool ShowContextInspectorByDefault { get; set; }
        public int MaxHistoryItems { get; set; } = 25;
        public int BenchmarkModelLimit { get; set; } = 3;
        public int RequestTimeoutSeconds { get; set; } = 120;
        public double Temperature { get; set; } = 0.2;
        public int MaxSystemPromptCharacters { get; set; } = 40_000;
        public int MaxUserPromptCharacters { get; set; } = 20_000;
        public int MaxContextCharacters { get; set; } = 500_000;
        public int TelemetryBufferCapacity { get; set; } = 500;

        public bool UseStreamingResponses { get; set; } = true;
        public bool SaveConversationHistory { get; set; } = true;
        public bool EnableConfirmableNavigationActions { get; set; } = true;
        public bool IncludeXamlDerivedSoftwareInventory { get; set; } = true;
        public bool ExportReportAppendixWithDisclaimer { get; set; } = true;

        public List<AiConversationRecord> ConversationHistory { get; set; } = new();
        public Dictionary<string, List<AiConversationRecord>> ProjectConversationHistory { get; set; } = new();
        public Dictionary<string, AiDockPreferenceRecord> ProjectDockPreferences { get; set; } = new();
        public Dictionary<string, AiModelCapabilityRecord> ModelCapabilities { get; set; } = new();
    }

    public sealed class AiProviderProfileSettings
    {
        public const string OllamaId = "ollama";
        public const string OpenRouterId = "openrouter";
        public const string OpenAiId = "openai";
        public const string AzureOpenAiId = "azure-openai";
        public const string CustomOpenAiId = "custom-openai-compatible";

        public string Id { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string ProviderKind { get; set; } = "OpenAICompatible";
        public bool Enabled { get; set; } = true;
        public bool IsLocal { get; set; }
        public string BaseUrl { get; set; } = string.Empty;
        public string ChatCompletionsPath { get; set; } = "chat/completions";
        public string ModelsPath { get; set; } = "models";
        public bool SupportsModelDiscovery { get; set; } = true;
        public bool SupportsStreaming { get; set; } = true;
        public bool SendTemperature { get; set; } = true;
        public string AuthenticationMode { get; set; } = "Bearer";
        public string ApiKeyHeaderName { get; set; } = "Authorization";
        public string CredentialTarget { get; set; } = string.Empty;
        public string DefaultModel { get; set; } = string.Empty;
        public Dictionary<string, string> AdditionalHeaders { get; set; } = new(StringComparer.OrdinalIgnoreCase);

        public static List<AiProviderProfileSettings> CreateDefaults() => new()
        {
            new()
            {
                Id = OllamaId,
                DisplayName = "Local Ollama",
                ProviderKind = "Ollama",
                IsLocal = true,
                BaseUrl = "http://localhost:11434/",
                ChatCompletionsPath = "api/chat",
                ModelsPath = "api/tags",
                AuthenticationMode = "None",
                CredentialTarget = string.Empty,
                DefaultModel = "llama3.2:latest"
            },
            new()
            {
                Id = OpenRouterId,
                DisplayName = "OpenRouter",
                BaseUrl = "https://openrouter.ai/api/v1/",
                ChatCompletionsPath = "chat/completions",
                ModelsPath = "models",
                AuthenticationMode = "Bearer",
                ApiKeyHeaderName = "Authorization",
                CredentialTarget = "CraneOptimizer/AI/OpenRouter",
                DefaultModel = string.Empty,
                AdditionalHeaders = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["X-OpenRouter-Title"] = "Crane Path Optimizer"
                }
            },
            new()
            {
                Id = OpenAiId,
                DisplayName = "OpenAI",
                BaseUrl = "https://api.openai.com/v1/",
                ChatCompletionsPath = "chat/completions",
                ModelsPath = "models",
                AuthenticationMode = "Bearer",
                ApiKeyHeaderName = "Authorization",
                CredentialTarget = "CraneOptimizer/AI/OpenAI",
                DefaultModel = string.Empty
            },
            new()
            {
                Id = AzureOpenAiId,
                DisplayName = "Azure OpenAI",
                BaseUrl = string.Empty,
                ChatCompletionsPath = "chat/completions",
                ModelsPath = string.Empty,
                SupportsModelDiscovery = false,
                AuthenticationMode = "ApiKeyHeader",
                ApiKeyHeaderName = "api-key",
                CredentialTarget = "CraneOptimizer/AI/AzureOpenAI",
                DefaultModel = string.Empty
            },
            new()
            {
                Id = CustomOpenAiId,
                DisplayName = "Custom OpenAI-compatible",
                BaseUrl = string.Empty,
                ChatCompletionsPath = "chat/completions",
                ModelsPath = "models",
                AuthenticationMode = "Bearer",
                ApiKeyHeaderName = "Authorization",
                CredentialTarget = "CraneOptimizer/AI/CustomOpenAICompatible",
                DefaultModel = string.Empty
            }
        };
    }

    public sealed class AiConversationRecord
    {
        public string Id { get; set; } = string.Empty;
        public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.Now;
        public string ProjectKey { get; set; } = "global";
        public string ProjectDisplayName { get; set; } = "Global";
        public string ProviderId { get; set; } = string.Empty;
        public string Model { get; set; } = string.Empty;
        public string Question { get; set; } = string.Empty;
        public string Answer { get; set; } = string.Empty;
        public string Source { get; set; } = "AI";
    }

    public sealed class AiDockPreferenceRecord
    {
        public string ProjectKey { get; set; } = "global";
        public string ProjectDisplayName { get; set; } = "Global";
        public bool LastDockVisible { get; set; }
        public string DockPreference { get; set; } = "Right";
        public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.Now;
    }

    public sealed class AiModelCapabilityRecord
    {
        public string Model { get; set; } = string.Empty;
        public bool LastBenchmarkSucceeded { get; set; }
        public long LastElapsedMilliseconds { get; set; }
        public int LastResponseCharacters { get; set; }
        public string Recommendation { get; set; } = string.Empty;
        public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.Now;
    }
}
