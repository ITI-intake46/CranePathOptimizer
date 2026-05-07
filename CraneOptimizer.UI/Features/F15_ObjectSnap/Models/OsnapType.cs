namespace CraneOptimizer.UI.Features.F15_ObjectSnap.Models
{
    /// <summary>
    /// All supported Object Snap types, ordered by snap priority
    /// (highest = first checked during a Snap() call).
    /// </summary>
    public enum OsnapType
    {
        /// <summary>Snaps to polygon / polyline / DXF / IFC element vertices.</summary>
        Endpoint = 0,

        /// <summary>Snaps to segment-segment intersection point.</summary>
        Intersection = 1,

        /// <summary>Snaps to the perpendicular foot from the cursor onto an edge.</summary>
        Perpendicular = 2,

        /// <summary>Snaps to the midpoint of any edge.</summary>
        Midpoint = 3,

        /// <summary>Snaps to the closest point on any edge (projection).</summary>
        Nearest = 4,

        /// <summary>Snaps to the nearest configurable grid point.</summary>
        Grid = 5,

        /// <summary>Snaps to arc / circle centre point (AutoCAD CEN).</summary>
        Center = 6,

        /// <summary>Snaps to quadrant points on arcs / circles (0°, 90°, 180°, 270°).</summary>
        Quadrant = 7,
    }
}
