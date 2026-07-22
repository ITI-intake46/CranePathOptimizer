using System.Collections.Generic;

namespace CraneOptimizer.AI.Configuration
{
    /// <summary>
    /// User-facing AI Copilot preferences persisted outside the project database.
    /// These settings intentionally affect only AI UX behavior; they do not alter
    /// crane calculations, GA configuration, IFC data, zones, or report outputs.
    /// </summary>
    public sealed class AiCopilotSettings
    {
        public string DefaultProviderId { get; set; } = "ollama";
        public string DefaultModelName { get; set; } = "llama3.2:latest";
        public string OllamaEndpoint { get; set; } = "http://localhost:11434/";
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

    public sealed class AiConversationRecord
    {
        public string Id { get; set; } = string.Empty;
        public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.Now;
        public string ProjectKey { get; set; } = "global";
        public string ProjectDisplayName { get; set; } = "Global";
        public string Model { get; set; } = string.Empty;
        public string Question { get; set; } = string.Empty;
        public string Answer { get; set; } = string.Empty;
        public string Source { get; set; } = "Ollama";
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
