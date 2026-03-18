using System;

namespace WPF_IFC_test1.Database.Entities
{
    /// <summary>
    /// Replaces RecentProjectsHelper's JSON file (recent_projects.json).
    /// Stores the last 20 opened projects with metadata for the welcome screen.
    /// Status mirrors the values in RecentProjectsHelper: "Active" | "Completed" | "Archived".
    /// </summary>
    public class RecentProjectEntity
    {
        public int      Id           { get; set; }
        public int      ProjectId    { get; set; }

        public string   ProjectName  { get; set; }
        public string   Location     { get; set; }   // Directory path
        public string   IfcFileName  { get; set; }   // Last loaded IFC filename
        public string   Status       { get; set; } = "Active";   // "Active" | "Completed" | "Archived"
        public string   StatusColor  { get; set; } = "#4CAF50";  // Hex color for UI badge
        public DateTime LastOpenedAt { get; set; } = DateTime.UtcNow;

        // Navigation
        public ProjectEntity Project { get; set; }
    }
}
