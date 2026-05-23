namespace CraneOptimizer.Domain.CreatedProject.ProjectState
{
    public sealed class CraneSetupEntity
    {
        public int Id { get; set; }

        public int ProjectInfoId { get; set; }

        public string SceneCraneGuid { get; set; } = Guid.NewGuid().ToString("N");

        public string? SourceCraneId { get; set; }

        public int? SourceCraneSpecificationId { get; set; }

        public string CraneName { get; set; } = string.Empty;

        public string CraneType { get; set; } = string.Empty;

        public double PositionX { get; set; }

        public double PositionY { get; set; }

        public double PositionZ { get; set; }

        public double Rotation { get; set; }

        public double SlewAngle { get; set; }

        public double PivotOffsetX { get; set; }

        public double PivotOffsetY { get; set; }

        public double PivotOffsetZ { get; set; }

        public double BoomAngle { get; set; }

        public double BoomLength { get; set; }

        public bool IsSelected { get; set; }

        public bool IsValidPlacement { get; set; } = true;

        public double? CurrentRadius { get; set; }

        public double? CurrentCapacity { get; set; }

        public string? ConfigurationJson { get; set; }

        public ProjectInfoEntity? ProjectInfo { get; set; }
    }
}
