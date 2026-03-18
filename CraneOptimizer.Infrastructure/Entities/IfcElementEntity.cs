namespace WPF_IFC_test1.Database.Entities
{
    /// <summary>
    /// DB entity for IfcElementModel. Stores all parsed IFC element data.
    /// Weight is always persisted in kg; unit-display is handled by the ViewModel.
    /// COG coordinates are stored in metres (world space).
    /// </summary>
    public class IfcElementEntity
    {
        public int    Id          { get; set; }
        public int    ProjectId   { get; set; }

        // IFC identity
        public string GlobalId    { get; set; }   // IFC GUID (22-char or standard GUID)
        public string Name        { get; set; }
        public string DisplayName { get; set; }
        public string Mark        { get; set; }   // Element tag / mark number
        public string Type        { get; set; }   // IFC type name e.g. "IfcColumn"

        // Dimensions (stored in metres, displayed according to SelectedUnit)
        public double? Length     { get; set; }
        public double? Width      { get; set; }
        public double? Height     { get; set; }
        public double? Volume     { get; set; }   // m³

        // Mass
        public double? Weight     { get; set; }   // Always kg
        public double? DisplayWeight { get; set; }
        public string  WeightUnit { get; set; } = "kg";

        // Material
        public string  Material   { get; set; }

        // Centre of Gravity — metres, world space
        public double? COG_X      { get; set; }
        public double? COG_Y      { get; set; }
        public double? COG_Z      { get; set; }

        // Visual
        public string  ElementColor { get; set; } = "#FFFFFF";
        public bool    IsSelected   { get; set; } = false;

        // Navigation
        public ProjectEntity Project { get; set; }
    }
}
