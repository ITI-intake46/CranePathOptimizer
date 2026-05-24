namespace CraneOptimizer.UI.Features.F10_ProjectManagement.Models
{
    public sealed class ProjectSession
    {
        public Guid ProjectId { get; init; }

        public string ProjectName { get; init; } = string.Empty;

        public string ProjectFolderPath { get; init; } = string.Empty;

        public string ProjectDatabasePath { get; init; } = string.Empty;

        public int ProjectInfoId { get; init; }

        public string ProjectGuid { get; init; } = string.Empty;

        public DateTime LastOpenedAtUtc { get; init; }

        public ProjectCardModel ProjectCard { get; init; } = new();
    }
}
