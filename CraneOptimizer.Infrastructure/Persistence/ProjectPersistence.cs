using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows.Media.Media3D;
using WPF_IFC_test1.ZoneModule.Models;

namespace WPF_IFC_test1.ZoneModule.Persistence
{
    /// <summary>
    /// Saves and loads the entire zone project to/from a JSON .craneproject file.
    /// Includes: site boundary, all zones, DXF path, alignment transform,
    /// and dimension annotations.
    /// On reload, DXF re-aligns automatically using stored transform.
    /// All vertices are in IFC world coordinates.
    /// </summary>
    public static class ProjectPersistence
    {
        public static void Save(string filePath, ZoneProjectData data)
        {
            var options = new JsonSerializerOptions { WriteIndented = true };
            var json = JsonSerializer.Serialize(data, options);
            File.WriteAllText(filePath, json);
        }

        public static ZoneProjectData Load(string filePath)
        {
            var json = File.ReadAllText(filePath);
            return JsonSerializer.Deserialize<ZoneProjectData>(json)
                ?? new ZoneProjectData();
        }

        public static ZoneProjectData FromManager(ZoneManager manager, string dxfFilePath = null, double[][]? alignmentMatrix = null)
        {
            return new ZoneProjectData
            {
                SavedAt = DateTime.UtcNow,
                DxfFilePath = dxfFilePath,
                AlignmentMatrix = alignmentMatrix,
                SiteBoundary = manager.SiteBoundary != null
                    ? ZoneDto.FromZone(manager.SiteBoundary) : null,
                Zones = manager.Zones.Select(ZoneDto.FromZone).ToList(),
                Annotations = manager.Annotations.Select(AnnotationDto.From).ToList()
            };
        }

        public static void RestoreToManager(ZoneProjectData data, ZoneManager manager)
        {
            manager.RemoveAll();

            if (data.SiteBoundary != null)
                manager.SetSiteBoundary(data.SiteBoundary.ToVertices());

            foreach (var dto in data.Zones)
            {
                var zone = dto.ToZone();
                try { manager.AddZone(zone); }
                catch (Exception ex)
                { System.Diagnostics.Debug.WriteLine($"Skipping zone {zone.Name}: {ex.Message}"); }
            }

            foreach (var a in data.Annotations)
                manager.Annotations.Add(a.ToAnnotation());
        }
    }

    // ── DTOs for JSON serialization ───────────────────────────────────────────

    public class ZoneProjectData
    {
        public DateTime SavedAt { get; set; }
        public string DxfFilePath { get; set; }
        public double[][] AlignmentMatrix { get; set; }
        public ZoneDto SiteBoundary { get; set; }
        public List<ZoneDto> Zones { get; set; } = new();
        public List<AnnotationDto> Annotations { get; set; } = new();
    }

    public class ZoneDto
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string ZoneType { get; set; }
        public double MinHeight { get; set; }
        public double MaxHeight { get; set; }
        public bool IsVisible { get; set; } = true;
        public string Notes { get; set; }
        public List<double[]> Vertices { get; set; } = new();

        public static ZoneDto FromZone(CraneZone z) => new()
        {
            Id = z.Id,
            Name = z.Name,
            ZoneType = z.ZoneType.ToString(),
            MinHeight = z.MinHeight,
            MaxHeight = z.MaxHeight,
            IsVisible = z.IsVisible,
            Notes = z.Notes,
            Vertices = z.Vertices.Select(v => new[] { v.X, v.Y, v.Z }).ToList()
        };

        public CraneZone ToZone() => new()
        {
            Id = Id,
            Name = Name,
            ZoneType = Enum.Parse<ZoneType>(ZoneType),
            MinHeight = MinHeight,
            MaxHeight = MaxHeight,
            IsVisible = IsVisible,
            Notes = Notes,
            Vertices = ToVertices()
        };

        public List<Point3D> ToVertices() =>
            // BUG FIX: Original accessed v[0] without checking v.Length > 0 first.
            // A corrupted or empty vertex array in saved JSON would throw
            // IndexOutOfRangeException and crash the entire project load.
            // Guard each coordinate access against the array length.
            Vertices
                .Where(v => v != null && v.Length > 0)
                .Select(v => new Point3D(
                    v[0],
                    v.Length > 1 ? v[1] : 0,
                    v.Length > 2 ? v[2] : 0))
                .ToList();
    }

    public class AnnotationDto
    {
        public string Id { get; set; }
        public string Type { get; set; }
        public double[] Start { get; set; }
        public double[] End { get; set; }
        public double Offset { get; set; }
        public bool IsVisible { get; set; } = true;

        public static AnnotationDto From(DimensionAnnotation a) => new()
        {
            Id = a.Id,
            Type = a.Type.ToString(),
            Start = new[] { a.StartPoint.X, a.StartPoint.Y, a.StartPoint.Z },
            End = new[] { a.EndPoint.X, a.EndPoint.Y, a.EndPoint.Z },
            Offset = a.OffsetDistance,
            IsVisible = a.IsVisible
        };

        public DimensionAnnotation ToAnnotation() => new()
        {
            Id = Id,
            Type = Enum.Parse<DimensionType>(Type),
            StartPoint = new Point3D(Start[0], Start[1], Start[2]),
            EndPoint = new Point3D(End[0], End[1], End[2]),
            OffsetDistance = Offset,
            IsVisible = IsVisible
        };
    }
}