using System;
using System.Collections.Generic;

namespace WPF_IFC_test1.Database.Entities
{
    /// <summary>
    /// One crane load chart (e.g. LTM 1200-5.1, 360°, full outrigger).
    /// Each chart has many CraneLoadChartPointEntity rows — one per radius/boom combo.
    /// Linked to CraneCatalog optionally (CatalogId can be null for imported charts).
    /// </summary>
    public class CraneLoadChartEntity
    {
        public int Id { get; set; }

        // ── Crane identity ────────────────────────────────────────────────────
        public string CraneModel { get; set; } = "";   // e.g. "LTM 1200-5.1"
        public string Manufacturer { get; set; } = "";   // e.g. "Liebherr"
        public string ConfigName { get; set; } = "";   // e.g. "360° full outrigger"

        // ── Chart metadata ────────────────────────────────────────────────────
        public string CraneCategory { get; set; } = "Mobile"; // "Mobile" | "Tower"
        public string ChartType { get; set; } = "360"; // "360" | "Over-rear" | "Over-side"
        public string OutriggerConfig { get; set; } = "";    // "Full" | "Partial 75%" | "None"
        public double CounterweightKg { get; set; } = 0;    // e.g. 78000
        public string Notes { get; set; } = "";

        // ── Physical dimensions (stored in mm, 0 = not entered) ──────────────
        public double OutriggerSpanMm { get; set; } = 0;    // W: pad-to-pad width
        public double TotalLengthMm { get; set; } = 0;      // L: transport length
        public double HeightMm { get; set; } = 0;           // H: transport height
        public double TransportWidthMm { get; set; } = 0;   // T: travel width incl. tires
        public double SpanToSpanMm { get; set; } = 0;       // B: front-to-rear span
        public double BoundingRadiusM { get; set; } = 0;    // calculated + user extra (metres)

        // ── Source image ──────────────────────────────────────────────────────
        public string SourceImagePath { get; set; } = "";    // path to original photo
        public string ImportMethod { get; set; } = "AI";  // "AI" | "Manual" | "CSV"
        public DateTime ImportedAt { get; set; } = DateTime.UtcNow;

        // ── Capacity / derating ───────────────────────────────────────────────
        /// <summary>Crane derating factor in percent (25 / 50 / 75 / 100). Default = 100.</summary>
        public int DeratingPct { get; set; } = 100;

        // ── Optional FK to catalog ────────────────────────────────────────────
        public int? CatalogId { get; set; }
        public CraneCatalogEntity? Catalog { get; set; }

        // ── Navigation ────────────────────────────────────────────────────────
        public List<CraneLoadChartPointEntity> Points { get; set; } = new();
    }

    /// <summary>
    /// One data point: at a given radius and boom length, what is the max capacity?
    /// null capacity = not rated (cell was blank in the original chart).
    /// </summary>
    public class CraneLoadChartPointEntity
    {
        public int Id { get; set; }
        public int ChartId { get; set; }
        public CraneLoadChartEntity Chart { get; set; } = null!;

        public double RadiusM { get; set; }   // working radius in metres
        public double BoomLengthM { get; set; }   // boom/jib length in metres
        public double? CapacityTonne { get; set; }   // null = not rated
    }
}