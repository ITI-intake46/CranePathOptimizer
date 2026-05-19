using System;
using System.Collections.Generic;
using CraneOptimizer.Domain.Units;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace CraneOptimizer.UI.Features.F11_DXF.Models
{
    /// <summary>
    /// Serialisable snapshot of how a DXF coordinate system maps onto the
  /// IFC / world coordinate system.  Offset values are stored in Helix scene
  /// units, which this UI uses as normalized meters.
  ///
  /// Transform order applied to every DXF vertex:
  ///   1. Convert from the selected DXF unit to scene meters
  ///   2. Apply user scale about the DXF origin
  ///   3. Rotate clockwise about Z (degrees)
  ///   4. Translate by the final offset, including the user global offset
  /// </summary>
    public class DxfTransformModel : INotifyPropertyChanged
    {
        public const int DefaultUnitIndex = -1;
        public static readonly string[] UnitLabels = { "mm", "cm", "m", "in", "ft" };
        public static readonly string[] DxfUnitLabels = { "Default", "mm", "cm", "m", "in", "ft" };

        public static double UnitScaleToSceneMeters(int unitIndex) => ClampUnitIndex(unitIndex) switch
        {
            0 => UnitSystem.ToMeters(1.0, LengthUnit.Millimeter),
            1 => UnitSystem.ToMeters(1.0, LengthUnit.Centimeter),
            2 => UnitSystem.ToMeters(1.0, LengthUnit.Meter),
            3 => UnitSystem.ToMeters(1.0, LengthUnit.Inch),
            4 => UnitSystem.ToMeters(1.0, LengthUnit.Foot),
            _ => 1.0
        };

        public static int ClampUnitIndex(int unitIndex)
            => unitIndex < 0 || unitIndex >= UnitLabels.Length ? 0 : unitIndex;

        public static int ClampDxfUnitIndex(int unitIndex)
            => unitIndex == DefaultUnitIndex ? DefaultUnitIndex : ClampUnitIndex(unitIndex);

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
                    OnPropertyChanged(nameof(EffectiveScale));
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

        private bool _showText = true;
        public bool ShowText
        {
            get => _showText;
            set
            {
                if (_showText == value) return;
                _showText = value;
                OnPropertyChanged();
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
            Math.Abs(GlobalOffsetX) > 1e-9 ||
            Math.Abs(GlobalOffsetY) > 1e-9 ||
            Math.Abs(Scale - 1.0) > 1e-9 ||
            Math.Abs(RotationDegrees) > 1e-9 ||
            DxfUnitIndex != DefaultUnitIndex ||
            GlobalOffsetUnitIndex != 0;

        /// <summary>Rotation in radians (negate when passing to Math.Cos/Sin for CW→CCW).</summary>
        public double RotationRadians => RotationDegrees * Math.PI / 180.0;

        public double EffectiveScale => Scale * UnitScaleToSceneMeters(ResolvedDxfUnitIndex);

        // -- Unit and post-alignment offset parameters -------------------------

        private int _dxfUnitIndex = DefaultUnitIndex;
        public int DxfUnitIndex
        {
            get => _dxfUnitIndex;
            set
            {
                int clamped = ClampDxfUnitIndex(value);
                if (_dxfUnitIndex == clamped) return;
                _dxfUnitIndex = clamped;
                OnPropertyChanged();
                OnPropertyChanged(nameof(EffectiveScale));
                OnPropertyChanged(nameof(ResolvedDxfUnitIndex));
                OnPropertyChanged(nameof(IsDirty));
            }
        }

        private int _dxfDocumentUnitIndex;
        public int DxfDocumentUnitIndex
        {
            get => _dxfDocumentUnitIndex;
            set
            {
                int clamped = ClampUnitIndex(value);
                if (_dxfDocumentUnitIndex == clamped) return;
                _dxfDocumentUnitIndex = clamped;
                OnPropertyChanged();
                OnPropertyChanged(nameof(ResolvedDxfUnitIndex));
                OnPropertyChanged(nameof(EffectiveScale));
            }
        }

        public int ResolvedDxfUnitIndex =>
            DxfUnitIndex == DefaultUnitIndex ? DxfDocumentUnitIndex : DxfUnitIndex;

        private int _globalOffsetUnitIndex;
        public int GlobalOffsetUnitIndex
        {
            get => _globalOffsetUnitIndex;
            set
            {
                int clamped = ClampUnitIndex(value);
                if (_globalOffsetUnitIndex == clamped) return;
                _globalOffsetUnitIndex = clamped;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsDirty));
            }
        }

        private double _globalOffsetX;
        public double GlobalOffsetX
        {
            get => _globalOffsetX;
            set
            {
                if (Math.Abs(_globalOffsetX - value) <= 1e-9) return;
                _globalOffsetX = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsDirty));
            }
        }

        private double _globalOffsetY;
        public double GlobalOffsetY
        {
            get => _globalOffsetY;
            set
            {
                if (Math.Abs(_globalOffsetY - value) <= 1e-9) return;
                _globalOffsetY = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsDirty));
            }
        }

        // ── Serialisation helpers ─────────────────────────────────────────────

        /// <summary>Serialise transform, refs, units, and post-alignment global offset.</summary>
        public double[] ToArray() => new[]
        {
            OffsetX,
            OffsetY,
            Scale,
            RotationDegrees,
            DxfRefX,
            DxfRefY,
            IfcRefX,
            IfcRefY,
            GlobalOffsetX,
            GlobalOffsetY,
            DxfUnitIndex,
            GlobalOffsetUnitIndex,
            ShowText ? 1.0 : 0.0
        };

        /// <summary>Restore from a flat double array produced by <see cref="ToArray"/>.</summary>
        public static DxfTransformModel FromArray(double[] arr)
        {
            if (arr == null || arr.Length < 4)
                return new DxfTransformModel();

            var model = new DxfTransformModel
            {
                OffsetX = arr[0],
                OffsetY = arr[1],
                Scale = arr[2],
                RotationDegrees = arr[3]
            };

            if (arr.Length >= 8)
            {
                model.DxfRefX = arr[4];
                model.DxfRefY = arr[5];
                model.IfcRefX = arr[6];
                model.IfcRefY = arr[7];
            }

            if (arr.Length >= 12)
            {
                model.GlobalOffsetX = arr[8];
                model.GlobalOffsetY = arr[9];
                model.DxfUnitIndex = (int)Math.Round(arr[10]);
                model.GlobalOffsetUnitIndex = (int)Math.Round(arr[11]);
            }

            if (arr.Length >= 13)
                model.ShowText = arr[12] >= 0.5;

            return model;
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
            GlobalOffsetX = GlobalOffsetX,
            GlobalOffsetY = GlobalOffsetY,
            DxfUnitIndex = DxfUnitIndex,
            DxfDocumentUnitIndex = DxfDocumentUnitIndex,
            GlobalOffsetUnitIndex = GlobalOffsetUnitIndex,
            Opacity = Opacity,
            ShowText = ShowText,
            ShowGrid = ShowGrid,
            SnapToGrid = SnapToGrid,
            GridSpacing = GridSpacing
        };

        public void Reset()
        {
            OffsetX = OffsetY = DxfRefX = DxfRefY = IfcRefX = IfcRefY = 0;
            GlobalOffsetX = GlobalOffsetY = 0;
            DxfUnitIndex = DefaultUnitIndex;
            GlobalOffsetUnitIndex = 0;
            ShowText = true;
            Scale = 1.0;
            RotationDegrees = 0;
        }

        // ── INotifyPropertyChanged ────────────────────────────────────────────

        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
