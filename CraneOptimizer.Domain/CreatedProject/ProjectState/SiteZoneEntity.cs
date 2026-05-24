namespace CraneOptimizer.Domain.CreatedProject.ProjectState
{
    public sealed class SiteZoneEntity
    {
        public int Id { get; set; }

        public int ProjectInfoId { get; set; }

        public string ZoneGuid { get; set; } = Guid.NewGuid().ToString("N");

        public string Name { get; set; } = string.Empty;

        public string ZoneType { get; set; } = string.Empty;

        public double Area { get; set; }

        public int SortOrder { get; set; }

        public string VerticesJson { get; set; } = string.Empty;

        public ProjectInfoEntity? ProjectInfo { get; set; }
    }
}
