using System.Windows.Media;

namespace CraneOptimizer.UI.Core.Models
{
    public class SceneDxf
    {
        public Guid Id { get; set; }

        // Raw geometry (Polyline / Line / Arc...)
        public object Geometry { get; set; } = null!;

        // Metadata
        public string Layer { get; set; } = string.Empty;

        // UI State
        public bool IsSelected { get; set; }
        public bool IsVisible { get; set; } = true;

        // Styling
        public Color Color { get; set; } = Colors.Gray;
        public double Thickness { get; set; } = 1;
    }
}
