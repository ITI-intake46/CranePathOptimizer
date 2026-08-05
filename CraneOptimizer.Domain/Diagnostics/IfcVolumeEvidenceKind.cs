namespace CraneOptimizer.Domain.Diagnostics
{
    public enum IfcVolumeEvidenceKind
    {
        Missing,
        IfcQuantity,
        IfcProperty,
        MeshGeometry,
        BoundingBox,
        DerivedFromWeight,
        Unknown
    }
}
