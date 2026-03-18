using System;

namespace WPF_IFC_test1.Database.Entities
{
    /// <summary>
    /// Tracks every IFC file loaded into a project.
    /// Replaces the in-memory list in MultiModelLoader.
    /// One project can have multiple IFC files (building + crane models).
    /// </summary>
    public class IfcFileEntity
    {
        public int      Id          { get; set; }
        public int      ProjectId   { get; set; }

        public string   FilePath    { get; set; }   // Full path on disk
        public string   FileName    { get; set; }   // e.g. "Building_A.ifc"
        public string   Role        { get; set; }   // "Building" | "Crane" | "Site" | "Other"
        public long     FileSizeBytes { get; set; }
        public bool     IsActive    { get; set; } = true;   // false = unloaded but remembered
        public DateTime LoadedAt    { get; set; } = DateTime.UtcNow;

        // IFC metadata captured after parsing
        public string   BuildingName  { get; set; }
        public int?     NumberOfStoreys { get; set; }
        public double?  BuildingArea  { get; set; }   // m²
        public double?  BuildingHeight { get; set; }  // m
        public int?     ElementCount  { get; set; }

        // Navigation
        public ProjectEntity Project { get; set; }
    }
}
