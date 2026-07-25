using CraneOptimizer.Domain.Common;
using CraneOptimizer.Domain.Schemas.Enums;

namespace CraneOptimizer.Domain.Schemas
{
    public class GreenAreaZone
    {
        public Guid ZoneId { get; set; } = Guid.NewGuid();

        public List<DomainPoint3D> Vertices { get; set; } = new();

        public double Area { get; set; }

        public string Name { get; set; }

        public ZoneType Type { get; set; }

        public double? MinimumHeight { get; set; }

        public double? MaximumHeight { get; set; }

        public Guid? SourceDxfId { get; set; }

        public string? SourceDxfFileName { get; set; }

        public string? SourceDxfFilePath { get; set; }

        public string? SourceLayerName { get; set; }

        public string? SourceCandidateId { get; set; }

        public string? ImportMethod { get; set; }

        public ZoneType? OriginalAssignmentType { get; set; }
    }
}
