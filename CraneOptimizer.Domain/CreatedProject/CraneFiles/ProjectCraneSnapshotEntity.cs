using CraneOptimizer.Domain.CreatedProject.OptimizationFiles;

namespace CraneOptimizer.Domain.CreatedProject.CraneFiles
{
    /// <summary>
    /// Snapshot of a crane selected from the global crane database.
    /// This protects old projects from changes in the global crane database.
    /// </summary>
    public sealed class ProjectCraneSnapshotEntity
    {
        public int Id { get; set; }

        public int ProjectInfoId { get; set; }

        /// <summary>
        /// Reference to the global crane database.
        /// Stored for traceability only.
        /// The project must use the snapshot values for calculations.
        /// </summary>
        public string? SourceCraneId { get; set; }

        public string CraneName { get; set; } = string.Empty;
        public string CraneType { get; set; } = string.Empty;

        public string? Manufacturer { get; set; }

        public string? Model { get; set; }

        public double? MaxCapacityTons { get; set; }

        public double? VehicleLengthM { get; set; }

        public double? VehicleWidthM { get; set; }

        public double? VehicleHeightM { get; set; }

        public double? OutriggerLengthM { get; set; }

        public double? OutriggerWidthM { get; set; }

        public double? MinRadiusM { get; set; }

        public double? MaxRadiusM { get; set; }

        public double? MinBoomLengthM { get; set; }

        public double? MaxBoomLengthM { get; set; }

        public double? MaxHookHeightM { get; set; }

        public double? CounterWeightTons { get; set; }

        public double? MachineRotation { get; set; }

        public double? DeratingRatio { get; set; }

        /// <summary>
        /// Stores selected crane configuration as JSON.
        /// Example: boom length, jib length, counterweight, setup mode.
        /// </summary>
        public string? ConfigurationJson { get; set; }

        /// <summary>
        /// Stores Outrigger configuration as JSON.
        /// Example: Outrigger length, Outrigger width.
        /// </summary>
        public string? OutriggerConfig { get; set; }

        public DateTime SnapshottedAtUtc { get; set; } = DateTime.UtcNow;

        public ProjectInfoEntity? ProjectInfo { get; set; }

        public List<ProjectCraneLoadChartSnapshotEntity> LoadChartRows { get; set; } = new();

        public List<ProjectOptimizationRunEntity> OptimizationRuns { get; set; } = new();
    }
}
