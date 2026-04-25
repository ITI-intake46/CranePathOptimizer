using CraneOptimizer.Domain.Common;

namespace CraneOptimizer.UI.Core.Interfaces
{
    public interface IViewportInteraction
    {
        void OnClick(DomainPoint3D point);
        void OnMove(DomainPoint3D point);
    }
}
