using CraneOptimizer.Domain.Common;
using CraneOptimizer.UI.Core.Interfaces;
using CraneOptimizer.UI.Features.F6_CranePlacement.ViewModels;

namespace CraneOptimizer.UI.Features.F6_CranePlacement.Interactions
{
    public class CranePlacementInteraction : IViewportInteraction
    {
        private readonly CranePlacementViewModel _vm;

        public CranePlacementInteraction(CranePlacementViewModel vm)
        {
            _vm = vm;
        }

        public void OnClick(DomainPoint3D point)
        {
            if (!_vm.IsPlacing) return;
            _vm.SetPlacementPoint(point);
        }

        public void OnMove(DomainPoint3D point)
        {
            if (!_vm.IsPlacing) return;
            _vm.SetPreviewPoint(point);
        }
    }
}
