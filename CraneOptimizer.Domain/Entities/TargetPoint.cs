namespace CraneOptimizer.Domain.Entities
{
    public class TargetPoint : BaseEntity
    {
        public int Id { get; set; }

        public double Radius { get; set; }
        public double BoomLength { get; set; }
        public double Capacity { get; set; }

        public int CraneSpecificationId { get; set; }
        public CraneSpecification CraneSpecification { get; set; } = null!;
    }

}
