namespace WPF_IFC_test1.Database.Entities
{
    /// <summary>
    /// Reference table of material densities.
    /// Replaces the hardcoded switch in MainViewModel.GetMaterialDensity():
    ///   concrete → 2400, steel → 7850, wood → 600, brick → 1800
    /// Users can add custom materials here.
    /// IsSystem = true rows are seeded on first run and cannot be deleted.
    /// </summary>
    public class MaterialCatalogEntity
    {
        public int      Id              { get; set; }
        public string   MaterialName    { get; set; }      // e.g. "Concrete"
        public string   Keywords        { get; set; }      // comma-separated for lookup e.g. "concrete,beton"
        public double   DensityKgPerM3  { get; set; }      // kg/m³
        public string   Category        { get; set; }      // "Concrete" | "Steel" | "Wood" | "Masonry" | "Other"
        public string   Description     { get; set; }
        public bool     IsSystem        { get; set; } = false;   // true = seeded, cannot be deleted
    }
}
