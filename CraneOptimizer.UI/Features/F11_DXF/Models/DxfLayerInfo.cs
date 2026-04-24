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
    /// Display information for a single DXF layer.
    /// Bound to the Layers ListBox in DxfAlignmentDialog via DxfAlignmentViewModel.
    /// </summary>
    public class DxfLayerInfo : INotifyPropertyChanged
    {
        public string Name { get; init; } = "";
        /// <summary>Web-hex colour string, e.g. "#FF4500".</summary>
        public string ColorHex { get; init; } = "#A0A0A0";
        public int EntityCount { get; init; }

        private bool _isVisible = true;
        public bool IsVisible
        {
            get => _isVisible;
            set { _isVisible = value; OnPropertyChanged(); }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
