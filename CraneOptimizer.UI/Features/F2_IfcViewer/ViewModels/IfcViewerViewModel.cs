using CraneOptimizer.Domain.Interfaces;
using CraneOptimizer.Domain.Schemas;
using CraneOptimizer.UI.Core.Base;
using CraneOptimizer.UI.Core.Commands;
using CraneOptimizer.UI.Core.Interfaces;
using CraneOptimizer.UI.Features.F2_IfcViewer.Models;
using CraneOptimizer.UI.Features.F2_IfcViewer.Services;
using CraneOptimizer.UI.Features.F16_Filter.Services;
using CraneOptimizer.Domain.Common;
using System.Diagnostics;
using System.Windows.Input;
using System.Windows.Media.Media3D;

namespace CraneOptimizer.UI.Features.F2_IfcViewer.ViewModels
{
    public class IfcViewerViewModel : ViewModelBase
    {
        private readonly IIfcReaderRepository _ifcReader;
        private readonly ISceneService _scene;
        private readonly IIfcOptionsDialogService _dialogService;
        private readonly IDialogService _dialog;
        private readonly IViewportInteractionService _interaction;
        private readonly IViewportHitTestService _hitTest;
        private readonly ICameraService _camera;
        private readonly IElementVisibilityFilterService _filterService;
        private readonly IIfcSceneOperationService _ifcSceneOperations;
        private List<PrecastElement>? _pendingIfcElements;
        private IfcFilterOptions? _pendingFilterOptions;
        private bool _isMovingExistingIfc;

        public ICommand LoadIfcCommand { get; }
        public ICommand MergeIfcCommand { get; }
        public ICommand CancelPlacementCommand { get; }
        public ICommand MoveIfcCommand { get; }
        public ICommand DeleteIfcCommand { get; }

        private bool _isLoading;
        public bool IsLoading
        {
            get => _isLoading;
            set { _isLoading = value; OnPropertyChanged(); }
        }

        private string _status = "Idle";
        public string Status
        {
            get => _status;
            set { _status = value; OnPropertyChanged(); }
        }

        private double _progress;
        public double Progress
        {
            get => _progress;
            set { _progress = value; OnPropertyChanged(); }
        }

        private bool _isPlacementMode;
        public bool IsPlacementMode
        {
            get => _isPlacementMode;
            private set
            {
                _isPlacementMode = value;
                OnPropertyChanged();
                CommandManager.InvalidateRequerySuggested();
            }
        }

        public string PlacementPrompt => _isMovingExistingIfc
            ? "Click where the building bottom-left corner should move. Press Esc to cancel."
            : "Click where the building bottom-left corner should be placed. Press Esc to cancel.";

        private Point3D? _pendingPlacementPoint;
        public Point3D? PendingPlacementPoint
        {
            get => _pendingPlacementPoint;
            private set
            {
                _pendingPlacementPoint = value;
                OnPropertyChanged();
            }
        }

        public IfcViewerViewModel(
            IIfcReaderRepository ifcReader,
            ISceneService scene,
            IDialogService dialog,
            IIfcOptionsDialogService dialogService,
            IViewportInteractionService interaction,
            IViewportHitTestService hitTest,
            ICameraService camera,
            IElementVisibilityFilterService filterService,
            IIfcSceneOperationService ifcSceneOperations)
        {
            _ifcReader = ifcReader;
            _scene = scene;
            _dialog = dialog;
            _dialogService = dialogService;
            _interaction = interaction;
            _hitTest = hitTest;
            _camera = camera;
            _filterService = filterService;
            _ifcSceneOperations = ifcSceneOperations;

            LoadIfcCommand = new RelayCommand(async () => await LoadIfcAsync(), () => !IsLoading && !IsPlacementMode);
            MergeIfcCommand = new RelayCommand(ShowMergeIfcPlaceholder, () => !IsLoading && !IsPlacementMode);
            CancelPlacementCommand = new RelayCommand(CancelPlacement, () => IsPlacementMode);
            MoveIfcCommand = new RelayCommand(StartMoveIfc, CanEditPlacedIfc);
            DeleteIfcCommand = new RelayCommand(DeleteIfc, CanEditPlacedIfc);
            _scene.ElementsChanged += OnSceneElementsChanged;
        }

        private async Task LoadIfcAsync()
        {
            try
            {
                IsLoading = true;
                Progress = 0;
                Status = "Selecting file...";

                var path = _dialog.OpenFile("IFC (*.ifc)|*.ifc");
                if (string.IsNullOrEmpty(path)) return;

                var filter = _dialogService.Show(path);
                if (filter == null) return;

                Status = "Reading IFC...";

                var progress = new Progress<double>(p =>
                {
                    Progress = p;
                    Status = $"Reading IFC... {p:F0}%";
                });

                var all = await Task.Run(() =>
                    _ifcReader.ReadElements(path, progress));

                Debug.WriteLine("IFC Loaded: " + (all?.Count ?? 0));

                if (all == null || !all.Any())
                {
                    _dialog.ShowMessage("No elements found.");
                    return;
                }

                _pendingIfcElements = all;
                _pendingFilterOptions = filter;
                _isMovingExistingIfc = false;
                PendingPlacementPoint = null;
                IsPlacementMode = true;
                OnPropertyChanged(nameof(PlacementPrompt));

                _interaction.PushMode(new Interaction.IfcPlacementMode(
                    this,
                    _hitTest,
                    _interaction,
                    () => _camera.UpdateClipping(_ifcSceneOperations.GetElements())));

                Progress = 100;
                Status = $"Loaded {all.Count} elements. Click to place IFC.";
            }
            catch (Exception ex)
            {
                _dialog.ShowMessage(ex.Message);
            }
            finally
            {
                IsLoading = false;
            }
        }

        public bool PlacePendingIfc(Point3D insertionPoint)
        {
            if (!IsPlacementMode || _pendingIfcElements == null || _pendingIfcElements.Count == 0)
                return false;

            var boundsAnchor = CalculateBoundsAnchor(_pendingIfcElements);
            var placementOffset = new Point3D(
                insertionPoint.X - boundsAnchor.X,
                insertionPoint.Y - boundsAnchor.Y,
                insertionPoint.Z - boundsAnchor.Z);

            PendingPlacementPoint = placementOffset;

            var placedElements = _ifcSceneOperations.TranslateElements(
                _pendingIfcElements,
                new DomainPoint3D(placementOffset.X, placementOffset.Y, placementOffset.Z));

            Debug.WriteLine($"IFC Placed Bottom-Left Anchor At: {insertionPoint.X:F3}, {insertionPoint.Y:F3}, {insertionPoint.Z:F3}");
            _ifcSceneOperations.ReplaceIfcModel(placedElements);
            if (_pendingFilterOptions != null)
                _filterService.ApplyLoadOptions(_pendingFilterOptions);

            _pendingIfcElements = null;
            _pendingFilterOptions = null;
            _isMovingExistingIfc = false;
            IsPlacementMode = false;
            OnPropertyChanged(nameof(PlacementPrompt));
            Status = $"Placed {_filterService.ShowingCount} of {placedElements.Count} elements from bottom-left anchor";
            return true;
        }

        public bool MovePlacedIfc(Point3D insertionPoint)
        {
            if (!IsPlacementMode || !_isMovingExistingIfc)
                return false;

            var elements = _ifcSceneOperations.GetElements();
            if (elements.Count == 0)
                return false;

            var currentBoundsAnchor = CalculateBoundsAnchor(elements);
            var delta = new Point3D(
                insertionPoint.X - currentBoundsAnchor.X,
                insertionPoint.Y - currentBoundsAnchor.Y,
                insertionPoint.Z - currentBoundsAnchor.Z);

            var movedElements = _ifcSceneOperations.MoveIfcModel(new DomainPoint3D(delta.X, delta.Y, delta.Z));

            var currentOffset = PendingPlacementPoint ?? new Point3D(0, 0, 0);
            PendingPlacementPoint = new Point3D(
                currentOffset.X + delta.X,
                currentOffset.Y + delta.Y,
                currentOffset.Z + delta.Z);
            _camera.UpdateClipping(movedElements);

            _isMovingExistingIfc = false;
            IsPlacementMode = false;
            OnPropertyChanged(nameof(PlacementPrompt));
            Status = $"Moved IFC bottom-left anchor to {insertionPoint.X:F2}, {insertionPoint.Y:F2}, {insertionPoint.Z:F2}";
            return true;
        }

        private void CancelPlacement()
        {
            _pendingIfcElements = null;
            _pendingFilterOptions = null;
            if (!_isMovingExistingIfc)
                PendingPlacementPoint = null;
            _isMovingExistingIfc = false;
            IsPlacementMode = false;
            OnPropertyChanged(nameof(PlacementPrompt));
            Progress = 0;
            Status = "IFC placement cancelled";
        }

        private void StartMoveIfc()
        {
            if (!CanEditPlacedIfc())
                return;

            _pendingIfcElements = null;
            _pendingFilterOptions = null;
            _isMovingExistingIfc = true;
            IsPlacementMode = true;
            OnPropertyChanged(nameof(PlacementPrompt));

            _interaction.PushMode(new Interaction.IfcMoveMode(
                this,
                _hitTest,
                _interaction,
                () => _camera.UpdateClipping(_ifcSceneOperations.GetElements())));

            Status = "Click a new location for the IFC model.";
        }

        private void DeleteIfc()
        {
            if (!CanEditPlacedIfc())
                return;

            _pendingIfcElements = null;
            _pendingFilterOptions = null;
            _isMovingExistingIfc = false;
            PendingPlacementPoint = null;
            IsPlacementMode = false;
            OnPropertyChanged(nameof(PlacementPrompt));

            _ifcSceneOperations.DeleteIfcModel();
            Progress = 0;
            Status = "IFC model deleted";
        }

        private void ShowMergeIfcPlaceholder()
        {
            Status = "Merge IFC is available in the ribbon for multi-building workflow, but shared multi-model geometry loading is not implemented yet.";
        }

        private bool CanEditPlacedIfc() => !IsLoading && !IsPlacementMode && _ifcSceneOperations.HasIfcModel;

        private void OnSceneElementsChanged(List<PrecastElement> elements)
        {
            CommandManager.InvalidateRequerySuggested();
        }

        private static Point3D CalculateBoundsAnchor(IReadOnlyList<PrecastElement> elements)
        {
            if (elements.Count == 0)
                return new Point3D();

            var minX = elements.Min(element => element.ElementCOG.X - GetHalfDimension(element.BBoxLength ?? element.ElementLength));
            var minY = elements.Min(element => element.ElementCOG.Y - GetHalfDimension(element.BBoxWidth ?? element.ElementWidth));
            var minZ = elements.Min(element => element.ElementCOG.Z - GetHalfDimension(element.BBoxHeight ?? element.ElementHeight));

            return new Point3D(minX, minY, minZ);
        }

        private static double GetHalfDimension(double? value)
        {
            if (!value.HasValue || double.IsNaN(value.Value) || double.IsInfinity(value.Value) || value.Value <= 0)
                return 0d;

            return value.Value / 2d;
        }

        private static List<PrecastElement> ApplyFilter(
            List<PrecastElement> elements,
            IfcFilterOptions options)
        {
            return elements.Where(e =>
            {
                var t = e.ElementType ?? "";

                if (options.HideGrids && t.Contains("IfcGrid", StringComparison.OrdinalIgnoreCase)) return false;
                if (options.HideLevels && t.Contains("IfcBuildingStorey", StringComparison.OrdinalIgnoreCase)) return false;
                if (options.HideAnnotations && t.Contains("IfcAnnotation", StringComparison.OrdinalIgnoreCase)) return false;
                if (options.HideSite && t.Contains("IfcSite", StringComparison.OrdinalIgnoreCase)) return false;
                if (options.HideSpaces && t.Contains("IfcSpace", StringComparison.OrdinalIgnoreCase)) return false;
                if (options.HideOpenings && t.Contains("IfcOpeningElement", StringComparison.OrdinalIgnoreCase)) return false;
                if (options.HideVirtualElements && t.Contains("IfcVirtualElement", StringComparison.OrdinalIgnoreCase)) return false;

                return true;
            }).ToList();
        }
    }
}
