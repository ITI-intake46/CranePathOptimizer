using CraneOptimizer.Domain.Common;

namespace CraneOptimizer.Domain.Schemas
{
    public class PrecastElement
    {
        public string ElementID { get; set; }

        public string ElementType { get; set; }

        public double? ElementLength { get; set; }

        public double? ElementWidth { get; set; }

        public double? ElementHeight { get; set; }

        public string ElementMaterial { get; set; }

        public double? ElementWT { get; set; }

        public double? ElementVolume { get; set; }

        public DomainPoint3D ElementCOG { get; set; } = new DomainPoint3D();
        public double? BBoxLength { get; set; }
        public double? BBoxWidth { get; set; }
        public double? BBoxHeight { get; set; }
    }
}
