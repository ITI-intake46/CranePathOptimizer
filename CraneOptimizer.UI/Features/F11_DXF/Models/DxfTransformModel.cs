using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace CraneOptimizer.UI.Features.F11_DXF.Models
{
    /// <summary>
  /// Serialisable snapshot of how a DXF coordinate system maps onto the
  /// IFC / world coordinate system.  All offset values are in metres (SI).
  ///
  /// Transform order applied to every DXF vertex:
  ///   1. Scale about the DXF origin
  ///   2. Rotate clockwise about Z  (degrees)
  ///   3. Translate by (OffsetX, OffsetY)
  /// </summary>
    public class DxfTransformModel : INotifyPropertyChanged
    {
        // ── Transform parameters ──────────────────────────────────────────────

        private double _offsetX;
        public double OffsetX
        {
            get => _offsetX;
            set
            {
                if (_offsetX != value)
                {
                    _offsetX = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(IsDirty));
                }
            }
        }

        private double _offsetY;
        public double OffsetY
        {
            get => _offsetY;
            set
            {
                if (_offsetY != value)
                {
                    _offsetY = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(IsDirty));
                }
            }
        }

        private double _scale = 1.0;
        public double Scale
        {
            get => _scale;
            set
            {
                double clamped = Math.Max(0.0001, Math.Min(10000.0, value));
                if (Math.Abs(_scale - clamped) > 1e-9)
                {
                    _scale = clamped;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(IsDirty));
                }
            }
        }

        private double _rotationDegrees;
        /// <summary>Clockwise rotation in degrees, normalised to [0, 360).</summary>
        public double RotationDegrees
        {
            get => _rotationDegrees;
            set
            {
                double normalised = ((value % 360) + 360) % 360;
                if (Math.Abs(_rotationDegrees - normalised) > 1e-9)
                {
                    _rotationDegrees = normalised;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(IsDirty));
                }
            }
        }

        // ── Reference-point pairing (point-match method) ──────────────────────

        private double _dxfRefX;
        public double DxfRefX { get => _dxfRefX; set { _dxfRefX = value; OnPropertyChanged(); } }

        private double _dxfRefY;
        public double DxfRefY { get => _dxfRefY; set { _dxfRefY = value; OnPropertyChanged(); } }

        private double _ifcRefX;
        public double IfcRefX { get => _ifcRefX; set { _ifcRefX = value; OnPropertyChanged(); } }

        private double _ifcRefY;
        public double IfcRefY { get => _ifcRefY; set { _ifcRefY = value; OnPropertyChanged(); } }

        // ── Visual options ────────────────────────────────────────────────────

        private double _opacity = 0.70;
        /// <summary>DXF overlay opacity in the 3D viewport (0.1 – 1.0).</summary>
        public double Opacity
        {
            get => _opacity;
            set
            {
                double v = Math.Clamp(value, 0.1, 1.0);
                if (Math.Abs(_opacity - v) > 1e-9) { _opacity = v; OnPropertyChanged(); }
            }
        }

        private bool _showGrid = true;
        public bool ShowGrid
        {
            get => _showGrid;
            set { _showGrid = value; OnPropertyChanged(); }
        }

        private bool _snapToGrid;
        public bool SnapToGrid
        {
            get => _snapToGrid;
            set { _snapToGrid = value; OnPropertyChanged(); }
        }

        private double _gridSpacing = 5.0;
        /// <summary>Grid spacing in metres for the 2-D canvas viewer.</summary>
        public double GridSpacing
        {
            get => _gridSpacing;
            set { _gridSpacing = Math.Max(0.1, value); OnPropertyChanged(); }
        }

        // ── Computed helpers ──────────────────────────────────────────────────

        /// <summary>True when any parameter differs from identity.</summary>
        public bool IsDirty =>
            Math.Abs(OffsetX) > 1e-9 ||
            Math.Abs(OffsetY) > 1e-9 ||
            Math.Abs(Scale - 1.0) > 1e-9 ||
            Math.Abs(RotationDegrees) > 1e-9;

        /// <summary>Rotation in radians (negate when passing to Math.Cos/Sin for CW→CCW).</summary>
        public double RotationRadians => RotationDegrees * Math.PI / 180.0;

        // ── Serialisation helpers ─────────────────────────────────────────────

        /// <summary>Serialise to [tx, ty, scale, rot] for database storage.</summary>
        public double[] ToArray() => new[] { OffsetX, OffsetY, Scale, RotationDegrees };

        /// <summary>Restore from a flat double array produced by <see cref="ToArray"/>.</summary>
        public static DxfTransformModel FromArray(double[] arr)
        {
            if (arr == null || arr.Length < 4)
                return new DxfTransformModel();

            return new DxfTransformModel
            {
                OffsetX = arr[0],
                OffsetY = arr[1],
                Scale = arr[2],
                RotationDegrees = arr[3]
            };
        }

        public DxfTransformModel Clone() => new DxfTransformModel
        {
            OffsetX = OffsetX,
            OffsetY = OffsetY,
            Scale = Scale,
            RotationDegrees = RotationDegrees,
            DxfRefX = DxfRefX,
            DxfRefY = DxfRefY,
            IfcRefX = IfcRefX,
            IfcRefY = IfcRefY,
            Opacity = Opacity,
            ShowGrid = ShowGrid,
            SnapToGrid = SnapToGrid,
            GridSpacing = GridSpacing
        };

        public void Reset()
        {
            OffsetX = OffsetY = DxfRefX = DxfRefY = IfcRefX = IfcRefY = 0;
            Scale = 1.0;
            RotationDegrees = 0;
        }

        // ── INotifyPropertyChanged ────────────────────────────────────────────

        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
