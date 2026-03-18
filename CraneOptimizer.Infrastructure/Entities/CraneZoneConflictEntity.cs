using System;

namespace WPF_IFC_test1.Database.Entities
{
    /// <summary>
    /// Recorded conflict when a crane's swing radius or hook path
    /// intersects a Forbidden zone.
    /// Detected by ZoneManager.Contains() / PointInPolygon().
    /// Severity: "Warning" | "Critical"
    /// </summary>
    public class CraneZoneConflictEntity
    {
        public int      Id          { get; set; }
        public int      ProjectId   { get; set; }
        public int      CraneId     { get; set; }
        public int      ZoneId      { get; set; }

        public string   Severity    { get; set; } = "Warning";   // "Warning" | "Critical"
        public string   Description { get; set; }

        // Position of crane at time of conflict
        public double   CraneX      { get; set; }
        public double   CraneY      { get; set; }
        public double   CraneZ      { get; set; }
        public double   SlewAngle   { get; set; }

        public bool     IsResolved  { get; set; } = false;
        public string   Resolution  { get; set; }   // How it was resolved
        public DateTime DetectedAt  { get; set; } = DateTime.UtcNow;
        public DateTime? ResolvedAt { get; set; }

        // Navigation
        public ProjectEntity Project { get; set; }
        public CraneEntity   Crane   { get; set; }
        public ZoneEntity    Zone    { get; set; }
    }
}
