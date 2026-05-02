using System;
using System.Collections.Generic;
using System.Windows.Media.Media3D;

namespace CraneOptimizer.UI.Features.F15_ObjectSnap.Services
{
    /// <summary>
    /// Extracts snap-eligible vertices from IFC structural elements.
    ///
    /// Strategy: only structural elements (columns, beams, slabs, walls) are processed.
    /// Vertices are de-duplicated within a 1 cm proximity grid to keep point counts manageable
    /// even for large models (typically stays well under 50 000 points).
    ///
    /// This class works with the already-parsed <c>PrecastElement</c> positions that the
    /// scene service already holds — no new IFC file parse or <c>CreateContext()</c> call.
    /// </summary>
    public static class IfcSnapExtractor
    {
        private const double DedupCellSize = 0.01; // 1 cm in model metres

        /// <summary>
        /// Builds a de-duplicated list of snap points from a set of world-space bounding-box
        /// corners for structural elements (columns, beams, slabs).
        ///
        /// Callers pass the element positions already available in the scene; this keeps the
        /// extractor free of any Xbim / IFC dependency so it compiles without the heavy SDK
        /// in the UI layer.
        /// </summary>
        /// <param name="elementOrigins">
        /// World-space origin of each structural element (one per IFC element).
        /// Typically <c>PrecastElement.Position</c> or similar.
        /// </param>
        /// <param name="boundingBoxCorners">
        /// Optional per-element bounding-box corner offsets relative to the origin.
        /// Pass an empty enumerable or null to use only the origin points.
        /// </param>
        public static List<Point3D> Extract(
            IEnumerable<Point3D> elementOrigins,
            IEnumerable<(Point3D Min, Point3D Max)>? boundingBoxCorners = null)
        {
            var raw = new List<Point3D>();

            // Keep actual Z — elements render in 3D, snap must match screen positions
            foreach (var origin in elementOrigins)
                raw.Add(origin);

            if (boundingBoxCorners != null)
            {
                foreach (var (min, max) in boundingBoxCorners)
                {
                    // 8 corners of the 3D bounding box
                    raw.Add(new Point3D(min.X, min.Y, min.Z));
                    raw.Add(new Point3D(max.X, min.Y, min.Z));
                    raw.Add(new Point3D(max.X, max.Y, min.Z));
                    raw.Add(new Point3D(min.X, max.Y, min.Z));
                    raw.Add(new Point3D(min.X, min.Y, max.Z));
                    raw.Add(new Point3D(max.X, min.Y, max.Z));
                    raw.Add(new Point3D(max.X, max.Y, max.Z));
                    raw.Add(new Point3D(min.X, max.Y, max.Z));
                }
            }

            return Deduplicate(raw);
        }

        // ─── De-duplication via spatial grid ──────────────────────────────────
        private static List<Point3D> Deduplicate(List<Point3D> points)
        {
            var seen = new HashSet<(long, long, long)>();
            var result = new List<Point3D>(points.Count);

            foreach (var p in points)
            {
                long gx = (long)Math.Round(p.X / DedupCellSize);
                long gy = (long)Math.Round(p.Y / DedupCellSize);
                long gz = (long)Math.Round(p.Z / DedupCellSize);
                if (seen.Add((gx, gy, gz)))
                    result.Add(p);
            }

            return result;
        }
    }
}
