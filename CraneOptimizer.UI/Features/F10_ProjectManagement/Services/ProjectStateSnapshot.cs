using CraneOptimizer.Domain.Common;
using CraneOptimizer.Domain.Schemas;
using CraneOptimizer.Domain.Schemas.Enums;

namespace CraneOptimizer.UI.Features.F10_ProjectManagement.Services
{
    public sealed class ProjectStateSnapshot
    {
        public ProjectIfcFileState? IfcFile { get; set; }

        public List<PrecastElement> IfcElements { get; set; } = new();

        public bool RequiresIfcCacheRefresh { get; set; }

        public ProjectModelFileState? DxfFile { get; set; }

        public List<ProjectSiteZoneState> Zones { get; set; } = new();

        public List<ProjectCraneSetupState> Cranes { get; set; } = new();

        public ProjectViewportState? Viewport { get; set; }
    }

    public sealed class ProjectIfcFileState
    {
        public string AbsolutePath { get; set; } = string.Empty;
        public string FileName { get; set; } = string.Empty;
        public string Role { get; set; } = "Primary";
        public DomainPoint3D InsertionOffset { get; set; } = new();
        public double RotationDegrees { get; set; }
        public double Scale { get; set; } = 1d;
        public string? MetadataJson { get; set; }
    }

    public sealed class ProjectModelFileState
    {
        public string FileType { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
        public string AbsolutePath { get; set; } = string.Empty;
        public string FileName { get; set; } = string.Empty;
        public double[]? TransformValues { get; set; }
        public string? MetadataJson { get; set; }
    }

    public sealed class ProjectSiteZoneState
    {
        public Guid ZoneId { get; set; }
        public string Name { get; set; } = string.Empty;
        public ZoneType Type { get; set; }
        public double Area { get; set; }
        public List<DomainPoint3D> Vertices { get; set; } = new();
    }

    public sealed class ProjectCraneSetupState
    {
        public Guid SceneCraneId { get; set; }
        public int? SourceCraneId { get; set; }
        public int? SourceCraneSpecificationId { get; set; }
        public string CraneName { get; set; } = string.Empty;
        public string CraneType { get; set; } = string.Empty;
        public DomainPoint3D Position { get; set; } = new();
        public double Rotation { get; set; }
        public double SlewAngle { get; set; }
        public DomainPoint3D PivotOffset { get; set; } = new();
        public double BoomAngle { get; set; }
        public double BoomLength { get; set; }
        public bool IsSelected { get; set; }
        public bool IsValidPlacement { get; set; } = true;
        public double? CurrentRadius { get; set; }
        public double? CurrentCapacity { get; set; }
        public string? ConfigurationJson { get; set; }
    }

    public sealed class ProjectViewportState
    {
        public DomainPoint3D? Origin { get; set; }
        public bool ShowCraneRadius { get; set; }
        public string? RenderQualityLabel { get; set; }
        public string? CameraJson { get; set; }
    }
}
