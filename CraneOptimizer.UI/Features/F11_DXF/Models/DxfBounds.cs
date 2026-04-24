using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CraneOptimizer.UI.Features.F11_DXF.Models
{
    /// <summary>
    /// Axis-aligned bounding rectangle of all DXF entities in DXF world coordinates.
    /// Used by DxfAlignmentService and DxfCanvasRenderer.
    /// CANONICAL LOCATION — do NOT copy or redefine this class elsewhere.
    /// </summary>
    public sealed class DxfBounds
    {
        // ── Raw bounds ────────────────────────────────────────────────────────

        public double MinX { get; set; } = double.MaxValue;
        public double MaxX { get; set; } = double.MinValue;
        public double MinY { get; set; } = double.MaxValue;
        public double MaxY { get; set; } = double.MinValue;

        // ── Derived properties ────────────────────────────────────────────────

        /// <summary>True when no geometry has been ingested yet (sentinel values).</summary>
        public bool IsEmpty => MinX >= MaxX || MinY >= MaxY;

        public double Width => IsEmpty ? 0 : MaxX - MinX;
        public double Height => IsEmpty ? 0 : MaxY - MinY;
        public double CenterX => (MinX + MaxX) * 0.5;
        public double CenterY => (MinY + MaxY) * 0.5;

        // ── Factory ───────────────────────────────────────────────────────────

        public static DxfBounds Empty => new DxfBounds();

        // ── Mutation helpers ──────────────────────────────────────────────────

        public void Expand(double x, double y)
        {
            if (x < MinX) MinX = x;
            if (x > MaxX) MaxX = x;
            if (y < MinY) MinY = y;
            if (y > MaxY) MaxY = y;
        }

        public DxfBounds WithPadding(double pad) => new DxfBounds
        {
            MinX = MinX - pad,
            MaxX = MaxX + pad,
            MinY = MinY - pad,
            MaxY = MaxY + pad
        };

        public override string ToString() =>
            IsEmpty
                ? "DxfBounds(Empty)"
                : $"DxfBounds(X:{MinX:F2}–{MaxX:F2}  Y:{MinY:F2}–{MaxY:F2}  W:{Width:F2} H:{Height:F2})";
    }
}
