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
            _originService.PickingChanged -= OnOriginPickingChanged;
            _craneSceneInteraction.StateChanged -= OnCraneInteractionStateChanged;
            _sceneRendering.ElementsCleared -= OnSceneElementsCleared;
            _sceneRendering.ElementsChanged -= OnSceneElementsChanged;
        }
    }
}
