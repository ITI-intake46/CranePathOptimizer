using System;

namespace WPF_IFC_test1.Database.Entities
{
    /// <summary>
    /// Stores generated reports (lift plan summary, zone conflict report, weight analysis, etc.)
    /// Content is stored as HTML or plain text.
    /// ReportType: "LiftPlan" | "ZoneConflict" | "WeightAnalysis" | "BuildingAnalysis" | "Custom"
    /// </summary>
    public class ReportEntity
    {
        public int      Id          { get; set; }
        public int      ProjectId   { get; set; }

        public string   Title       { get; set; }
        public string   ReportType  { get; set; }
        public string   ContentHtml { get; set; }   // Full report content
        public string   GeneratedBy { get; set; }   // Username or "System"
        public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;

        // Navigation
        public ProjectEntity Project { get; set; }
    }
}
