using CraneOptimizer.Domain.Common;

namespace CraneOptimizer.UI.Core.Interfaces
{
    public interface IOriginService
    {
        DomainPoint3D? OriginPoint { get; }
        bool IsPickingOrigin { get; }

        event Action? OriginChanged;
        event Action? PickingChanged;

        void StartPicking();
        void SetOrigin(DomainPoint3D origin);
        void ClearOrigin();
        void CancelPicking();
    }
}
