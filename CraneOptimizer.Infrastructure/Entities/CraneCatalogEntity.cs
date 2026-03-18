namespace WPF_IFC_test1.Database.Entities
{
    /// <summary>
    /// Library of real-world crane models with manufacturer specifications.
    /// Users pick from this catalog when adding a crane, instead of entering
    /// every parameter manually.
    /// CraneType: "Tower" | "Mobile"
    /// </summary>
    public class CraneCatalogEntity
    {
        public int      Id              { get; set; }
        public string   CraneType       { get; set; }   // "Tower" | "Mobile"
        public string   Manufacturer    { get; set; }   // e.g. "Liebherr"
        public string   Model           { get; set; }   // e.g. "LTM 1100-4.2"
        public string   Description     { get; set; }

        // Capacity
        public double   MaxCapacityKg   { get; set; }   // at minimum radius
        public double   MaxReachM       { get; set; }   // max jib/boom length

        // Tower crane defaults
        public double?  DefaultTowerHeight      { get; set; }
        public double?  DefaultBoomLength       { get; set; }
        public double?  DefaultCounterJibLength { get; set; }
        public double?  DefaultBaseRadius       { get; set; }

        // Mobile crane defaults
        public double?  DefaultCarrierLength    { get; set; }
        public double?  DefaultCarrierWidth     { get; set; }
        public double?  DefaultCarrierHeight    { get; set; }
        public double?  DefaultOutriggerSpread  { get; set; }

        public bool     IsSystem        { get; set; } = false;   // true = seeded data
    }
}
