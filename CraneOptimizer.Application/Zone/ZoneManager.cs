// FILE: ZoneModule/ZoneManager.cs
// FIXES:
//   BUG FIX: GetReasonCode() — operator precedence on allowed-zone filter.
//            `z.MinHeight > 0 || z.MaxHeight < 999` was evaluated BEFORE `&&`,
//            causing any zone with MaxHeight < 999 to pass the check regardless
//            of ZoneType or IsVisible. Added parentheses to fix grouping.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows.Media.Media3D;
using WPF_IFC_test1.ZoneModule.Models;

namespace WPF_IFC_test1.ZoneModule
{
    // ── optimizer reason codes (spec Section 9) ─────────────────────────
    public enum ZoneEvaluationResult
    {
        Valid,
        OutsideSite,
        InForbiddenZone,
        OutsideAllowedZone
    }

    public class ZoneManager : INotifyPropertyChanged
    {
        public ObservableCollection<CraneZone> Zones { get; } = new();
        public ObservableCollection<DimensionAnnotation> Annotations { get; } = new();
        public CraneZone SiteBoundary { get; private set; }

        public event Action ZonesChanged;
        public event Action<CraneZone> ZoneSelected;

        public event PropertyChangedEventHandler PropertyChanged;

        private CraneZone _selectedZone;
        public CraneZone SelectedZone
        {
            get => _selectedZone;
            set
            {
                if (_selectedZone == value) return;
                _selectedZone = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SelectedZone)));
                ZoneSelected?.Invoke(value);
            }
        }

        private SpatialGrid _spatialGrid;

        public bool HasSiteBoundary => SiteBoundary != null;

        public void NotifyZonesChanged() => ZonesChanged?.Invoke();

        public void SelectZone(CraneZone zone) => SelectedZone = zone;

        public void SetSiteBoundary(List<Point3D> vertices)
        {
            SiteBoundary = new CraneZone
            {
                ZoneType = ZoneType.SiteBoundary,
                Name = "Site Boundary",
                Vertices = new List<Point3D>(vertices),
                MinHeight = 0,
                MaxHeight = 999
            };
            RebuildSpatialIndex();
            ZonesChanged?.Invoke();
        }

        public void AddZone(CraneZone zone)
        {
            // Site boundary is no longer required before drawing Allowed/Forbidden zones.
            if (zone.FootprintArea < 1.0)
                throw new InvalidOperationException(
                    $"Zone area {zone.FootprintArea:F2} m² is below the 1 m² minimum.");
            Zones.Add(zone);
            RebuildSpatialIndex();
            ZonesChanged?.Invoke();
        }

        public void RemoveZone(CraneZone zone)
        {
            Zones.Remove(zone);
            if (SelectedZone == zone) SelectedZone = null;
            RebuildSpatialIndex();
            ZonesChanged?.Invoke();
        }

        // Alias so old callers using DeleteZone still compile
        public void DeleteZone(CraneZone zone) => RemoveZone(zone);

        public void RemoveAll()
        {
            Zones.Clear();
            SiteBoundary = null;
            SelectedZone = null;
            _spatialGrid = null;
            ZonesChanged?.Invoke();
        }

        public void MoveZone(int fromIndex, int toIndex)
        {
            if (fromIndex < 0 || fromIndex >= Zones.Count) return;
            if (toIndex < 0 || toIndex >= Zones.Count) return;
            Zones.Move(fromIndex, toIndex);
            ZonesChanged?.Invoke();
        }

        // ── Optimizer evaluation ─────────────────────────────────────────────

        public bool IsPositionAllowed(double x, double y, double h)
            => GetReasonCode(x, y, h) == ZoneEvaluationResult.Valid;

        /// <summary>
        /// Evaluates a candidate crane position in strict priority order (spec §9).
        /// 1. FAIL if outside site boundary.
        /// 2. FAIL if inside any Forbidden zone volume.
        /// 3. FAIL if explicit Allowed zones exist at this height but none contain position.
        /// 4. PASS — default green.
        /// </summary>
        public ZoneEvaluationResult GetReasonCode(double x, double y, double h)
        {
            // Step 1 — site boundary
            if (HasSiteBoundary && !SiteBoundary.PointInPolygon(x, y))
                return ZoneEvaluationResult.OutsideSite;

            var candidates = _spatialGrid != null
                ? _spatialGrid.GetCandidates(x, y)
                : Zones.AsEnumerable();

            // Step 2 — forbidden zones
            foreach (var zone in candidates)
                if (zone.ZoneType == ZoneType.Forbidden && zone.IsVisible)
                    if (zone.Contains(x, y, h))
                        return ZoneEvaluationResult.InForbiddenZone;

            // Step 3 — explicit allowed zones with height limits
            // BUG FIX: Added parentheses around (z.MinHeight > 0 || z.MaxHeight < 999).
            // Without them, operator precedence evaluated as:
            //   (z.ZoneType == Allowed && z.IsVisible && z.MinHeight > 0) || (z.MaxHeight < 999)
            // meaning ANY zone with MaxHeight < 999 (almost all of them) would pass,
            // making the allowed-zone check always false (no zone in allowedAtHeight
            // would be there but any zone with MaxHeight<999 would sneak past step 2).
            var allowedAtHeight = Zones
                .Where(z => z.ZoneType == ZoneType.Allowed && z.IsVisible
                         && (z.MinHeight > 0 || z.MaxHeight < 999))
                .ToList();

            if (allowedAtHeight.Count > 0)
            {
                bool inAny = allowedAtHeight.Any(z => z.Contains(x, y, h));
                if (!inAny) return ZoneEvaluationResult.OutsideAllowedZone;
            }

            // Step 4 — default green
            return ZoneEvaluationResult.Valid;
        }

        // ── Optimizer API methods (spec Section 9) ──────────────────────────
        public IEnumerable<CraneZone> GetValidZones()
            => Zones.Where(z => z.ZoneType == ZoneType.Allowed);

        public IEnumerable<CraneZone> GetForbiddenZones()
            => Zones.Where(z => z.ZoneType == ZoneType.Forbidden);

        public CraneZone GetSiteBoundary() => SiteBoundary;

        // ── Grid generation & filtering ──────────────────────────────────────

        public List<(double X, double Y)> FilterCandidates(
            List<(double X, double Y)> candidates, double craneHeight)
            => candidates.Where(c => IsPositionAllowed(c.X, c.Y, craneHeight)).ToList();

        public List<(double X, double Y)> GenerateCandidateGrid(double gridSpacing = 2.0)
        {
            if (!HasSiteBoundary) return new List<(double X, double Y)>();
            var verts = SiteBoundary.Vertices;
            double minX = verts.Min(v => v.X), maxX = verts.Max(v => v.X);
            double minY = verts.Min(v => v.Y), maxY = verts.Max(v => v.Y);
            var grid = new List<(double X, double Y)>();
            for (double x = minX; x <= maxX; x += gridSpacing)
                for (double y = minY; y <= maxY; y += gridSpacing)
                    if (SiteBoundary.PointInPolygon(x, y))
                        grid.Add((x, y));
            return grid;
        }

        public double TotalSiteArea => SiteBoundary?.FootprintArea ?? 0;

        public double ForbiddenGroundCoverage()
        {
            if (!HasSiteBoundary || TotalSiteArea == 0) return 0;
            double forbidden = Zones
                .Where(z => z.ZoneType == ZoneType.Forbidden && z.MinHeight == 0)
                .Sum(z => z.FootprintArea);
            return Math.Min(100.0, forbidden / TotalSiteArea * 100.0);
        }

        // ── Export ───────────────────────────────────────────────────────────

        public string ExportToJson()
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("{");
            sb.AppendLine("  \"siteBoundary\": " + ZoneToJson(SiteBoundary) + ",");
            sb.AppendLine("  \"zones\": [");
            for (int i = 0; i < Zones.Count; i++)
            {
                sb.Append("    " + ZoneToJson(Zones[i]));
                if (i < Zones.Count - 1) sb.Append(",");
                sb.AppendLine();
            }
            sb.AppendLine("  ]");
            sb.AppendLine("}");
            return sb.ToString();
        }

        public string ExportToCsv()
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("Id,Name,Type,MinHeight,MaxHeight,Area,VertexCount");
            foreach (var z in Zones)
                sb.AppendLine($"{z.Id},{z.Name},{z.ZoneType},{z.MinHeight},{z.MaxHeight},{z.FootprintArea:F2},{z.Vertices.Count}");
            return sb.ToString();
        }

        private string ZoneToJson(CraneZone z)
        {
            if (z == null) return "null";
            var verts = string.Join(", ", z.Vertices.Select(v => $"{{\"x\":{v.X:F3},\"y\":{v.Y:F3}}}"));
            return $"{{\"id\":\"{z.Id}\",\"name\":\"{z.Name}\",\"type\":\"{z.ZoneType}\"," +
                   $"\"minHeight\":{z.MinHeight},\"maxHeight\":{z.MaxHeight}," +
                   $"\"area\":{z.FootprintArea:F2},\"vertices\":[{verts}]}}";
        }

        private void RebuildSpatialIndex()
        {
            if (Zones.Count == 0) { _spatialGrid = null; return; }
            _spatialGrid = new SpatialGrid(Zones, cellSize: 10.0);
        }
    }

    internal class SpatialGrid
    {
        private readonly Dictionary<(int, int), List<CraneZone>> _cells = new();
        private readonly double _cellSize;

        public SpatialGrid(IEnumerable<CraneZone> zones, double cellSize)
        {
            _cellSize = cellSize;
            foreach (var zone in zones) IndexZone(zone);
        }

        private void IndexZone(CraneZone zone)
        {
            if (zone.Vertices.Count == 0) return;
            double minX = zone.Vertices.Min(v => v.X), maxX = zone.Vertices.Max(v => v.X);
            double minY = zone.Vertices.Min(v => v.Y), maxY = zone.Vertices.Max(v => v.Y);
            int x0 = (int)Math.Floor(minX / _cellSize), x1 = (int)Math.Floor(maxX / _cellSize);
            int y0 = (int)Math.Floor(minY / _cellSize), y1 = (int)Math.Floor(maxY / _cellSize);
            for (int cx = x0; cx <= x1; cx++)
                for (int cy = y0; cy <= y1; cy++)
                {
                    var key = (cx, cy);
                    if (!_cells.ContainsKey(key)) _cells[key] = new List<CraneZone>();
                    _cells[key].Add(zone);
                }
        }

        public IEnumerable<CraneZone> GetCandidates(double x, double y)
        {
            var key = ((int)Math.Floor(x / _cellSize), (int)Math.Floor(y / _cellSize));
            return _cells.TryGetValue(key, out var list) ? list : Enumerable.Empty<CraneZone>();
        }
    }
}