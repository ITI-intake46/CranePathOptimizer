namespace CraneOptimizer.Domain.CreatedProject.IfcFiles
{
    /// <summary>
    /// Lightweight IFC element cache used by the application after import.
    /// This does not store the full IFC object.
    /// It only stores the data needed for crane planning, filtering, linking, and reporting.
    /// </summary>
    public sealed class ProjectIfcElementEntity
    {
        public int Id { get; set; }

        public int ProjectInfoId { get; set; }

        public int IfcFileId { get; set; }

        // IFC identity
        public string GlobalId { get; set; } = string.Empty;

        public int? EntityLabel { get; set; }

        public string? Name { get; set; }

        public string? DisplayName { get; set; }

        public string? Mark { get; set; }

        /// <summary>
        /// Example: IfcColumn, IfcBeam, IfcSlab, IfcWall.
        /// </summary>
        public string? ElementType { get; set; }

        public string? StoreyName { get; set; }

        public string? Material { get; set; }

        // Dimensions stored in meters.
        public double? LengthM { get; set; }

        public double? WidthM { get; set; }

        public double? HeightM { get; set; }

        public double? VolumeM3 { get; set; }

        // Mass stored in tons.
        public double? WeightTons { get; set; }

        // Centre of gravity stored in meters, world coordinates.
        public double? COG_X { get; set; }

        public double? COG_Y { get; set; }

        public double? COG_Z { get; set; }

        // Bounding box stored in meters, world coordinates.
        public double? BBoxMinX { get; set; }

        public double? BBoxMinY { get; set; }

        public double? BBoxMinZ { get; set; }

        public double? BBoxMaxX { get; set; }

        public double? BBoxMaxY { get; set; }

        public double? BBoxMaxZ { get; set; }

        /// <summary>
        /// True if the element is relevant for crane lifting.
        /// False for ignored/non-liftable elements.
        /// </summary>
        public bool IsLiftable { get; set; } = true;

        // Phase 2A/0 Normalization fields
        public string? RawIfcType { get; set; }
        public string? ResolvedCategory { get; set; }
        public string? Role { get; set; }
        public string? ParentGlobalId { get; set; }
        public string? ParentGlobalIdSource { get; set; }
        public string? ChildGlobalIdsJson { get; set; }
        public string? VisibleGeometryOwnerGlobalIdsJson { get; set; }
        public string? HiddenInternalGeometryOwnerGlobalIdsJson { get; set; }
        public bool IncludeInRender { get; set; } = true;
        public bool IncludeInElementTree { get; set; } = true;
        public bool IncludeInGa { get; set; } = true;
        public string? WeightSource { get; set; }
        public string? WeightTrustLevel { get; set; }
        public string? ExtractionWarningsJson { get; set; }
        public string? NormalizationDecisionReason { get; set; }

        // Canonical Assembly Persistence (Phase 2)
        public string? AssemblyMark { get; set; }
        public string? AssemblyMarkSource { get; set; }
        public string? AssemblyMarkConfidence { get; set; }
        public bool AssemblyMarkIsExplicit { get; set; }
        public string? SourceSystem { get; set; }
        public string? ExporterProfile { get; set; }
        public string? ValidationStatus { get; set; }
        public string? ValidationWarningsJson { get; set; }

        public DateTime CachedAtUtc { get; set; } = DateTime.UtcNow;

        public ProjectInfoEntity? ProjectInfo { get; set; }

        public ProjectIfcFileEntity? IfcFile { get; set; }
    }
}
