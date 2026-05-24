using CraneOptimizer.Domain.CreatedProject.CraneFiles;

namespace CraneOptimizer.Domain.CreatedProject.OptimizationFiles
{
    /// <summary>
    /// Represents one crane optimization execution.
    /// Stores summary information and references the crane snapshot used in the run.
    /// </summary>
    public sealed class ProjectOptimizationRunEntity
    {
        public int Id { get; set; }

        public int ProjectInfoId { get; set; }

        public int? CraneSnapshotId { get; set; }

        public string? RunName { get; set; }

        /// <summary>
        /// Created, Running, Completed, Failed, Cancelled.
        /// </summary>
        public string Status { get; set; } = "Created";

        public DateTime StartedAtUtc { get; set; } = DateTime.UtcNow;

        public DateTime? FinishedAtUtc { get; set; }

        public int? TotalElements { get; set; }

        public int? SafeElements { get; set; }

        public int? UnsafeElements { get; set; }

        public double? CoverageRatio { get; set; }

        public double? AverageUtilizationPercent { get; set; }

        /// <summary>
        /// Stores optimizer settings used for this run.
        /// </summary>
        public string? SettingsJson { get; set; }

        /// <summary>
        /// Stores summary output as JSON for reporting/review.
        /// </summary>
        public string? ResultSummaryJson { get; set; }

        public string CranePositionsJson { get; set; } = string.Empty;

        public ProjectInfoEntity? ProjectInfo { get; set; }

        public ProjectCraneSnapshotEntity? CraneSnapshot { get; set; }

        public List<ProjectOptimizationElementResultEntity> ElementResults { get; set; } = new();
    }
}
