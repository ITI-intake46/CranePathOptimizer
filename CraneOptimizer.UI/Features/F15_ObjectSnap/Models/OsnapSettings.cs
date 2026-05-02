using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace CraneOptimizer.UI.Features.F15_ObjectSnap.Models
{
    /// <summary>
    /// Global, app-wide OSnap configuration.
    /// Persisted as JSON via <c>OsnapSettingsService</c> — no DB table needed.
    /// </summary>
    public sealed class OsnapSettings
    {
        // ─── Master toggle ────────────────────────────────────────────────────
        /// <summary>
        /// When false ALL snap types are suppressed, regardless of their individual flags.
        /// </summary>
        public bool MasterEnabled { get; set; } = true;

        // ─── Individual snap-type flags ───────────────────────────────────────
        public bool SnapEndpoint     { get; set; } = true;
        public bool SnapMidpoint     { get; set; } = true;
        public bool SnapIntersection { get; set; } = false;
        public bool SnapPerpendicular{ get; set; } = false;
        public bool SnapNearest      { get; set; } = false;
        public bool SnapGrid         { get; set; } = true;
        public bool SnapCenter       { get; set; } = true;
        public bool SnapQuadrant     { get; set; } = false;

        // ─── Tuning ───────────────────────────────────────────────────────────
        /// <summary>Screen-space snap capture radius in WPF device-independent pixels.</summary>
        public double SnapRadiusPx { get; set; } = 25.0;

        /// <summary>Grid cell size in model-space metres.</summary>
        public double GridSpacing { get; set; } = 1.0;

        /// <summary>
        /// Maximum number of edges compared for Intersection snap (O(n²) guard).
        /// Intersection snap is skipped when edge count exceeds this value.
        /// </summary>
        public int MaxEdgesForIntersection { get; set; } = 200;

        // ─── Source toggles ───────────────────────────────────────────────────
        /// <summary>Allow snapping to imported DXF geometry.</summary>
        public bool SnapToDxf { get; set; } = true;

        /// <summary>Allow snapping to structural IFC element vertices (columns, beams, slabs).</summary>
        public bool SnapToIfc { get; set; } = true;

        // ─── Helpers ──────────────────────────────────────────────────────────
        /// <summary>
        /// Returns the set of enabled <see cref="OsnapType"/> values based on the current flag state.
        /// Respects <see cref="MasterEnabled"/> — returns empty set when master is off.
        /// </summary>
        [JsonIgnore]
        public IReadOnlySet<OsnapType> EnabledTypes
        {
            get
            {
                var set = new HashSet<OsnapType>();
                if (!MasterEnabled) return set;
                if (SnapEndpoint)      set.Add(OsnapType.Endpoint);
                if (SnapMidpoint)      set.Add(OsnapType.Midpoint);
                if (SnapIntersection)  set.Add(OsnapType.Intersection);
                if (SnapPerpendicular) set.Add(OsnapType.Perpendicular);
                if (SnapNearest)       set.Add(OsnapType.Nearest);
                if (SnapGrid)          set.Add(OsnapType.Grid);
                if (SnapCenter)        set.Add(OsnapType.Center);
                if (SnapQuadrant)      set.Add(OsnapType.Quadrant);
                return set;
            }
        }

        public OsnapSettings Clone() =>
            JsonSerializer.Deserialize<OsnapSettings>(JsonSerializer.Serialize(this))!;
    }
}
