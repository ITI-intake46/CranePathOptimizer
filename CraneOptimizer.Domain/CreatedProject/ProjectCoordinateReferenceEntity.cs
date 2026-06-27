using System;

namespace CraneOptimizer.Domain.CreatedProject
{
    public sealed class ProjectCoordinateReferenceEntity
    {
        public int Id { get; set; }
        public int ProjectInfoId { get; set; }
        public bool IsEnabled { get; set; }
        public string Name { get; set; } = "Coordinate Reference";
        public string? CoordinateSystemName { get; set; }
        public string? EPSGCode { get; set; }
        public double LocalReferenceX { get; set; }
        public double LocalReferenceY { get; set; }
        public double LocalReferenceZ { get; set; }
        public double SurveyEasting { get; set; }
        public double SurveyNorthing { get; set; }
        public double SurveyElevation { get; set; }
        public double RotationToNorthDegrees { get; set; }
        public double Scale { get; set; } = 1.0;
        public string Source { get; set; } = "ManualSinglePoint";
        public double? Latitude { get; set; }
        public double? Longitude { get; set; }
        public string? Description { get; set; }
        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;

        public ProjectInfoEntity? ProjectInfo { get; set; }
    }
}
