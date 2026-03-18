using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Media;
using WPF_IFC_test1.Database.Entities;

namespace WPF_IFC_test1.Database.Services
{
    /// <summary>
    /// Shared static service for capacity lookup and heatmap colour generation.
    /// Used by: Feasibility Checker, Heatmap View, Compare Window, 3D Capacity Rings.
    /// </summary>
    public static class CraneLoadChartLookup
    {
        // ── Conservative ceiling lookup ────────────────────────────────────────

        /// <summary>
        /// Returns capacity in tonnes at the given (radius, boomLength).
        ///
        /// LOOKUP RULE — always use the NEXT HIGHER table value:
        ///   • If radius=35 and the table has 34 and 36 → use 36
        ///   • If boom=50 and the table has 47.6 and 52.0 → use 52.0
        ///   • Exact match → use the exact row/column
        ///
        /// This is the conservative (safe) approach: capacity can only be
        /// overstated by interpolating down, never by snapping up.
        /// Returns null if either value is beyond the rated envelope.
        /// </summary>
        public static double? GetCapacity(
            List<CraneLoadChartPointEntity> points,
            double radiusM,
            double boomLengthM)
        {
            if (points == null || points.Count == 0) return null;

            // Build lookup — GroupBy tolerates duplicate (radius, boom) DB entries
            var lookup = points
                .Where(p => p.CapacityTonne.HasValue)
                .GroupBy(p => (Math.Round(p.RadiusM, 4), Math.Round(p.BoomLengthM, 4)))
                .ToDictionary(g => g.Key, g => g.Last().CapacityTonne!.Value);

            // Distinct sorted axis values
            var booms = points.Select(p => p.BoomLengthM).Distinct().OrderBy(b => b).ToList();
            var radii = points.Select(p => p.RadiusM).Distinct().OrderBy(r => r).ToList();

            if (booms.Count == 0 || radii.Count == 0) return null;

            // Ceiling snap: find the smallest table value >= requested value.
            // FirstOrDefault returns 0 (double default) when nothing qualifies,
            // which means the requested value exceeds the table maximum → not rated.
            double boomCeil   = booms.FirstOrDefault(b => b >= boomLengthM);
            double radiusCeil = radii.FirstOrDefault(r => r >= radiusM);

            // 0 means no entry was >= the requested value → beyond the rated envelope
            if (boomCeil   == 0 && boomLengthM   > booms.Last())   return null;
            if (radiusCeil == 0 && radiusM        > radii.Last())   return null;

            // If the request is below the table minimum, clamp to the first entry
            if (boomCeil   == 0) boomCeil   = booms[0];
            if (radiusCeil == 0) radiusCeil = radii[0];

            var key = (Math.Round(radiusCeil, 4), Math.Round(boomCeil, 4));
            return lookup.TryGetValue(key, out double cap) ? cap : (double?)null;
        }

        // ── Heatmap colour ─────────────────────────────────────────────────────

        private static readonly Color _red   = Color.FromRgb(0xCC, 0x22, 0x00);
        private static readonly Color _amber = Color.FromRgb(0xD4, 0xA8, 0x20);
        private static readonly Color _green = Color.FromRgb(0x27, 0xAE, 0x60);

        /// <summary>
        /// Maps a capacity value (given min/max of the chart) to a heatmap colour.
        /// Red (min) -> Amber (mid) -> Green (max).
        /// Returns semi-transparent brush (alpha 0.6) for overlay use.
        /// </summary>
        public static SolidColorBrush GetHeatmapBrush(double value, double min, double max)
        {
            Color c = GetHeatmapColor(value, min, max);
            c.A = 153; // 0.6 alpha
            return new SolidColorBrush(c);
        }

        public static Color GetHeatmapColor(double value, double min, double max)
        {
            if (max <= min) return _amber;
            double t = Math.Max(0, Math.Min(1, (value - min) / (max - min)));
            return t < 0.5 ? Lerp(_red, _amber, t * 2) : Lerp(_amber, _green, (t - 0.5) * 2);
        }

        private static Color Lerp(Color a, Color b, double t)
        {
            return Color.FromArgb(
                (byte)(a.A + t * (b.A - a.A)),
                (byte)(a.R + t * (b.R - a.R)),
                (byte)(a.G + t * (b.G - a.G)),
                (byte)(a.B + t * (b.B - a.B)));
        }

        // ── Utilisation classification ─────────────────────────────────────────

        public enum FeasibilityStatus { Safe, Marginal, OverRated, NoChart }

        public static FeasibilityStatus Classify(double utilisationPct)
        {
            if (utilisationPct < 80) return FeasibilityStatus.Safe;
            if (utilisationPct < 95) return FeasibilityStatus.Marginal;
            return FeasibilityStatus.OverRated;
        }

        // ── Manufacturer detection ─────────────────────────────────────────────

        private static readonly (string[] prefixes, string manufacturer)[] _mfgTable =
        {
            (new[] { "LTM", "LTC", "LTT", "LG", "LTR", "MK" }, "Liebherr"),
            (new[] { "AC", "GMK", "RT", "GRT", "TMS" },         "Grove"),
            (new[] { "ATF", "CC", "CTL", "GTC" },               "Tadano"),
            (new[] { "SCC", "STC", "SCX", "MLC" },              "Manitowoc"),
            (new[] { "SL", "TL", "WL", "CTG" },                 "Terex"),
        };

        /// <summary>
        /// Given a crane model string (e.g. "LTM1200-5.1"), returns the manufacturer name.
        /// Returns empty string if not recognised.
        /// </summary>
        public static string GuessManufacturer(string model)
        {
            if (string.IsNullOrWhiteSpace(model)) return "";
            string upper = model.Trim().ToUpperInvariant();
            foreach (var (prefixes, mfg) in _mfgTable)
                foreach (string p in prefixes)
                    if (upper.StartsWith(p, StringComparison.OrdinalIgnoreCase))
                        return mfg;
            return "";
        }
    }
}
