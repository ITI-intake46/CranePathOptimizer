using CraneOptimizer.Domain.CreatedProject.CraneFiles;
using CraneOptimizer.Domain.CreatedProject.IfcFiles;
using CraneOptimizer.Domain.CreatedProject.OptimizationFiles;
using CraneOptimizer.Domain.CreatedProject.ProjectState;

namespace CraneOptimizer.Domain.CreatedProject
{
    /// <summary>
    /// Stores metadata for a single project-specific database.
    /// This entity belongs to project.db inside the project folder.
    /// It must not be mapped into the global CraneDbContext.
    /// The global Project entity is only a catalog entry used to locate this project folder..
    /// </summary>
    public sealed class ProjectInfoEntity
    {
        public int Id { get; set; }

        public string ProjectGuid { get; set; } = Guid.NewGuid().ToString("N");

        public string Name { get; set; } = string.Empty;

        public string? Description { get; set; }

        public string? Country { get; set; }

        public string? SiteLocation { get; set; }

        public string? CompanyName { get; set; }

        public string? ProjectImagePath { get; set; }

        public string UnitSystem { get; set; } = "Metric";

        public string LengthUnit { get; set; } = "m";

        public string AreaUnit { get; set; } = "m²";

        public string? AppVersion { get; set; }

        public int SchemaVersion { get; set; } = 1;

        public int CacheVersion { get; set; } = 1;

        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

        public DateTime LastOpenedAtUtc { get; set; } = DateTime.UtcNow;

        public List<ProjectSettingEntity> Settings { get; set; } = new();

        public List<ProjectIfcFileEntity> IfcFiles { get; set; } = new();

        public List<ProjectModelFileEntity> ModelFiles { get; set; } = new();

        public List<SiteZoneEntity> SiteZones { get; set; } = new();

        public List<CraneSetupEntity> CraneSetups { get; set; } = new();

        public List<ProjectViewportStateEntity> ViewportStates { get; set; } = new();

        public List<ProjectIfcElementEntity> IfcElements { get; set; } = new();

        public List<ProjectGeometryCacheEntity> GeometryCache { get; set; } = new();

        public List<ProjectCraneSnapshotEntity> CraneSnapshots { get; set; } = new();

        public List<ProjectOptimizationRunEntity> OptimizationRuns { get; set; } = new();
    }
}
