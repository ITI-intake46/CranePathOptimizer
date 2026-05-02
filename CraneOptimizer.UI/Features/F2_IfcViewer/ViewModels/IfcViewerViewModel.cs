using CraneOptimizer.Domain.Interfaces;
using CraneOptimizer.Domain.Schemas;
using CraneOptimizer.UI.Core.Base;
using CraneOptimizer.UI.Core.Commands;
using CraneOptimizer.UI.Core.Interfaces;
using CraneOptimizer.UI.Features.F2_IfcViewer.Models;
using CraneOptimizer.UI.Features.F2_IfcViewer.Services;
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
        private List<PrecastElement>? _pendingIfcElements;

        public ICommand LoadIfcCommand { get; }
        public ICommand CancelPlacementCommand { get; }

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
            IIfcOptionsDialogService dialogService)
        {
            _ifcReader = ifcReader;
            _scene = scene;
            _dialog = dialog;
            _dialogService = dialogService;

            LoadIfcCommand = new RelayCommand(async () => await LoadIfcAsync(), () => !IsLoading);
            CancelPlacementCommand = new RelayCommand(CancelPlacement, () => IsPlacementMode);
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

                Status = "Filtering...";
                var visible = ApplyFilter(all, filter);

                Debug.WriteLine("IFC Visible Elements: " + visible.Count);

                _pendingIfcElements = visible;
                PendingPlacementPoint = null;
                IsPlacementMode = true;

                Progress = 100;
                Status = $"Loaded {visible.Count} elements. Click to place IFC.";
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

            var minX = _pendingIfcElements.Min(e => e.ElementCOG.X - ((e.BBoxLength ?? 0d) / 2d));
            var minY = _pendingIfcElements.Min(e => e.ElementCOG.Y - ((e.BBoxWidth ?? 0d) / 2d));
            var minZ = _pendingIfcElements.Min(e => e.ElementCOG.Z - ((e.BBoxHeight ?? 0d) / 2d));

            var shiftX = insertionPoint.X - minX;
            var shiftY = insertionPoint.Y - minY;
            var shiftZ = insertionPoint.Z - minZ;

            var placementOffset = new Point3D(shiftX, shiftY, shiftZ);

            PendingPlacementPoint = placementOffset;

            var placedElements = _pendingIfcElements
                .Select(element => TranslateElement(element, placementOffset))
                .ToList();

            Debug.WriteLine($"IFC Placed At: {insertionPoint.X:F3}, {insertionPoint.Y:F3}, {insertionPoint.Z:F3}");
            _scene.SetElements(placedElements);

            _pendingIfcElements = null;
            IsPlacementMode = false;
            Status = $"Placed {placedElements.Count} elements";
            return true;
        }

        private void CancelPlacement()
        {
            _pendingIfcElements = null;
            PendingPlacementPoint = null;
            IsPlacementMode = false;
            Progress = 0;
            Status = "IFC placement cancelled";
        }

        private static PrecastElement TranslateElement(PrecastElement element, Point3D insertionPoint)
        {
            return new PrecastElement
            {
                ElementID = element.ElementID,
                ElementType = element.ElementType,
                ElementLength = element.ElementLength,
                ElementWidth = element.ElementWidth,
                ElementHeight = element.ElementHeight,
                ElementMaterial = element.ElementMaterial,
                ElementWT = element.ElementWT,
                ElementVolume = element.ElementVolume,
                ElementCOG = new Domain.Common.DomainPoint3D(
                    element.ElementCOG.X + insertionPoint.X,
                    element.ElementCOG.Y + insertionPoint.Y,
                    element.ElementCOG.Z + insertionPoint.Z),
                BBoxLength = element.BBoxLength,
                BBoxWidth = element.BBoxWidth,
                BBoxHeight = element.BBoxHeight
            };
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
