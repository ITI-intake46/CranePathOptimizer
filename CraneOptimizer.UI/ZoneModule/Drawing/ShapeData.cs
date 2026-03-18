using System.Collections.Generic;
using System.Windows.Media.Media3D;
using WPF_IFC_test1.ZoneModule.UI;

namespace WPF_IFC_test1.ZoneModule.Drawing
{
    // ══════════════════════════════════════════════════════════════════════════
    //  Shape data objects — emitted by ShapeInputController when the user
    //  confirms a shape placement.  These are passed to the zone drawing
    //  system which converts them to actual zone geometry.
    // ══════════════════════════════════════════════════════════════════════════

    /// <summary>Base class for all confirmed shape results.</summary>
    public abstract class ShapeData
    {
        /// <summary>The zone type this shape is being drawn for.</summary>
        public ZonePreviewType ZoneType { get; set; }
    }

    /// <summary>A confirmed axis-aligned rectangle placement.</summary>
    public class RectangleShapeData : ShapeData
    {
        /// <summary>World-space centre of the rectangle (Z = ground plane).</summary>
        public Point3D Center { get; set; }

        /// <summary>Width in metres (X axis).</summary>
        public double Width { get; set; }

        /// <summary>Height in metres (Y axis).</summary>
        public double Height { get; set; }

        /// <summary>
        /// Rotation in degrees clockwise from north (Y+).
        /// 0 = axis-aligned.  Set when user uses Method 3 (pick width side).
        /// </summary>
        public double Rotation { get; set; }

        /// <summary>
        /// The four corners in world space, in order:
        /// top-left, top-right, bottom-right, bottom-left.
        /// Computed by the controller after confirmation.
        /// </summary>
        public List<Point3D> Corners { get; set; } = new();
    }

    /// <summary>A confirmed circle placement.</summary>
    public class CircleShapeData : ShapeData
    {
        /// <summary>World-space centre of the circle.</summary>
        public Point3D Center { get; set; }

        /// <summary>Radius in metres.</summary>
        public double Radius { get; set; }
    }

    /// <summary>A confirmed arc placement.</summary>
    public class ArcShapeData : ShapeData
    {
        /// <summary>World-space centre of the arc's circle.</summary>
        public Point3D Center { get; set; }

        /// <summary>Radius in metres.</summary>
        public double Radius { get; set; }

        /// <summary>Start angle in degrees (0 = east / positive X).</summary>
        public double StartAngle { get; set; }

        /// <summary>End angle in degrees, clockwise from StartAngle.</summary>
        public double EndAngle { get; set; }

        /// <summary>True = minor arc; False = major arc.</summary>
        public bool IsMinorArc { get; set; }
    }

    /// <summary>A confirmed zone from picked IFC elements.</summary>
    public class PickObjectsShapeData : ShapeData
    {
        /// <summary>Bounding box vertices (4 corners on ground plane).</summary>
        public List<Point3D> Vertices { get; set; } = new();

        /// <summary>Number of elements that were selected.</summary>
        public int PickedCount { get; set; }
    }
}