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

        public string? Notes { get; set; }

        public ProjectCraneSnapshotEntity? CraneSnapshot { get; set; }
    }
}
