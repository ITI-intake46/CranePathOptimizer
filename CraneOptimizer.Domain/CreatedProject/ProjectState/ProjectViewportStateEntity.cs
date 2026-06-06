namespace CraneOptimizer.Domain.CreatedProject.ProjectState
{
    public sealed class ProjectViewportStateEntity
    {
        public int Id { get; set; }

        public int ProjectInfoId { get; set; }

        public string StateKey { get; set; } = "Main";

        public double? OriginX { get; set; }

        public double? OriginY { get; set; }

        public double? OriginZ { get; set; }

        public bool ShowCraneRadius { get; set; }

        public string? RenderQualityLabel { get; set; }

        public string? CameraJson { get; set; }

        public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;

        public ProjectInfoEntity? ProjectInfo { get; set; }
    }
}
