using CraneOptimizer.UI.Core.Interfaces;
using HelixToolkit.Wpf;
using System.Windows.Media;
using System.Windows.Media.Media3D;

namespace CraneOptimizer.UI.Features.F8_GAOptimizer
{
    public class BestPointVisualBuilder : IVisualBuilder<(double x, double y)>
    {
        public Visual3D Build((double x, double y) item)
        {
            throw new NotImplementedException();
        }
    }
}
