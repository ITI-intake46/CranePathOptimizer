using CraneOptimizer.UI.Features.F15_ObjectSnap.Models;
using System;
using System.IO;
using System.Text.Json;

namespace CraneOptimizer.UI.Features.F15_ObjectSnap.Services
{
    /// <summary>
    /// Loads and saves <see cref="OsnapSettings"/> from/to a JSON file in the app's data directory.
    /// No database table is needed — OSnap preferences are global app settings.
    /// </summary>
    public sealed class OsnapSettingsService
    {
        private static readonly string SettingsPath = Path.Combine(
            AppContext.BaseDirectory, "Data", "osnap_settings.json");

        private static readonly JsonSerializerOptions _jsonOptions = new()
        {
            WriteIndented = true
        };

        /// <summary>
        /// Loads settings from disk. Returns defaults if the file does not exist or is corrupt.
        /// </summary>
        public OsnapSettings Load()
        {
            try
            {
                if (File.Exists(SettingsPath))
                {
                    var json = File.ReadAllText(SettingsPath);
                    var loaded = JsonSerializer.Deserialize<OsnapSettings>(json, _jsonOptions);
                    if (loaded != null) return loaded;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[OsnapSettingsService] Load failed: {ex.Message}");
            }

            return new OsnapSettings(); // default values
        }

        /// <summary>Persists the current settings to disk.</summary>
        public void Save(OsnapSettings settings)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath)!);
                var json = JsonSerializer.Serialize(settings, _jsonOptions);
                File.WriteAllText(SettingsPath, json);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[OsnapSettingsService] Save failed: {ex.Message}");
            }
        }
    }
}
