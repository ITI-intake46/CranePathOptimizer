using CraneOptimizer.UI.Features.F15_ObjectSnap.Models;
using CraneOptimizer.UI.Features.F15_ObjectSnap.DTOs;
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
                    if (loaded != null)
                    {
                        NormalizeLoadedSettings(loaded);
                        return loaded;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[OsnapSettingsService] Load failed: {ex.Message}");
            }

            return new OsnapSettings(); // default values
        }

        private static void NormalizeLoadedSettings(OsnapSettings settings)
        {
            // Older settings stored grid spacing as "1.0" while the scene is in
            // millimetres, causing a visible grid snap candidate almost everywhere.
            if (settings.GridSpacing < 10.0)
                settings.GridSpacing = 1000.0;

            // Nearest snaps anywhere along an edge. Keep it off so object snap
            // stays focused on explicit points such as END, MID, CEN, and QUA.
            settings.SnapNearest = false;
            settings.SnapPerpendicular = false;
            settings.SnapGrid = false;
            settings.SnapMidpoint = true;
            settings.SnapIntersection = true;
            settings.SnapQuadrant = true;
        }

        /// <summary>Persists the current settings to disk.</summary>
        public void Save(OsnapSettings settings)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath)!);
                var json = JsonSerializer.Serialize(ToDto(settings), _jsonOptions);
                File.WriteAllText(SettingsPath, json);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[OsnapSettingsService] Save failed: {ex.Message}");
            }
        }

        private static OsnapSettings ToModel(OsnapSettingsDto dto)
        {
            return new OsnapSettings
            {
                MasterEnabled = dto.MasterEnabled,
                SnapEndpoint = dto.SnapEndpoint,
                SnapMidpoint = dto.SnapMidpoint,
                SnapIntersection = dto.SnapIntersection,
                SnapPerpendicular = dto.SnapPerpendicular,
                SnapNearest = dto.SnapNearest,
                SnapGrid = dto.SnapGrid,
                SnapCenter = dto.SnapCenter,
                SnapQuadrant = dto.SnapQuadrant,
                SnapRadiusPx = dto.SnapRadiusPx,
                GridSpacing = dto.GridSpacing,
                MaxEdgesForIntersection = dto.MaxEdgesForIntersection,
                SnapToDxf = dto.SnapToDxf,
                SnapToIfc = dto.SnapToIfc
            };
        }

        private static OsnapSettingsDto ToDto(OsnapSettings settings)
        {
            return new OsnapSettingsDto
            {
                MasterEnabled = settings.MasterEnabled,
                SnapEndpoint = settings.SnapEndpoint,
                SnapMidpoint = settings.SnapMidpoint,
                SnapIntersection = settings.SnapIntersection,
                SnapPerpendicular = settings.SnapPerpendicular,
                SnapNearest = settings.SnapNearest,
                SnapGrid = settings.SnapGrid,
                SnapCenter = settings.SnapCenter,
                SnapQuadrant = settings.SnapQuadrant,
                SnapRadiusPx = settings.SnapRadiusPx,
                GridSpacing = settings.GridSpacing,
                MaxEdgesForIntersection = settings.MaxEdgesForIntersection,
                SnapToDxf = settings.SnapToDxf,
                SnapToIfc = settings.SnapToIfc
            };
        }
    }
}
