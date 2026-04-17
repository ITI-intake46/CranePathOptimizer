using Domian = CraneOptimizer.Domain.Common;
using CraneOptimizer.Domain.GreenAreaEntities;
using DomainPoint = CraneOptimizer.Domain.Common.DomainPoint3D;

namespace CraneOptimizer.UI.Core.Services
{
    /// <summary>
    /// Handles user drawing logic (state + polygon creation).
    /// Keeps UI thin.
    /// </summary>
    public class DrawingService
    {
        private readonly List<DomainPoint> _points = new();

        public void Start() => _points.Clear();
        public bool TryAddPoint(DomainPoint point)
        {
            if (_points.Count < 2)
            {
                _points.Add(point);
                return true;
            }

            var newStart = _points.Last();
            var newEnd = point;

            for (int i = 0; i < _points.Count - 2; i++)
            {
                var a1 = _points[i];
                var a2 = _points[i + 1];

                if (DoLinesIntersect(a1, a2, newStart, newEnd))
                    return false;
            }

            _points.Add(point);
            return true;
        }
        public List<DomainPoint> GetPoints() => _points;

        public void AddPoint(DomainPoint point) => _points.Add(point);
        public CranePlacementZone? BuildZone()
        {
            if (_points.Count < 3) return null;
            return new CranePlacementZone
            {
                Vertices = _points
                    .Select(p => new DomainPoint(p.X, p.Y, p.Z)) // ✅ واضح
                    .ToList()
            };
        }
        private bool DoLinesIntersect(
                        DomainPoint p1, DomainPoint p2,
                        DomainPoint p3, DomainPoint p4)
        {
            double d1 = Direction(p3, p4, p1);
            double d2 = Direction(p3, p4, p2);
            double d3 = Direction(p1, p2, p3);
            double d4 = Direction(p1, p2, p4);

            if ((d1 > 0 && d2 < 0 || d1 < 0 && d2 > 0) &&
                (d3 > 0 && d4 < 0 || d3 < 0 && d4 > 0))
                return true;

            return false;
        }
        private double Direction(DomainPoint a, DomainPoint b, DomainPoint c)
        {
            return (c.X - a.X) * (b.Y - a.Y) - (c.Y - a.Y) * (b.X - a.X);
        }
    }
}