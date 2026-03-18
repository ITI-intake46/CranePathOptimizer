using System;
using System.Collections.Generic;

namespace WPF_IFC_test1.Database.Entities
{
    /// <summary>
    /// A planned crane lift: assigns one crane to lift one IFC element.
    /// Core planning table of the app — links Cranes ↔ IfcElements.
    /// Status: "Planned" | "InProgress" | "Done" | "Cancelled"
    /// </summary>
    public class LiftPlanEntity
    {
        public int      Id          { get; set; }
        public int      ProjectId   { get; set; }
        public int      CraneId     { get; set; }
        public int      ElementId   { get; set; }

        public string   LiftName    { get; set; }   // Optional user label
        public string   Status      { get; set; } = "Planned";
        public int      Sequence    { get; set; }   // Order within the day/phase
        public string   Phase       { get; set; }   // Construction phase name

        // Schedule
        public DateTime? PlannedDate     { get; set; }
        public TimeSpan? EstimatedDuration { get; set; }
        public DateTime? ActualStartTime  { get; set; }
        public DateTime? ActualEndTime    { get; set; }

        // Lift parameters at time of planning
        public double?  LiftWeightKg    { get; set; }
        public double?  LiftRadiusM     { get; set; }   // horizontal reach used
        public double?  LiftHeightM     { get; set; }   // hook height needed
        public double?  CraneCapacityKg { get; set; }   // capacity at that radius
        public double?  UtilizationPct  { get; set; }   // Weight / Capacity × 100

        public string   Notes       { get; set; }
        public DateTime CreatedAt   { get; set; } = DateTime.UtcNow;

        // Navigation
        public ProjectEntity    Project   { get; set; }
        public CraneEntity      Crane     { get; set; }
        public IfcElementEntity Element   { get; set; }
        public ICollection<LiftChecklistItemEntity> ChecklistItems { get; set; } = new List<LiftChecklistItemEntity>();
    }

    /// <summary>
    /// Pre-lift safety checklist item for a LiftPlan.
    /// Examples: "Ground conditions checked", "Crane level verified", "Tag lines ready"
    /// </summary>
    public class LiftChecklistItemEntity
    {
        public int      Id          { get; set; }
        public int      LiftPlanId  { get; set; }

        public string   Item        { get; set; }   // Description of the check
        public bool     IsChecked   { get; set; } = false;
        public string   CheckedBy   { get; set; }
        public DateTime? CheckedAt  { get; set; }
        public int      SortOrder   { get; set; }

        // Navigation
        public LiftPlanEntity LiftPlan { get; set; }
    }
}
