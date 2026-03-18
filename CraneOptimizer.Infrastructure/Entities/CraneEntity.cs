namespace WPF_IFC_test1.Database.Entities
{
    // ── Base (shared columns for both crane types) ────────────────────────────
    /// <summary>
    /// Abstract base mapped via Table-Per-Hierarchy (TPH).
    /// CraneType discriminator: "Tower" or "Mobile".
    /// </summary>
    public abstract class CraneEntity
    {
        public int    Id          { get; set; }
        public int    ProjectId   { get; set; }

        // Discriminator column — set by EF, not manually
        public string CraneType   { get; set; }
        public string DisplayName { get; set; }

        // Position (metres, site coordinate system)
        public double PositionX   { get; set; }
        public double PositionY   { get; set; }
        public double PositionZ   { get; set; }

        // Slewing & cable
        public double SlewAngle   { get; set; }   // degrees 0-360
        public double CableLength { get; set; } = 15;

        // Visibility
        public bool   IsVisible       { get; set; } = true;
        public bool   ShowSwingRadius { get; set; } = true;

        // Navigation
        public ProjectEntity Project { get; set; }

        // Load chart link (optional — set when engineer assigns a chart)
        public int?    LoadChartId    { get; set; }
        public string? AssignedConfig { get; set; }
    }

    // ── Tower Crane ───────────────────────────────────────────────────────────
    /// <summary>
    /// Adds tower-specific columns: TowerHeight, BoomLength, BoomAngle,
    /// CounterJibLength, BaseRadius.
    /// </summary>
    public class TowerCraneEntity : CraneEntity
    {
        public double TowerHeight      { get; set; } = 30;   // m  (10–100)
        public double BoomLength       { get; set; } = 50;   // m  (20–80)
        public double BoomAngle        { get; set; } = 5;    // °  (-5–30)
        public double CounterJibLength { get; set; } = 15;   // m  (5–30)
        public double BaseRadius       { get; set; } = 3;    // m  (2–6)
    }

    // ── Mobile Crane ──────────────────────────────────────────────────────────
    /// <summary>
    /// Adds mobile-specific columns: carrier geometry, boom, outriggers.
    /// </summary>
    public class MobileCraneEntity : CraneEntity
    {
        // Carrier
        public double CarrierLength    { get; set; } = 10;   // m  (6–16)
        public double CarrierWidth     { get; set; } = 2.8;  // m  (2–4)
        public double CarrierHeight    { get; set; } = 1.2;  // m  (0.8–2)

        // Boom
        public double BoomLength       { get; set; } = 35;   // m  (15–100)
        public double BoomAngle        { get; set; } = 60;   // °  (0–85)
        public int    BoomSections     { get; set; } = 4;    // 1–6
        public double BoomBaseWidth    { get; set; } = 0.8;  // m

        // Outriggers
        public double OutriggerSpread  { get; set; } = 7;    // m  (4–12)
        public double OutriggerPadSize { get; set; } = 2.0;  // m  (1–4)
        public bool   ShowOutriggerPads{ get; set; } = true;
    }
}
