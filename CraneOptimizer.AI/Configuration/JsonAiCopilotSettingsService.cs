using System.IO;
using System.Text.Json;

namespace CraneOptimizer.AI.Configuration
{
    /// <summary>
    /// Lightweight local settings store for AI Assist and AI Studio preferences. Persistence is
    /// intentionally best-effort: failure to read/write preferences must never
    /// prevent the crane planner from launching or running deterministic workflows.
    /// </summary>
    public sealed class JsonAiCopilotSettingsService : IAiCopilotSettingsService
    {
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            WriteIndented = true
        };

        private readonly string _settingsPath;
        private AiCopilotSettings _current = new();

        public JsonAiCopilotSettingsService()
        {
            var root = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var directory = Path.Combine(root, "CraneOptimizer", "AiCopilot");
            _settingsPath = Path.Combine(directory, "ai-copilot-settings.json");
            Load();
        }

        public AiCopilotSettings Current => _current;

        public AiCopilotSettings Load()
        {
            try
            {
                if (!File.Exists(_settingsPath))
                {
                    _current = new AiCopilotSettings();
                    return _current;
                }

                var json = File.ReadAllText(_settingsPath);
                _current = JsonSerializer.Deserialize<AiCopilotSettings>(json, JsonOptions) ?? new AiCopilotSettings();
                var migrated = ApplyProviderMigrations(_current);
                Normalize(_current);
                if (migrated)
                    Save();
            }
            catch
            {
                _current = new AiCopilotSettings();
            }

            return _current;
        }

        public void Save()
        {
            try
            {
                var directory = Path.GetDirectoryName(_settingsPath);
                if (!string.IsNullOrWhiteSpace(directory))
                    Directory.CreateDirectory(directory);

                Normalize(_current);
                File.WriteAllText(_settingsPath, JsonSerializer.Serialize(_current, JsonOptions));
            }
            catch
            {
                // Preference persistence must never block core project workflows.
            }
        }

        public void Update(Action<AiCopilotSettings> updateAction)
        {
            if (updateAction == null)
                throw new ArgumentNullException(nameof(updateAction));

            updateAction(_current);
            Save();
        }

        private static void Normalize(AiCopilotSettings settings)
        {
            if (string.IsNullOrWhiteSpace(settings.DefaultProviderId))
                settings.DefaultProviderId = "ollama";

            if (string.IsNullOrWhiteSpace(settings.DefaultModelName))
                settings.DefaultModelName = "llama3.2:latest";

            if (!Uri.TryCreate(settings.OllamaEndpoint, UriKind.Absolute, out var ollamaEndpoint)
                || (ollamaEndpoint.Scheme != Uri.UriSchemeHttp && ollamaEndpoint.Scheme != Uri.UriSchemeHttps))
            {
                settings.OllamaEndpoint = "http://localhost:11434/";
            }
            else if (!settings.OllamaEndpoint.EndsWith("/", StringComparison.Ordinal))
            {
                settings.OllamaEndpoint += "/";
            }

            if (string.IsNullOrWhiteSpace(settings.DockPreference))
                settings.DockPreference = "Right";

            settings.MaxHistoryItems = Math.Clamp(settings.MaxHistoryItems, 1, 100);
            settings.BenchmarkModelLimit = Math.Clamp(settings.BenchmarkModelLimit, 1, 8);
            settings.RequestTimeoutSeconds = Math.Clamp(settings.RequestTimeoutSeconds, 15, 600);
            settings.Temperature = Math.Clamp(settings.Temperature, 0.0, 1.5);
            settings.MaxSystemPromptCharacters = Math.Clamp(settings.MaxSystemPromptCharacters, 1_000, 200_000);
            settings.MaxUserPromptCharacters = Math.Clamp(settings.MaxUserPromptCharacters, 1_000, 200_000);
            settings.MaxContextCharacters = Math.Clamp(settings.MaxContextCharacters, 10_000, 2_000_000);
            settings.TelemetryBufferCapacity = Math.Clamp(settings.TelemetryBufferCapacity, 10, 10_000);
            settings.ProviderProfiles ??= AiProviderProfileSettings.CreateDefaults();
            settings.CloudConsentAcceptedProviderIds = new HashSet<string>(
                settings.CloudConsentAcceptedProviderIds?.Where(id => !string.IsNullOrWhiteSpace(id))
                    .Select(id => id.Trim())
                ?? Array.Empty<string>(),
                StringComparer.OrdinalIgnoreCase);
            EnsureProviderProfiles(settings);

            settings.ConversationHistory ??= new List<AiConversationRecord>();
            settings.ProjectConversationHistory ??= new Dictionary<string, List<AiConversationRecord>>();
            settings.ProjectDockPreferences ??= new Dictionary<string, AiDockPreferenceRecord>();
            settings.ModelCapabilities ??= new Dictionary<string, AiModelCapabilityRecord>();

            foreach (var entry in settings.ProjectConversationHistory.ToArray())
            {
                if (string.IsNullOrWhiteSpace(entry.Key) || entry.Value == null)
                    settings.ProjectConversationHistory.Remove(entry.Key);
            }

            foreach (var entry in settings.ProjectDockPreferences.ToArray())
            {
                if (string.IsNullOrWhiteSpace(entry.Key) || entry.Value == null)
                    settings.ProjectDockPreferences.Remove(entry.Key);
            }

            foreach (var entry in settings.ModelCapabilities.ToArray())
            {
                if (string.IsNullOrWhiteSpace(entry.Key) || entry.Value == null)
                    settings.ModelCapabilities.Remove(entry.Key);
            }
        }
        private static bool ApplyProviderMigrations(AiCopilotSettings settings)
        {
            var migrated = false;
            foreach (var profile in settings.ProviderProfiles ?? new List<AiProviderProfileSettings>())
            {
                if (profile != null)
                    migrated |= AiProviderProfileMigrations.Apply(profile);
            }
            return migrated;
        }

        private static void EnsureProviderProfiles(AiCopilotSettings settings)
        {
            var defaults = AiProviderProfileSettings.CreateDefaults();
            foreach (var defaultProfile in defaults)
            {
                var profile = settings.ProviderProfiles.FirstOrDefault(item =>
                    string.Equals(item?.Id, defaultProfile.Id, StringComparison.OrdinalIgnoreCase));
                if (profile == null)
                {
                    settings.ProviderProfiles.Add(defaultProfile);
                    continue;
                }

                NormalizeProfile(profile, defaultProfile);
            }

            var seenProviderIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var profile in settings.ProviderProfiles.ToArray())
            {
                if (profile == null || string.IsNullOrWhiteSpace(profile.Id))
                {
                    settings.ProviderProfiles.Remove(profile!);
                    continue;
                }

                NormalizeProfile(profile, defaults.FirstOrDefault(item =>
                    string.Equals(item.Id, profile.Id, StringComparison.OrdinalIgnoreCase)));
                if (!seenProviderIds.Add(profile.Id))
                    settings.ProviderProfiles.Remove(profile);
            }

            var ollama = settings.ProviderProfiles.First(profile =>
                string.Equals(profile.Id, AiProviderProfileSettings.OllamaId, StringComparison.OrdinalIgnoreCase));
            ollama.BaseUrl = settings.OllamaEndpoint;
            if (string.IsNullOrWhiteSpace(ollama.DefaultModel))
                ollama.DefaultModel = settings.DefaultModelName;

            if (!settings.ProviderProfiles.Any(profile =>
                    string.Equals(profile.Id, settings.DefaultProviderId, StringComparison.OrdinalIgnoreCase)
                    && profile.Enabled))
            {
                settings.DefaultProviderId = AiProviderProfileSettings.OllamaId;
            }
        }

        private static void NormalizeProfile(AiProviderProfileSettings profile, AiProviderProfileSettings? fallback)
        {
            profile.Id = profile.Id?.Trim() ?? string.Empty;
            profile.DisplayName = string.IsNullOrWhiteSpace(profile.DisplayName)
                ? fallback?.DisplayName ?? profile.Id
                : profile.DisplayName.Trim();
            profile.ProviderKind = fallback?.ProviderKind
                ?? (string.IsNullOrWhiteSpace(profile.ProviderKind) ? "OpenAICompatible" : profile.ProviderKind.Trim());
            if (fallback != null)
                profile.IsLocal = fallback.IsLocal;
            profile.ChatCompletionsPath = NormalizeRelativePath(profile.ChatCompletionsPath, fallback?.ChatCompletionsPath ?? "chat/completions");
            profile.ModelsPath = string.IsNullOrWhiteSpace(profile.ModelsPath)
                ? fallback?.ModelsPath ?? string.Empty
                : NormalizeRelativePath(profile.ModelsPath, string.Empty);
            profile.AuthenticationMode = NormalizeAuthenticationMode(
                string.IsNullOrWhiteSpace(profile.AuthenticationMode)
                    ? fallback?.AuthenticationMode ?? "Bearer"
                    : profile.AuthenticationMode.Trim(),
                fallback?.AuthenticationMode ?? "Bearer");
            profile.ApiKeyHeaderName = string.IsNullOrWhiteSpace(profile.ApiKeyHeaderName)
                ? fallback?.ApiKeyHeaderName ?? "Authorization"
                : profile.ApiKeyHeaderName.Trim();
            profile.CredentialTarget = string.IsNullOrWhiteSpace(profile.CredentialTarget)
                ? fallback?.CredentialTarget ?? $"CraneOptimizer/AI/{profile.Id}"
                : profile.CredentialTarget.Trim();
            profile.DefaultModel = profile.DefaultModel?.Trim() ?? fallback?.DefaultModel ?? string.Empty;
            AiProviderProfileMigrations.Apply(profile);
            var normalizedHeaders = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var header in profile.AdditionalHeaders ?? new Dictionary<string, string>())
            {
                if (!string.IsNullOrWhiteSpace(header.Key)
                    && !string.IsNullOrWhiteSpace(header.Value)
                    && !IsSensitiveHeaderName(header.Key))
                {
                    normalizedHeaders[header.Key.Trim()] = header.Value.Trim();
                }
            }
            profile.AdditionalHeaders = normalizedHeaders;

            if (!string.IsNullOrWhiteSpace(profile.BaseUrl))
            {
                if (!Uri.TryCreate(profile.BaseUrl, UriKind.Absolute, out var uri)
                    || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
                {
                    profile.BaseUrl = fallback?.BaseUrl ?? string.Empty;
                }
                else if (!profile.BaseUrl.EndsWith("/", StringComparison.Ordinal))
                {
                    profile.BaseUrl += "/";
                }
            }
        }



        private static string NormalizeAuthenticationMode(string? value, string fallback)
        {
            if (string.Equals(value, "Bearer", StringComparison.OrdinalIgnoreCase))
                return "Bearer";
            if (string.Equals(value, "ApiKeyHeader", StringComparison.OrdinalIgnoreCase))
                return "ApiKeyHeader";
            if (string.Equals(value, "None", StringComparison.OrdinalIgnoreCase))
                return "None";
            return fallback;
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

        private static string NormalizeRelativePath(string? value, string fallback)
        {
            var normalized = string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
            return normalized.TrimStart('/');
        }

    }
}
