using System;
using System.Collections.Generic;

namespace WPF_IFC_test1.Database.Entities
{
    /// <summary>
    /// DB entity for CraneZone.
    /// Vertices are stored as child rows (ZoneVertexEntity) ordered by Index.
    /// Color is persisted as a hex string e.g. "#641BAE60".
    /// </summary>
    public class ZoneEntity
    {
        public int    Id        { get; set; }
        public int    ProjectId { get; set; }

        public string Name      { get; set; }

        /// <summary>"Allowed" | "Forbidden" | "SiteBoundary"</summary>
        public string ZoneType  { get; set; }

        public double MinHeight { get; set; }
        public double MaxHeight { get; set; }
        public bool   IsVisible { get; set; } = true;
        public string Notes     { get; set; }

        /// <summary>
        /// Hex ARGB colour override (null = use default colour for ZoneType).
        /// Example: "#641BAE60" — 100% alpha, green.
        /// </summary>
        public string ColorHex  { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Navigation
        public ProjectEntity           Project  { get; set; }
        public ICollection<ZoneVertexEntity> Vertices { get; set; } = new List<ZoneVertexEntity>();
    }

    /// <summary>
    /// One row = one vertex of a CraneZone polygon.
    /// Order determines the polygon winding.
    /// Z is the ground-plane elevation (metres).
    /// </summary>
    public class ZoneVertexEntity
    {
        public int    Id     { get; set; }
        public int    ZoneId { get; set; }
        public int    Order  { get; set; }   // 0-based index in polygon

        public double X      { get; set; }
        public double Y      { get; set; }
        public double Z      { get; set; }

        // Navigation
        public ZoneEntity Zone { get; set; }
    }
}
