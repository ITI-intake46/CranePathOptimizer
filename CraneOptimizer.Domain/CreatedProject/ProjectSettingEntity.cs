namespace CraneOptimizer.Domain.CreatedProject
{
    /// <summary>
    /// Stores flexible project settings as key/value JSON.
    /// UI settings, import options, render options, and optimizer settings.
    /// </summary>
    public sealed class ProjectSettingEntity
    {
        public int Id { get; set; }

        public int ProjectInfoId { get; set; }

        public string Key { get; set; } = string.Empty;

        public string? ValueJson { get; set; }

        public ProjectInfoEntity? ProjectInfo { get; set; }
    }
}
