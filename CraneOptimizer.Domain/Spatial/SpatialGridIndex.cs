using CraneOptimizer.Domain.Common;

namespace CraneOptimizer.Domain.Spatial;

/// <summary>
/// Immutable 2-D grid index over a flat list of points.
/// Thread-safe for concurrent reads after construction.
/// </summary>
public sealed class SpatialGridIndex
{
    private readonly Dictionary<(int x, int y), List<int>> _cells = new();
    private readonly double _cellSize;

    /// <summary>
    /// Element centroids — index must match ElementWeights.
    /// Grid cell size in project units (metres). Use 10.0 as default.
    /// </summary>
    public SpatialGridIndex(IReadOnlyList<DomainPoint3D> points, double cellSize = 10.0)
    {
        _cellSize = cellSize;
        for (int i = 0; i < points.Count; i++)
        {
            var key = Cell(points[i]);
            if (!_cells.TryGetValue(key, out var list))
                _cells[key] = list = new List<int>();
            list.Add(i);
        }
    }

    /// <summary>
    /// Returns element indices whose cell overlaps a circle of <radius>
    /// centred at <center>. May return false positives — callers must
    /// re-check exact distance if needed.
    /// </summary>
    public IEnumerable<int> Query(DomainPoint3D center, double radius)
    {
        int r = (int)(radius / _cellSize) + 1;
        var (cx, cy) = Cell(center);
        for (int dx = -r; dx <= r; dx++)
        for (int dy = -r; dy <= r; dy++)
            if (_cells.TryGetValue((cx + dx, cy + dy), out var list))
                foreach (var i in list)
                    yield return i;
    }

    private (int x, int y) Cell(DomainPoint3D p)
        => ((int)Math.Floor(p.X / _cellSize), (int)Math.Floor(p.Y / _cellSize));
}
