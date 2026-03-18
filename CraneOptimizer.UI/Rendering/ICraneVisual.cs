using System.Windows.Media.Media3D;
using WPF_IFC_test1.Models;

namespace WPF_IFC_test1.Utilities
{
    public interface ICraneVisual
    {
        CraneParametersBase Parameters { get; }
        void Rebuild();
        void Dispose();
    }
}