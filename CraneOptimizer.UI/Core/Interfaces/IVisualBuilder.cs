using System.Windows.Media.Media3D;

namespace CraneOptimizer.UI.Core.Interfaces
{
    public interface IVisualBuilder<T>
    {
        Visual3D Build(T item);
    }
}
