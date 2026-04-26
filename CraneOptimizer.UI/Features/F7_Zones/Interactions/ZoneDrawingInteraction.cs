using CraneOptimizer.Domain.Common;
using CraneOptimizer.UI.Core.Interfaces;
using CraneOptimizer.UI.Features.F7_Zones.ViewModels;

namespace CraneOptimizer.UI.Features.F7_Zones.Interactions
{
    public class ZoneDrawingInteraction : IViewportInteraction
    {
        private readonly ZoneDrawingViewModel _vm;

        public ZoneDrawingInteraction(ZoneDrawingViewModel vm)
        {
            _vm = vm;
        }

        public void OnClick(DomainPoint3D point)
        {
            if (!_vm.IsDrawing) return;
            _vm.AddPoint(point);
        }

        public void OnMove(DomainPoint3D point)
        {
            if (!_vm.IsDrawing) return;
            _vm.SetHoverPoint(point);
        }
    }
}
