using System;
using System.Collections.Generic;

namespace WPF_IFC_test1.Database.Entities
{
    public class ProjectEntity
    {
        public int    Id                     { get; set; }
        public string ProjectName            { get; set; }
        public string Country                { get; set; } = "Egypt";
        public double ConstructionSiteSize   { get; set; } = 200.0;
        public string ProjectDescription     { get; set; }
        public string Location               { get; set; }

        // Unit system
        public string UnitSystem             { get; set; } = "Metric";
        public string LengthUnit             { get; set; } = "m";
        public string MassUnit               { get; set; } = "kg";
        public string AreaUnit               { get; set; } = "m²";
        public string VolumeUnit             { get; set; } = "m³";
        public string PressureUnit           { get; set; } = "kPa";
        public string ForceUnit              { get; set; } = "kN";
        public string DecimalSeparator       { get; set; } = ".";
        public bool   ShowAdvancedParameters { get; set; } = false;

        public DateTime CreatedAt            { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt            { get; set; } = DateTime.UtcNow;

        // ── Navigation ────────────────────────────────────────────────────────
        public ICollection<IfcElementEntity>            IfcElements            { get; set; } = new List<IfcElementEntity>();
        public ICollection<CraneEntity>                 Cranes                 { get; set; } = new List<CraneEntity>();
        public ICollection<ZoneEntity>                  Zones                  { get; set; } = new List<ZoneEntity>();
        public ICollection<IfcFileEntity>               IfcFiles               { get; set; } = new List<IfcFileEntity>();
        public ICollection<DxfFileEntity>               DxfFiles               { get; set; } = new List<DxfFileEntity>();
        public ICollection<LiftPlanEntity>              LiftPlans              { get; set; } = new List<LiftPlanEntity>();
        public ICollection<CraneZoneConflictEntity>     CraneZoneConflicts     { get; set; } = new List<CraneZoneConflictEntity>();
        public ICollection<SiteObstacleEntity>          SiteObstacles          { get; set; } = new List<SiteObstacleEntity>();
        public ICollection<ElementGroupEntity>          ElementGroups          { get; set; } = new List<ElementGroupEntity>();
        public ICollection<DimensionAnnotationEntity>   DimensionAnnotations   { get; set; } = new List<DimensionAnnotationEntity>();
        public ICollection<ProjectNoteEntity>           ProjectNotes           { get; set; } = new List<ProjectNoteEntity>();
        public ICollection<ReportEntity>                Reports                { get; set; } = new List<ReportEntity>();
        public ICollection<BuildingAnalysisResultEntity> BuildingAnalysisResults { get; set; } = new List<BuildingAnalysisResultEntity>();
    }
}
