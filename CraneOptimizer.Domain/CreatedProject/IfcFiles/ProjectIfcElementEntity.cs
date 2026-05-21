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

        public DateTime CachedAtUtc { get; set; } = DateTime.UtcNow;

        public ProjectInfoEntity? ProjectInfo { get; set; }

        public ProjectIfcFileEntity? IfcFile { get; set; }
    }
}
