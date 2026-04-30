using CraneOptimizer.Domain.Common;

namespace CraneOptimizer.Domain.Schemas
{
    public class PrecastElement
    {
        public Guid ModelInstanceId { get; set; }

        public string ElementID { get; set; }

        public string ElementType { get; set; }

        public double? ElementLength { get; set; }

        public double? ElementWidth { get; set; }

        public double? ElementHeight { get; set; }

        public string ElementMaterial { get; set; }

        public double? ElementWT { get; set; }

        public double? ElementVolume { get; set; }

        public DomainPoint3D OriginalElementCOG { get; set; } = new DomainPoint3D();

        public DomainPoint3D ElementCOG { get; set; } = new DomainPoint3D();
        public double? BBoxLength { get; set; }
        public double? BBoxWidth { get; set; }
        public double? BBoxHeight { get; set; }
        public List<IfcElementMesh> Meshes { get; set; } = new();
    }

    public class IfcElementMesh
    {
        public List<DomainPoint3D> Positions { get; set; } = new();
        public List<int> TriangleIndices { get; set; } = new();
        public List<DomainVector3D> Normals { get; set; } = new();
    }
}
