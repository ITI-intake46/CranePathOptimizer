using CraneOptimizer.Domain.Entities;
using HelixToolkit.Wpf;
using System.Windows.Media.Media3D;

namespace CraneOptimizer.UI.Features.F6_CranePlacement
{
    public class CraneVisualBuilder
    {
        public ModelVisual3D Build(Crane crane)
        {
            var sphere = new SphereVisual3D();

            return sphere;
        }
    }
}
