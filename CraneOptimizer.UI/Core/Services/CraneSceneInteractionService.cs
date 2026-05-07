using CraneOptimizer.Domain.Common;
using CraneOptimizer.Domain.Entities;
using CraneOptimizer.UI.Core.Interfaces;
using CraneOptimizer.UI.Core.Models;

namespace CraneOptimizer.UI.Core.Services
{
    public sealed class CraneSceneInteractionService : ICraneSceneInteractionService
    {
        private readonly ISceneService _scene;
        private readonly ICoordinateTransformService _coordinateTransform;
        private readonly ICraneValidationService _craneValidation;
        private readonly IStatusService _status;

        public CraneSceneInteractionService(
            ISceneService scene,
            ICoordinateTransformService coordinateTransform,
            ICraneValidationService craneValidation,
            IStatusService status)
        {
            _scene = scene;
            _coordinateTransform = coordinateTransform;
            _craneValidation = craneValidation;
            _status = status;
        }

        public SceneCrane? SelectedSceneCrane { get; private set; }
        public bool IsDraggingCrane { get; private set; }
        public DomainPoint3D? DragOffset { get; private set; }

        public event Action? StateChanged;

        public void SelectSceneCrane(SceneCrane? crane)
        {
            SelectedSceneCrane = crane;
            _scene.SetSelectedCrane(crane);
            StateChanged?.Invoke();
        }

        public void PlaceCrane(Crane crane, DomainPoint3D point)
        {
            var activeSpec = crane.Specifications?
                .OrderByDescending(spec => spec.MaxRadius)
                .FirstOrDefault();

            var scenePoint = _coordinateTransform.ViewportToScene(point);

            var sceneCrane = new SceneCrane
            {
                CraneDefinition = crane,
                ActiveSpecification = activeSpec,
                Position = new DomainPoint3D(scenePoint.X, scenePoint.Y, scenePoint.Z),
                Rotation = 0,
                PivotOffset = new DomainPoint3D(0, 0, Math.Max(1.2, crane.VehicleHeight)),
                BoomLength = activeSpec != null
                    ? Math.Clamp(activeSpec.MaxBoomLength * 0.6, activeSpec.MinBoomLength, activeSpec.MaxBoomLength)
                    : 20,
                BoomAngle = 45
            };

            sceneCrane.IsValidPlacement = _craneValidation.ValidatePlacement(sceneCrane, _scene.GetZones());

            _scene.AddCrane(sceneCrane);
            SelectSceneCrane(sceneCrane);

            if (activeSpec == null)
            {
                _status.SetStatus(
                    sceneCrane.IsValidPlacement
                        ? $"Crane placed at ({point.X:F2}, {point.Y:F2}) with default boom values (no load chart found)."
                        : "Crane placed with default boom values, but invalid: footprint/radius violate zone constraints.");
            }
            else
            {
                _status.SetStatus(sceneCrane.IsValidPlacement
                    ? $"Crane placed at ({point.X:F2}, {point.Y:F2})."
                    : "Crane placed, but invalid: footprint/radius violate zone constraints.");
            }
        }

        public double EvaluateFitness(SceneCrane crane)
        {
            return _craneValidation.EvaluateCraneFitness(
                crane,
                _scene.GetElements(),
                _scene.GetZones());
        }

        public void StartDrag(DomainPoint3D clickPoint)
        {
            var crane = SelectedSceneCrane;
            if (crane == null)
                return;

            var scenePoint = _coordinateTransform.ViewportToScene(clickPoint);
            DragOffset = new DomainPoint3D(
                crane.Position.X - scenePoint.X,
                crane.Position.Y - scenePoint.Y,
                crane.Position.Z - scenePoint.Z);

            IsDraggingCrane = true;
            _status.SetStatus("Dragging crane...");
            StateChanged?.Invoke();
        }

        public void DragCrane(DomainPoint3D worldPoint)
        {
            if (!IsDraggingCrane)
                return;

            var crane = SelectedSceneCrane;
            var offset = DragOffset;
            if (crane == null || offset == null)
                return;

            var scenePoint = _coordinateTransform.ViewportToScene(worldPoint);
            var target = new DomainPoint3D(
                scenePoint.X + offset.X,
                scenePoint.Y + offset.Y,
                crane.Position.Z);

            crane.Position = target;
            crane.IsValidPlacement = _craneValidation.ValidatePlacement(crane, _scene.GetZones());
            _scene.UpdateCrane(crane);
        }

        public void EndDrag()
        {
            if (!IsDraggingCrane)
                return;

            IsDraggingCrane = false;
            DragOffset = null;

            var crane = SelectedSceneCrane;
            if (crane != null)
            {
                crane.IsValidPlacement = _craneValidation.ValidatePlacement(crane, _scene.GetZones());
                _scene.UpdateCrane(crane);
                _status.SetStatus(crane.IsValidPlacement
                    ? "Crane drag finished."
                    : "Crane drag finished: invalid placement.");
            }
            else
            {
                _status.SetStatus("Crane drag finished.");
            }

            StateChanged?.Invoke();
        }
    }
}
