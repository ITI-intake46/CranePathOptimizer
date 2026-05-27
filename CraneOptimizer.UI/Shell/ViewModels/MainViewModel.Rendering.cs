using CraneOptimizer.Domain.Common;
using CraneOptimizer.Domain.Schemas;
using CraneOptimizer.UI.Core.Interfaces;

namespace CraneOptimizer.UI.Shell.ViewModels
{
    public partial class MainViewModel
    {
        public bool ShowCraneRadius
        {
            get => _sceneRendering.ShowCraneRadius;
            set
            {
                if (_sceneRendering.ShowCraneRadius == value)
                    return;

                _sceneRendering.ShowCraneRadius = value;
                OnPropertyChanged();
                QueueProjectStateSave();
            }
        }

        public bool IsPickingOrigin
        {
            get => _originService.IsPickingOrigin;
            set
            {
                if (_originService.IsPickingOrigin == value)
                    return;

                if (value)
                    _originService.StartPicking();
                else
                    _originService.CancelPicking();
            }
        }

        public DomainPoint3D? OriginPoint => _originService.OriginPoint;

        public void SetOrigin(DomainPoint3D origin)
        {
            _originService.SetOrigin(origin);
            ReRenderElements();
            ReRenderCranes();
            StatusText = $"Origin set at ({origin.X:F2}, {origin.Y:F2}, {origin.Z:F2}).";
        }

        public void ReRenderElements() => _sceneRendering.ReRenderElements();
        public void ReRenderCranes() => _sceneRendering.ReRenderCranes();

        private void ConfigureSceneRendering()
        {
            _sceneRendering.Configure(new SceneRenderingContext
            {
                GetOriginPoint = () => OriginPoint,
                GetPlacementOffset = GetPlacementOffset,
                GetLiftResults = () => CranePlacement.SelectedPosition?.ElementResults,
                GetSelectedElementId = () => ElementManagement.SelectedElement?.ElementID,
                GetZones = () => Scene.GetZones(),
                GetGAViewMode = () => CranePlacement.CurrentGAViewMode
            });

            _sceneRendering.ElementsCleared += OnSceneElementsCleared;
            _sceneRendering.ElementsChanged += OnSceneElementsChanged;
        }

        private DomainPoint3D? GetPlacementOffset()
        {
            var pendingPlacement = IfcViewer.PendingPlacementPoint;
            return pendingPlacement.HasValue
                ? new DomainPoint3D(
                    pendingPlacement.Value.X,
                    pendingPlacement.Value.Y,
                    pendingPlacement.Value.Z)
                : null;
        }

        private void OnSceneElementsCleared()
        {
            _originService.ClearOrigin();
            _originService.CancelPicking();
            IsModelLoaded = false;
            BuildingArea = 0;
            BuildingHeight = 0;
            BuildingName = "No Building Loaded";
            StatusText = "Model cleared";
        }

        private void OnSceneElementsChanged(IReadOnlyList<PrecastElement> elements)
        {
            _originService.ClearOrigin();
            _originService.CancelPicking();
            IsModelLoaded = true;
            RefreshBuildingMetrics();
            BuildingName = CurrentProjectName;
            StatusText = $"Model loaded ({elements.Count} elements).";
        }

        private void OnOriginChanged()
        {
            OnPropertyChanged(nameof(OriginPoint));
            QueueProjectStateSave();
        }

        private void OnOriginPickingChanged()
        {
            OnPropertyChanged(nameof(IsPickingOrigin));
        }
    }
}
