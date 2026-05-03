using CraneOptimizer.Domain.Common;
using CraneOptimizer.Domain.Entities;
using CraneOptimizer.UI.Core.Models;
using CraneOptimizer.UI.Features.F8_GAOptimizer.Views;
using System.Windows.Input;

namespace CraneOptimizer.UI.Shell.ViewModels
{
    public partial class MainViewModel
    {
        private void OpenOptimizerDialog()
        {
            var dialog = new CraneOptimizerDialog(
                _cranePlacementService,
                _render,
                _dialog,
                _craneRepo,
                Scene,
                CraneSetup,
                _ifcReader,
                OriginPoint,
                GetPlacementOffset());

            if (System.Windows.Application.Current?.MainWindow != null)
                dialog.Owner = System.Windows.Application.Current.MainWindow;

            dialog.ShowDialog();

            if (dialog.LatestResult != null && dialog.AppliedPosition != null)
            {
                CranePlacement.Result = dialog.LatestResult;
                CranePlacement.SelectedPosition = dialog.AppliedPosition;
                ReRenderElements();

                _status.Set("Optimization Applied", $"Best Fitness: {dialog.LatestResult.FitnessScore:F2}");
            }
        }

        public void PlaceSelectedCrane(DomainPoint3D point)
        {
            if (SelectedCrane == null)
                return;

            _craneSceneInteraction.PlaceCrane(SelectedCrane, point);
        }

        public double EvaluateCraneFitness(SceneCrane crane)
        {
            return _craneSceneInteraction.EvaluateFitness(crane);
        }

        public SceneCrane? SelectedSceneCrane
        {
            get => _craneSceneInteraction.SelectedSceneCrane;
            set
            {
                _craneSceneInteraction.SelectSceneCrane(value);
            }
        }

        public bool IsDraggingCrane
        {
            get => _craneSceneInteraction.IsDraggingCrane;
            private set { }
        }

        public DomainPoint3D? DragOffset
        {
            get => _craneSceneInteraction.DragOffset;
            private set { }
        }

        public void StartDrag(DomainPoint3D clickPoint) => _craneSceneInteraction.StartDrag(clickPoint);

        public void DragCrane(DomainPoint3D worldPoint) => _craneSceneInteraction.DragCrane(worldPoint);

        public void EndDrag() => _craneSceneInteraction.EndDrag();

        private void OnCraneInteractionStateChanged()
        {
            OnPropertyChanged(nameof(SelectedSceneCrane));
            OnPropertyChanged(nameof(IsDraggingCrane));
            OnPropertyChanged(nameof(DragOffset));
        }

        private bool CanAddCrane() => SelectedCrane != null;

        private void AddMobileCraneAndStartPlacement()
        {
            CranePlacement.SelectedCrane = SelectedCrane;
            CranePlacement.StartPlacementCommand.Execute(null);
            StatusText = "Click in viewport to place crane center.";
        }

        private bool CanRemoveSelectedCrane() => Scene.SelectedCrane != null;

        private void RemoveSelectedCrane()
        {
            var selected = Scene.SelectedCrane;
            if (selected == null)
                return;

            Scene.RemoveCrane(selected);
            StatusText = "Selected crane removed.";
        }

        private void RemoveAllCranes()
        {
            Scene.ClearCranes();
            StatusText = "All cranes removed.";
        }

        private async Task LoadCranesAsync()
        {
            var cranes = await _craneRepo.GetAllWithSpecsAsync();
            Cranes.Clear();

            foreach (var crane in cranes)
                Cranes.Add(crane);

            if (Cranes.Count > 0 && SelectedCrane == null)
                SelectedCrane = Cranes[0];

            CommandManager.InvalidateRequerySuggested();
        }
    }
}
