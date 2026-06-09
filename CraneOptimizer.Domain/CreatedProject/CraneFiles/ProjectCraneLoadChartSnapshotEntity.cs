namespace CraneOptimizer.Domain.CreatedProject.CraneFiles
{
    /// <summary>
    /// Snapshot of crane load chart rows used by the project.
    /// Stored inside the project to keep calculations stable over time.
    /// </summary>
    public sealed class ProjectCraneLoadChartSnapshotEntity
    {
        public int Id { get; set; }

        public int CraneSnapshotId { get; set; }

        public double RadiusM { get; set; }

        public double BoomLengthM { get; set; }

        public double CapacityTons { get; set; }

        /// <summary>
        /// Optional one-based visual column index for exact PDF load charts.
        /// </summary>
        public int? VisualColumnIndex { get; set; }

        /// <summary>
        /// Optional exact visual PDF column header, for example 39.1 (2).
        /// </summary>
        public string? VisualColumnHeader { get; set; }

        /// <summary>
        /// Optional manufacturer chart hoist reeving metadata for the visual boom column.
        /// </summary>
        public int? NumberOfLines { get; set; }

        /// <summary>
        /// Optional human-facing operating condition label for the selected visual column.
        /// </summary>
        public string? OperatingConditionLabel { get; set; }

        public string? Notes { get; set; }

        public ProjectCraneSnapshotEntity? CraneSnapshot { get; set; }
    }
}
