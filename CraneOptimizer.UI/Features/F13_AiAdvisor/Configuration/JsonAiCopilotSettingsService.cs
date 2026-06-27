using System.IO;
using System.Text.Json;

namespace CraneOptimizer.UI.Features.F13_AiAdvisor.Configuration
{
    /// <summary>
    /// Lightweight local settings store for AI Copilot preferences. Persistence is
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
                Normalize(_current);
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
            if (string.IsNullOrWhiteSpace(settings.DefaultModelName))
                settings.DefaultModelName = "llama3.2:latest";

            if (string.IsNullOrWhiteSpace(settings.DockPreference))
                settings.DockPreference = "Right";

            settings.MaxHistoryItems = Math.Clamp(settings.MaxHistoryItems, 1, 100);
            settings.BenchmarkModelLimit = Math.Clamp(settings.BenchmarkModelLimit, 1, 8);
            settings.RequestTimeoutSeconds = Math.Clamp(settings.RequestTimeoutSeconds, 15, 600);
            settings.Temperature = Math.Clamp(settings.Temperature, 0.0, 1.5);
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
    }
}
