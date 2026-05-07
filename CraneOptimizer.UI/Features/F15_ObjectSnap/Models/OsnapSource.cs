namespace CraneOptimizer.UI.Features.F15_ObjectSnap.Models
{
    /// <summary>
    /// Identifies where a snap point came from so the indicator
    /// can use different colours per source (Zone=cyan, DXF=orange, IFC=green).
    /// </summary>
    public enum OsnapSource
    {
        Zone,   // user-drawn zone / site boundary polygon
        Dxf,    // imported DXF line / polyline / arc geometry
        Ifc,    // structural IFC element vertices (columns, beams, slabs)
        Grid,   // virtual grid point (no geometry source)
    }
}
