// FILE: ZoneModule/Models/CraneZone.cs
// CHANGES vs original:
//   + IsSiteBoundary property (spec Section 11.1)
//   + Color property (spec Section 11.1)
//   + DisplayLabel now respects IsSiteBoundary

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Windows.Media;
using System.Windows.Media.Media3D;

namespace WPF_IFC_test1.ZoneModule.Models
{
    public enum ZoneType { Allowed, Forbidden, SiteBoundary }

    public class CraneZone : INotifyPropertyChanged
    {
        private string _name;
        private ZoneType _zoneType;
        private double _minHeight;
        private double _maxHeight;
        private bool _isVisible = true;
        private string _notes;
        private Color? _colorOverride;

        public string Id { get; set; } = Guid.NewGuid().ToString();

        public string Name
        {
            get => _name;
            set { _name = value; OnPropertyChanged(nameof(Name)); OnPropertyChanged(nameof(DisplayLabel)); }
        }

        public ZoneType ZoneType
        {
            get => _zoneType;
            set { _zoneType = value; OnPropertyChanged(nameof(ZoneType)); OnPropertyChanged(nameof(DisplayLabel)); OnPropertyChanged(nameof(IsSiteBoundary)); }
        }

        // ── NEW: spec Section 11.1 explicitly lists IsSiteBoundary ──────────
        /// <summary>True when this zone is the special outer site boundary polygon.</summary>
        public bool IsSiteBoundary => ZoneType == ZoneType.SiteBoundary;

        // ── NEW: spec Section 11.1 lists Color (derived from ZoneType, overridable) ──
        /// <summary>Display color — derived from ZoneType by default, overridable per-zone.</summary>
        public Color Color
        {
            get => _colorOverride ?? DefaultColor;
            set { _colorOverride = value; OnPropertyChanged(nameof(Color)); }
        }

        private Color DefaultColor => ZoneType switch
        {
            ZoneType.Allowed => Color.FromArgb(100, 39, 174, 96),
            ZoneType.Forbidden => Color.FromArgb(100, 231, 76, 60),
            ZoneType.SiteBoundary => Color.FromArgb(60, 0, 122, 204),
            _ => Color.FromArgb(80, 150, 150, 150)
        };

        public void ResetColor() { _colorOverride = null; OnPropertyChanged(nameof(Color)); }

        public List<Point3D> Vertices { get; set; } = new List<Point3D>();

        public double MinHeight
        {
            get => _minHeight;
            set { _minHeight = value; OnPropertyChanged(nameof(MinHeight)); OnPropertyChanged(nameof(DisplayLabel)); }
        }

        public double MaxHeight
        {
            get => _maxHeight;
            set { _maxHeight = value; OnPropertyChanged(nameof(MaxHeight)); OnPropertyChanged(nameof(DisplayLabel)); }
        }

        public bool IsVisible
        {
            get => _isVisible;
            set { _isVisible = value; OnPropertyChanged(nameof(IsVisible)); }
        }

        public string Notes
        {
            get => _notes;
            set { _notes = value; OnPropertyChanged(nameof(Notes)); }
        }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public string DisplayLabel
        {
            get
            {
                if (IsSiteBoundary) return "⬛ Site Boundary";
                string prefix = ZoneType == ZoneType.Allowed ? "🟩" : "🟥";
                string label = string.IsNullOrEmpty(Name)
                    ? $"{ZoneType} Zone"
                    : Name;
                return $"{prefix} {label} ({MinHeight:F0}m – {MaxHeight:F0}m)";
            }
        }

        public double FootprintArea => CalculateArea();

        /// <summary>Human-readable height range shown in zone list and hover tooltip.</summary>
        public string HeightRange
        {
            get
            {
                if (MaxHeight >= 999)
                    return MinHeight <= 0 ? "Ground → unlimited" : $"{MinHeight:F0}m → unlimited";
                return MinHeight <= 0 ? $"Ground → {MaxHeight:F0}m" : $"{MinHeight:F0}m → {MaxHeight:F0}m";
            }
        }

        private double CalculateArea()
        {
            if (Vertices.Count < 3) return 0;
            double area = 0;
            int n = Vertices.Count;
            for (int i = 0; i < n; i++)
            {
                var a = Vertices[i];
                var b = Vertices[(i + 1) % n];
                area += a.X * b.Y - b.X * a.Y;
            }
            return Math.Abs(area) / 2.0;
        }

        public bool Contains(double x, double y, double h)
        {
            if (h < MinHeight || h > MaxHeight) return false;
            return PointInPolygon(x, y);
        }

        public bool PointInPolygon(double x, double y)
        {
            bool inside = false;
            int n = Vertices.Count;
            for (int i = 0, j = n - 1; i < n; j = i++)
            {
                var vi = Vertices[i];
                var vj = Vertices[j];
                if (((vi.Y > y) != (vj.Y > y)) &&
                    (x < (vj.X - vi.X) * (y - vi.Y) / (vj.Y - vi.Y) + vi.X))
                    inside = !inside;
            }
            return inside;
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string name)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    public class DimensionAnnotation
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public DimensionType Type { get; set; }
        public Point3D StartPoint { get; set; }
        public Point3D EndPoint { get; set; }
        public double OffsetDistance { get; set; }
        public bool IsVisible { get; set; } = true;
        public string Label { get; set; } = "";
        public double MeasuredDistance => (EndPoint - StartPoint).Length;
    }

    public enum DimensionType { Linear, Aligned, Radius }
}