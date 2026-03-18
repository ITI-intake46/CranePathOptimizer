namespace WPF_IFC_test1.Database.Entities
{
    /// <summary>
    /// Physical obstacles on the construction site that cranes must avoid.
    /// Examples: existing buildings, power lines, trees, roads, scaffolding.
    /// Stored as a bounding box (min/max XYZ) in site world coordinates.
    /// Type: "Building" | "PowerLine" | "Tree" | "Road" | "Scaffolding" | "Other"
    /// </summary>
    public class SiteObstacleEntity
    {
        public int      Id          { get; set; }
        public int      ProjectId   { get; set; }

        public string   Name        { get; set; }
        public string   ObstacleType { get; set; } = "Other";
        public string   Description { get; set; }

        // Bounding box (metres, world coordinates)
        public double   MinX        { get; set; }
        public double   MinY        { get; set; }
        public double   MinZ        { get; set; }
        public double   MaxX        { get; set; }
        public double   MaxY        { get; set; }
        public double   MaxZ        { get; set; }

        public bool     IsVisible   { get; set; } = true;
        public string   ColorHex    { get; set; } = "#FF6600";

        // Navigation
        public ProjectEntity Project { get; set; }
    }
}
