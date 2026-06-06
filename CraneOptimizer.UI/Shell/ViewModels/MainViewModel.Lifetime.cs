namespace CraneOptimizer.UI.Shell.ViewModels
{
    public partial class MainViewModel
    {
        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;

            _status.StatusChanged -= OnStatusChanged;
            _status.HistoryChanged -= OnHistoryChanged;
            _originService.OriginChanged -= OnOriginChanged;
            if (_transformService != null)
            {
                _transformService.ReferenceChanged -= OnReferenceChanged;
            }
            _originService.PickingChanged -= OnOriginPickingChanged;
            _craneSceneInteraction.StateChanged -= OnCraneInteractionStateChanged;
            _unitSettings.SettingsChanged -= OnUnitSettingsChanged;
            Scene.ZoneAdded -= OnProjectStateZoneChanged;
            Scene.ZoneRemoved -= OnProjectStateZoneChanged;
            Scene.ZoneUpdated -= OnProjectStateZoneChanged;
            Scene.ZonesCleared -= OnProjectStateChanged;
            Scene.CraneAdded -= OnProjectStateCraneChanged;
            Scene.CraneRemoved -= OnProjectStateCraneChanged;
            Scene.CraneMoved -= OnProjectStateCraneChanged;
            Scene.CraneRotated -= OnProjectStateCraneChanged;
            Scene.CraneUpdated -= OnProjectStateCraneChanged;
            Scene.CranesCleared -= OnProjectStateChanged;
            IfcViewer.IfcModelChanged -= OnProjectStateChanged;
            Dxf.DxfStateChanged -= OnProjectStateChanged;
            _sceneRendering.ElementsCleared -= OnSceneElementsCleared;
            _sceneRendering.ElementsChanged -= OnSceneElementsChanged;
        }
    }
}
