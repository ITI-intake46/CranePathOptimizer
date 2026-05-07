using CraneOptimizer.Domain.Common;
using CraneOptimizer.UI.Core.Interfaces;
using CraneOptimizer.UI.Shell.Interaction;

namespace CraneOptimizer.UI.Shell.Services
{
    public sealed class OriginService : IOriginService
    {
        private readonly IViewportInteractionService _interaction;
        private readonly IViewportHitTestService _hitTest;
        private readonly ICameraService _camera;
        private readonly ISceneService _scene;

        public OriginService(
            IViewportInteractionService interaction,
            IViewportHitTestService hitTest,
            ICameraService camera,
            ISceneService scene)
        {
            _interaction = interaction;
            _hitTest = hitTest;
            _camera = camera;
            _scene = scene;
        }

        public DomainPoint3D? OriginPoint { get; private set; }
        public bool IsPickingOrigin { get; private set; }

        public event Action? OriginChanged;
        public event Action? PickingChanged;

        public void StartPicking()
        {
            if (IsPickingOrigin)
                return;

            IsPickingOrigin = true;
            PickingChanged?.Invoke();

            _interaction.PushMode(new OriginPickingMode(
                this,
                _hitTest,
                _interaction,
                () => _camera.UpdateClipping(_scene.GetElements())));
        }

        public void SetOrigin(DomainPoint3D origin)
        {
            OriginPoint = origin;
            OriginChanged?.Invoke();
            CancelPicking();
        }

        public void ClearOrigin()
        {
            if (OriginPoint == null)
                return;

            OriginPoint = null;
            OriginChanged?.Invoke();
        }

        public void CancelPicking()
        {
            if (!IsPickingOrigin)
                return;

            IsPickingOrigin = false;
            PickingChanged?.Invoke();
        }
    }
}
