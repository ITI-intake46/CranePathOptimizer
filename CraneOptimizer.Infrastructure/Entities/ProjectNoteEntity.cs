using System;

namespace WPF_IFC_test1.Database.Entities
{
    /// <summary>
    /// Freeform notes that can be attached to any entity in the project.
    /// TargetType + TargetId identify what the note is about.
    /// TargetType: "Project" | "Crane" | "Element" | "Zone" | "LiftPlan"
    /// </summary>
    public class ProjectNoteEntity
    {
        public int      Id          { get; set; }
        public int      ProjectId   { get; set; }

        public string   Title       { get; set; }
        public string   Content     { get; set; }
        public string   TargetType  { get; set; } = "Project";
        public int?     TargetId    { get; set; }   // FK to whichever entity the note belongs to
        public string   Author      { get; set; }
        public bool     IsPinned    { get; set; } = false;
        public DateTime CreatedAt   { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt   { get; set; } = DateTime.UtcNow;

        // Navigation
        public ProjectEntity Project { get; set; }
    }
}
