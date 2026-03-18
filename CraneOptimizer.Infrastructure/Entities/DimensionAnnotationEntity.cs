namespace WPF_IFC_test1.Database.Entities
{
    /// <summary>
    /// Persists DimensionAnnotation objects drawn in the ZoneModule.
    /// Replaces the Annotations list in the JSON .craneproject file.
    /// Type: "Linear" | "Aligned" | "Radius"
    /// All coordinates are in IFC world space (metres).
    /// </summary>
    public class DimensionAnnotationEntity
    {
        public int      Id          { get; set; }
        public int      ProjectId   { get; set; }

        public string   AnnotationType { get; set; }   // "Linear" | "Aligned" | "Radius"
        public string   Label          { get; set; }

        // Start and end points (metres)
        public double   StartX      { get; set; }
        public double   StartY      { get; set; }
        public double   StartZ      { get; set; }
        public double   EndX        { get; set; }
        public double   EndY        { get; set; }
        public double   EndZ        { get; set; }

        public double   OffsetDistance { get; set; }   // How far the dim line is offset
        public bool     IsVisible   { get; set; } = true;

        // Navigation
        public ProjectEntity Project { get; set; }
    }
}
