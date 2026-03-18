using System;

namespace WPF_IFC_test1.Database.Entities
{
    /// <summary>
    /// Persists results from IfcBuildingAnalyzer so the app doesn't need to
    /// re-parse the IFC file every time the project is opened.
    /// One row per IFC file per analysis run.
    /// </summary>
    public class BuildingAnalysisResultEntity
    {
        public int      Id          { get; set; }
        public int      ProjectId   { get; set; }
        public int?     IfcFileId   { get; set; }   // which IFC file was analyzed

        // Results from IfcBuildingAnalyzer
        public string   BuildingName     { get; set; }
        public double   BuildingArea     { get; set; }    // m²
        public double   BuildingHeight   { get; set; }    // m
        public int      NumberOfStoreys  { get; set; }

        // Total weight & COG of all elements (computed from IfcElements)
        public double?  TotalWeightKg    { get; set; }
        public double?  OverallCOG_X     { get; set; }   // m
        public double?  OverallCOG_Y     { get; set; }   // m
        public double?  OverallCOG_Z     { get; set; }   // m

        public bool     IsAnalyzed       { get; set; } = false;
        public DateTime AnalyzedAt       { get; set; } = DateTime.UtcNow;

        // Navigation
        public ProjectEntity  Project  { get; set; }
        public IfcFileEntity  IfcFile  { get; set; }
    }
}
