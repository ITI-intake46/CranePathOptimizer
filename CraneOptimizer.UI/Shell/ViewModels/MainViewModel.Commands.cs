using CraneOptimizer.Domain.Common;
using CraneOptimizer.Domain.Entities;
using CraneOptimizer.Domain.Entities.Enums;
using CraneOptimizer.UI.Core.Models;
using CraneOptimizer.UI.Features.F8_GAOptimizer.Views;
using System.Diagnostics;
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
                _sceneRendering);

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
            var craneToPlace = SelectedCrane ?? CranePlacement.SelectedCrane;
            if (craneToPlace == null)
            {
                Debug.WriteLine("[Placement] Click ignored: MainViewModel.SelectedCrane and CranePlacement.SelectedCrane are both null.");
                StatusText = "Select a mobile crane from the list first, then click inside the zone.";
                return;
            }

            _craneSceneInteraction.PlaceCrane(craneToPlace, point);
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

        private bool CanAddCrane() => Cranes.Count > 0;

        private void AddMobileCraneAndStartPlacement()
        {
            if (SelectedCrane?.CraneType != CraneType.Mobile && Cranes.Count > 0)
            {
                SelectedCrane =
                    Cranes.FirstOrDefault(crane => crane.CraneType == CraneType.Mobile)
                    ?? Cranes.FirstOrDefault(crane => crane.CraneType == CraneType.Crawler)
                    ?? Cranes[0];
            }

            if (SelectedCrane == null)
            {
                StatusText = "No cranes in the database. Import a crane first.";
                return;
            }

            // If the SelectedCrane setter already placed the crane (elements loaded path),
            // CranePlacement.SelectedCrane is already set and we are done.
            // If no elements were loaded the setter entered placement-mode — keep that behaviour.
            // Either way, set CranePlacement.SelectedCrane so subsequent viewport clicks work.
            CranePlacement.SelectedCrane = SelectedCrane;

            // Auto-place immediately: calculate a sensible position beside the model.
            var defaultPos = CalculateDefaultCranePosition();
            if (defaultPos != null)
            {
                PlaceSelectedCrane(defaultPos);
                StatusText = $"Crane placed at ({defaultPos.X:F2}, {defaultPos.Y:F2}).";
            }
            else
            {
                // No model loaded — place at scene origin so the crane is visible immediately.
                var origin = new DomainPoint3D(0, 0, 0);
                PlaceSelectedCrane(origin);
                StatusText = "Crane placed at origin. Load an IFC model to auto-position.";
            }
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

        public async Task ReloadCranesAsync()
        {
            var selectedCraneId = SelectedCrane?.Id;
            var cranes = await _craneRepo.GetAllWithSpecsAsync();
            Cranes.Clear();

            foreach (var crane in cranes)
                Cranes.Add(crane);

            if (selectedCraneId.HasValue)
            {
                var refreshedSelection = Cranes.FirstOrDefault(crane => crane.Id == selectedCraneId.Value);
                if (refreshedSelection != null && !ReferenceEquals(SelectedCrane, refreshedSelection))
                    SelectedCrane = refreshedSelection;
            }
            else if (Cranes.Count > 0)
            {
                SelectedCrane = Cranes[0];
            }

            CommandManager.InvalidateRequerySuggested();
        }

        private Task LoadCranesAsync() => ReloadCranesAsync();
    }
}
