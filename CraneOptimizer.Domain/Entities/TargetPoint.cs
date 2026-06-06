namespace CraneOptimizer.Domain.Entities
{
    public class TargetPoint : BaseEntity
    {
        public int Id { get; set; }

        public double Radius { get; set; }
        public double BoomLength { get; set; }
        public double Capacity { get; set; }

        /// <summary>
        /// Optional one-based visual column index from an exact PDF load-chart table.
        /// This preserves repeated manufacturer boom columns such as 39.1, 39.1 (2), 39.1 (3).
        /// Null means the point came from a normal simple 2D chart.
        /// </summary>
        public int? VisualColumnIndex { get; set; }

        /// <summary>
        /// Optional display header from the exact PDF visual table.
        /// Example: 39.1 (2). This is presentation/audit metadata and is not used as a numeric boom length.
        /// </summary>
        public string? VisualColumnHeader { get; set; }

        public int CraneSpecificationId { get; set; }
        public CraneSpecification CraneSpecification { get; set; } = null!;
    }
}
