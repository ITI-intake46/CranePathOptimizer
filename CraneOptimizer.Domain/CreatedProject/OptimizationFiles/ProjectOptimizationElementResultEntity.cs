namespace CraneOptimizer.Domain.CreatedProject.OptimizationFiles
{
    /// <summary>
    /// Stores per-element result for an optimization run.
    /// </summary>
    public sealed class ProjectOptimizationElementResultEntity
    {
        public int Id { get; set; }

        public int OptimizationRunId { get; set; }

        public string ElementGlobalId { get; set; } = string.Empty;

        public int AssignedCraneIndex { get; set; }

        public double? RadiusM { get; set; }
        public double? BoomLengthM { get; set; }

        public double? RequiredCapacityTon { get; set; }

        public double? AvailableCapacityTon { get; set; }

        public double? UtilizationPercent { get; set; }

        /// <summary>
        /// Liftable, Overweight, Overweight, OutOfRadius, HeightFail, MissingData, SpecialCase.
        /// </summary>
        public string? Status { get; set; }

        public string? Reason { get; set; }

        public ProjectOptimizationRunEntity? OptimizationRun { get; set; }
    }
}
