namespace CraneOptimizer.Domain.CreatedProject.IfcFiles
{
    /// <summary>
    /// Represents an IFC file imported into the project.
    /// The IFC file itself remains on disk.
    /// This table stores metadata and cache validation data only.
    /// </summary>
    public sealed class ProjectIfcFileEntity
    {
        public int Id { get; set; }

        public int ProjectInfoId { get; set; }

        public string FileName { get; set; } = string.Empty;

        /// <summary>
        /// Relative path inside the project folder.
        /// Example: Models/building.ifc
        /// </summary>
        public string RelativePath { get; set; } = string.Empty;

        /// <summary>
        /// Original user-selected path before copying/importing into the project folder.
        /// Optional, used for traceability only.
        /// </summary>
        public string? OriginalFullPath { get; set; }

        public long FileSizeBytes { get; set; }

        public DateTime? LastWriteUtc { get; set; }

        /// <summary>
        /// Used to check whether the IFC file changed and cache should be rebuilt.
        /// </summary>
        public string? FileHash { get; set; }

        /// <summary>
        /// IFC2X3, IFC4, IFC4X3, etc.
        /// </summary>
        public string? SchemaVersion { get; set; }

        public bool IsActive { get; set; } = true;

        public bool IsCacheValid { get; set; } = false;

        public DateTime ImportedAtUtc { get; set; } = DateTime.UtcNow;

        public string? BuildingName { get; set; }

        public int? NumberOfStoreys { get; set; }

        public double? BuildingAreaM2 { get; set; }

        public double? BuildingHeightM { get; set; }

        public int? ElementCount { get; set; }

        public ProjectInfoEntity? ProjectInfo { get; set; }

        public List<ProjectIfcElementEntity> Elements { get; set; } = new();

        public List<ProjectGeometryCacheEntity> GeometryCache { get; set; } = new();
    }
}
