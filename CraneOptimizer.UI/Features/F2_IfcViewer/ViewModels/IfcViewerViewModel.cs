using CraneOptimizer.Domain.Interfaces;
using CraneOptimizer.Domain.Schemas;
using CraneOptimizer.UI.Core.Base;
using CraneOptimizer.UI.Core.Commands;
using CraneOptimizer.UI.Core.Interfaces;
using CraneOptimizer.UI.Features.F2_IfcViewer.DTOs;
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
        private readonly IElementVisibilityFilterService _filterService;
        private readonly IIfcSceneOperationService _ifcSceneOperations;
        private List<PrecastElement>? _pendingIfcElements;
        private IfcLoadOptionsDto? _pendingFilterOptions;
        private bool _isMovingExistingIfc;

        public ICommand LoadIfcCommand { get; }
        public ICommand MergeIfcCommand { get; }
        public ICommand CancelPlacementCommand { get; }
        public ICommand MoveIfcCommand { get; }
        public ICommand DeleteIfcCommand { get; }

        public event Action? IfcModelChanged;

        public string? LoadedIfcFilePath { get; private set; }

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
            ? "Click the new IFC origin location. Press Esc to cancel."
            : "Click to place IFC origin. Press Esc to cancel.";

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
            IElementVisibilityFilterService filterService,
            IIfcSceneOperationService ifcSceneOperations)
        {
            _ifcReader = ifcReader;
            _scene = scene;
            _dialog = dialog;
            _dialogService = dialogService;
            _interaction = interaction;
            _hitTest = hitTest;
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
                LoadedIfcFilePath = path;
                _isMovingExistingIfc = false;
                PendingPlacementPoint = null;
                IsPlacementMode = true;
                OnPropertyChanged(nameof(PlacementPrompt));

                _interaction.PushMode(new Interaction.IfcPlacementMode(
                    this,
                    _hitTest,
                    _interaction));

                Progress = 100;
                Status = $"Loaded {all.Count} elements. Click to place IFC origin.";
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

            var placementOffset = CalculatePlacementOffset(_pendingIfcElements, insertionPoint);

            PendingPlacementPoint = placementOffset;

            var placedElements = _ifcSceneOperations.TranslateElements(
                _pendingIfcElements,
                new DomainPoint3D(placementOffset.X, placementOffset.Y, placementOffset.Z));

            Debug.WriteLine($"IFC Origin Placed At: {insertionPoint.X:F3}, {insertionPoint.Y:F3}, {insertionPoint.Z:F3}");
            _ifcSceneOperations.ReplaceIfcModel(placedElements);
            if (_pendingFilterOptions != null)
                _filterService.ApplyLoadOptions(_pendingFilterOptions);

            _pendingIfcElements = null;
            _pendingFilterOptions = null;
            _isMovingExistingIfc = false;
            IsPlacementMode = false;
            OnPropertyChanged(nameof(PlacementPrompt));
            Status = $"Placed {_filterService.ShowingCount} of {placedElements.Count} elements";
            IfcModelChanged?.Invoke();
            return true;
        }

        public async Task<bool> LoadIfcFromProjectAsync(string filePath, Point3D insertionOffset)
        {
            if (string.IsNullOrWhiteSpace(filePath) || !System.IO.File.Exists(filePath))
                return false;

            try
            {
                IsLoading = true;
                Progress = 0;
                Status = "Restoring IFC...";

                var progress = new Progress<double>(p =>
                {
                    Progress = p;
                    Status = $"Restoring IFC... {p:F0}%";
                });

                var elements = await Task.Run(() => _ifcReader.ReadElements(filePath, progress));
                if (elements == null || elements.Count == 0)
                {
                    Status = "Saved IFC contains no elements.";
                    return false;
                }

                var placedElements = _ifcSceneOperations.TranslateElements(
                    elements,
                    new DomainPoint3D(insertionOffset.X, insertionOffset.Y, insertionOffset.Z));

                _ifcSceneOperations.ReplaceIfcModel(placedElements);
                LoadedIfcFilePath = filePath;
                PendingPlacementPoint = insertionOffset;
                Progress = 100;
                Status = $"Restored {placedElements.Count} IFC elements";
                return true;
            }
            catch (Exception ex)
            {
                Status = $"IFC restore failed: {ex.Message}";
                return false;
            }
            finally
            {
                IsLoading = false;
            }
        }

        public bool MovePlacedIfc(Point3D insertionPoint)
        {
            if (!IsPlacementMode || !_isMovingExistingIfc)
                return false;

            var elements = _ifcSceneOperations.GetElements();
            if (elements.Count == 0)
                return false;

            var currentOffset = PendingPlacementPoint ?? new Point3D(0, 0, 0);
            var currentGround = CalculateModelBaseElevation(elements);
            var delta = new Point3D(
                insertionPoint.X - currentOffset.X,
                insertionPoint.Y - currentOffset.Y,
                insertionPoint.Z - currentGround);

            _ifcSceneOperations.MoveIfcModel(new DomainPoint3D(delta.X, delta.Y, delta.Z));

            PendingPlacementPoint = new Point3D(
                currentOffset.X + delta.X,
                currentOffset.Y + delta.Y,
                currentOffset.Z + delta.Z);

            _isMovingExistingIfc = false;
            IsPlacementMode = false;
            OnPropertyChanged(nameof(PlacementPrompt));
            Status = $"Moved IFC origin to {insertionPoint.X:F2}, {insertionPoint.Y:F2}, {insertionPoint.Z:F2}";
            IfcModelChanged?.Invoke();
            return true;
        }

        private void CancelPlacement()
        {
            _pendingIfcElements = null;
            _pendingFilterOptions = null;
            if (!_isMovingExistingIfc)
            {
                PendingPlacementPoint = null;
                LoadedIfcFilePath = null;
            }
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
                _interaction));

            Status = "Click a new origin location for the IFC model.";
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
            LoadedIfcFilePath = null;
            Progress = 0;
            Status = "IFC model deleted";
            IfcModelChanged?.Invoke();
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

        private static List<PrecastElement> ApplyFilter(
            List<PrecastElement> elements,
            IfcLoadOptionsDto options)
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

        private Point3D CalculatePlacementOffset(
            IReadOnlyList<PrecastElement> sourceElements,
            Point3D insertionPoint)
        {
            var anchor = CalculatePlacementAnchor(sourceElements);
            return new Point3D(
                insertionPoint.X - anchor.X,
                insertionPoint.Y - anchor.Y,
                insertionPoint.Z - anchor.Z);
        }

        private DomainPoint3D CalculatePlacementAnchor(IReadOnlyList<PrecastElement> sourceElements)
        {
            var bounds = CalculateBounds(sourceElements);
            var centerX = (bounds.MinX + bounds.MaxX) * 0.5;
            var centerY = (bounds.MinY + bounds.MaxY) * 0.5;
            var extent = Math.Max(bounds.MaxX - bounds.MinX, bounds.MaxY - bounds.MinY);
            var distanceFromOrigin = Math.Sqrt(centerX * centerX + centerY * centerY);
            var horizontalAnchor = distanceFromOrigin > Math.Max(1000d, extent * 10d)
                ? new DomainPoint3D(centerX, centerY, 0d)
                : new DomainPoint3D(0d, 0d, 0d);

            var groundZ = _ifcReader.ModelGroundElevation ?? bounds.MinZ;
            return new DomainPoint3D(horizontalAnchor.X, horizontalAnchor.Y, groundZ);
        }

        private static double CalculateModelBaseElevation(IReadOnlyList<PrecastElement> elements)
            => CalculateBounds(elements).MinZ;

        private static (double MinX, double MinY, double MinZ, double MaxX, double MaxY, double MaxZ)
            CalculateBounds(IReadOnlyList<PrecastElement> elements)
        {
            double minX = double.MaxValue, minY = double.MaxValue, minZ = double.MaxValue;
            double maxX = double.MinValue, maxY = double.MinValue, maxZ = double.MinValue;

            foreach (var element in elements)
            {
                var cog = element.ElementCOG;
                var halfLength = (element.BBoxLength ?? element.ElementLength ?? 0d) / 2d;
                var halfWidth = (element.BBoxWidth ?? element.ElementWidth ?? 0d) / 2d;
                var halfHeight = (element.BBoxHeight ?? element.ElementHeight ?? 0d) / 2d;

                minX = Math.Min(minX, cog.X - halfLength);
                maxX = Math.Max(maxX, cog.X + halfLength);
                minY = Math.Min(minY, cog.Y - halfWidth);
                maxY = Math.Max(maxY, cog.Y + halfWidth);
                minZ = Math.Min(minZ, cog.Z - halfHeight);
                maxZ = Math.Max(maxZ, cog.Z + halfHeight);
            }

            return minX == double.MaxValue
                ? (0d, 0d, 0d, 0d, 0d, 0d)
                : (minX, minY, minZ, maxX, maxY, maxZ);
        }
    }
}
