using CraneOptimizer.UI.Features.F11_DXF.Models;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CraneOptimizer.UI.Features.F11_DXF.Services
{
    /// <summary>
    /// Stateless Application-layer service that owns all alignment maths for
    /// the DXF-to-IFC overlay.
    ///
    /// Rules:
    ///  • No WPF / Windows dependencies — pure C#.
    ///  • No state: every method receives and returns explicit arguments.
    ///  • Works only with Models (DxfTransformModel, DxfBounds).
    /// </summary>
    public sealed class DxfAlignmentService
    {
        // =====================================================================
        //  Manual-transform application
        // =====================================================================

        /// <summary>
        /// Parses the four text-box values from the UI and, if all are valid,
        /// writes them into <paramref name="model"/>.
        /// </summary>
        /// <returns>true if all four values are valid and the model was updated.</returns>
        public bool TryApplyManual(
            DxfTransformModel model,
            string offsetXText,
            string offsetYText,
            string scaleText,
            string rotationText,
            out string error)
        {
            if (model == null) throw new ArgumentNullException(nameof(model));

            bool ok = true;
            error = "";

            if (!TryParseDouble(offsetXText, out double offsetX))
            { error += "OffsetX must be a number.\n"; ok = false; }

            if (!TryParseDouble(offsetYText, out double offsetY))
            { error += "OffsetY must be a number.\n"; ok = false; }

            if (!TryParseDouble(scaleText, out double scale) || scale <= 0)
            { error += "Scale must be a positive number.\n"; ok = false; }

            if (!TryParseDouble(rotationText, out double rotation))
            { error += "Rotation must be a number (degrees).\n"; ok = false; }

            if (!ok) { error = error.TrimEnd('\n'); return false; }

            model.OffsetX = offsetX;
            model.OffsetY = offsetY;
            model.Scale = scale;
            model.RotationDegrees = rotation;

            error = "";
            return true;
        }

        // =====================================================================
        //  Point-match
        // =====================================================================

        /// <summary>
        /// Computes and applies a pure-translation offset so that the DXF
        /// reference point maps onto the IFC world reference point.
        /// Scale and rotation are preserved.
        /// </summary>
        public void ApplyPointMatch(
            DxfTransformModel model,
            (double X, double Y) dxfRef,
            (double X, double Y) ifcRef)
        {
            if (model == null) throw new ArgumentNullException(nameof(model));

            double s = model.Scale;
            double rad = -model.RotationRadians;
            double cos = Math.Cos(rad);
            double sin = Math.Sin(rad);

            double worldX = s * (cos * dxfRef.X - sin * dxfRef.Y);
            double worldY = s * (sin * dxfRef.X + cos * dxfRef.Y);

            model.OffsetX = ifcRef.X - worldX;
            model.OffsetY = ifcRef.Y - worldY;
        }

        // =====================================================================
        //  Auto-fit helpers
        // =====================================================================

        /// <summary>
        /// Suggests a scale factor that fits the DXF drawing inside a square
        /// canvas of <paramref name="targetSizeMetres"/> metres on each side.
        /// </summary>
        public double SuggestScale(DxfBounds bounds, double targetSizeMetres)
        {
            if (bounds == null || bounds.IsEmpty || targetSizeMetres <= 0)
                return 1.0;

            double maxExtent = Math.Max(bounds.Width, bounds.Height);
            return maxExtent <= 0 ? 1.0 : targetSizeMetres / maxExtent;
        }

        /// <summary>
        /// Suggests translation offsets (metres) so the scaled DXF is centred
        /// on the given world origin.
        /// </summary>
        public (double Tx, double Ty) SuggestCenterOffset(
            DxfBounds bounds, double scale, double originX, double originY)
        {
            if (bounds == null || bounds.IsEmpty)
                return (originX, originY);

            double scaledCx = bounds.CenterX * scale;
            double scaledCy = bounds.CenterY * scale;
            return (originX - scaledCx, originY - scaledCy);
        }

        // =====================================================================
        //  Undo / snapshot
        // =====================================================================

        /// <summary>Creates an opaque snapshot for undo. Treat as a black-box token.</summary>
        public object Snapshot(DxfTransformModel model)
        {
            if (model == null) throw new ArgumentNullException(nameof(model));
            return model.Clone();
        }

        /// <summary>Restores a transform model to the state captured by <see cref="Snapshot"/>.</summary>
        public void Restore(DxfTransformModel model, object snapshot)
        {
            if (model == null) throw new ArgumentNullException(nameof(model));
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));

            if (snapshot is not DxfTransformModel saved)
                throw new ArgumentException(
                    "Snapshot was not produced by DxfAlignmentService.Snapshot.", nameof(snapshot));

            model.OffsetX = saved.OffsetX;
            model.OffsetY = saved.OffsetY;
            model.Scale = saved.Scale;
            model.RotationDegrees = saved.RotationDegrees;
            model.DxfRefX = saved.DxfRefX;
            model.DxfRefY = saved.DxfRefY;
            model.IfcRefX = saved.IfcRefX;
            model.IfcRefY = saved.IfcRefY;
            model.Opacity = saved.Opacity;
            model.ShowGrid = saved.ShowGrid;
            model.SnapToGrid = saved.SnapToGrid;
            model.GridSpacing = saved.GridSpacing;
        }

        // =====================================================================
        //  Private helpers
        // =====================================================================

        private static bool TryParseDouble(string text, out double value)
            => double.TryParse(
                text?.Trim(),
                NumberStyles.Float | NumberStyles.AllowLeadingSign,
                CultureInfo.InvariantCulture,
                out value);
    }
}
