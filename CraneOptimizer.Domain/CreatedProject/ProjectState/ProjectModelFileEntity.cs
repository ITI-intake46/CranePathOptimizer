namespace CraneOptimizer.Domain.CreatedProject.ProjectState
{
    public sealed class ProjectModelFileEntity
    {
        public int Id { get; set; }

        public int ProjectInfoId { get; set; }

        public string FileType { get; set; } = string.Empty;

        public string Role { get; set; } = string.Empty;

        public string FileName { get; set; } = string.Empty;

        public string RelativePath { get; set; } = string.Empty;

        public string? OriginalFullPath { get; set; }

        public long FileSizeBytes { get; set; }

        public DateTime? LastWriteUtc { get; set; }

        public string? TransformJson { get; set; }

        public string? MetadataJson { get; set; }

        public bool IsActive { get; set; } = true;

        public DateTime ImportedAtUtc { get; set; } = DateTime.UtcNow;

        public ProjectInfoEntity? ProjectInfo { get; set; }
    }
}
