using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Models;
using CraneOptimizer.Domain.Common;
using CraneOptimizer.Domain.Entities;
using CraneOptimizer.Domain.Interfaces;
using CraneOptimizer.Domain.Schemas;
using CraneOptimizer.UI.Core.Base;
using CraneOptimizer.UI.Core.Commands;
using CraneOptimizer.UI.Core.Interfaces;
using CraneOptimizer.UI.Core.Models;
using CraneOptimizer.UI.Features.F2_IfcViewer;
using CraneOptimizer.UI.Features.F2_IfcViewer.ViewModels;
using CraneOptimizer.UI.Features.F0_CoordinateSystem;
using CraneOptimizer.UI.Features.F3_CraneSetup;
using CraneOptimizer.UI.Features.F7_Zones.ViewModels;
using CraneOptimizer.UI.Features.F11_DXF.ViewModels;
using CraneOptimizer.UI.Features.F8_GAOptimizer.Services;
using CraneOptimizer.UI.Features.F8_GAOptimizer.ViewModels;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Windows.Input;

namespace CraneOptimizer.UI.Shell.ViewModels
{
    public class MainViewModel : ViewModelBase, IDisposable
    {
        private readonly IIfcReaderRepository _ifcReader;
        private readonly IRenderingService _render;
        private readonly ICraneRepository _craneRepo;
        private readonly IDialogService _dialog;
        private readonly ICranePlacementService _cranePlacementService;
        private readonly ICoordinateTransformService _coordinateTransform;
        private readonly ICraneValidationService _craneValidationService;
        private readonly List<PrecastElement> _sceneElements = new();

        private Crane? _selectedCrane;
        private bool _disposed;
        private bool _isPickingOrigin;
        private DomainPoint3D? _originPoint;
        private Guid? _currentProjectId;
        private string _currentProjectName = string.Empty;
        private string? _currentProjectIfcPath;
        private bool _showCraneRadius = true;

        public ISceneService Scene { get; }

        public MainViewModel(
            ISceneService scene,
            DrawingViewModel drawing,
            CranePlacementViewModel cranePlacement,
            IfcViewerViewModel ifcViewer,
            DxfTabViewModel dxf,
            IIfcReaderRepository ifcReader,
            IRenderingService render,
            ICraneRepository craneRepo,
            IDialogService dialog,
            ICranePlacementService cranePlacementService,
            ICoordinateTransformService coordinateTransform,
            ICraneValidationService craneValidationService)
        {
            Scene = scene;
            Drawing = drawing;
            CranePlacement = cranePlacement;
            IfcViewer = ifcViewer;
            Dxf = dxf;

            _ifcReader = ifcReader;
            _render = render;
            _craneRepo = craneRepo;
            _dialog = dialog;
            _cranePlacementService = cranePlacementService;
            _coordinateTransform = coordinateTransform;
            _craneValidationService = craneValidationService;

            Scene.ElementsChanged += OnElementsChanged;
            Scene.CraneAdded += OnCranesChanged;
            Scene.CraneMoved += OnCranesChanged;
            Scene.CraneUpdated += OnCranesChanged;
            Scene.CraneRemoved += OnCranesChanged;
            Scene.CranesCleared += OnCranesCleared;
            Scene.ZoneAdded += _ => ReRenderCranes();
            Scene.ZoneUpdated += _ => ReRenderCranes();
            Scene.ZoneRemoved += _ => ReRenderCranes();
            Scene.ZonesCleared += ReRenderCranes;

            OptimizeCommand = new RelayCommand(OpenOptimizerDialog);
            AddMobileCraneCommand = new RelayCommand(AddMobileCraneAndStartPlacement, CanAddCrane);
            RemoveSelectedCraneCommand = new RelayCommand(RemoveSelectedCrane, CanRemoveSelectedCrane);
            RemoveAllCranesCommand = new RelayCommand(RemoveAllCranes, () => Scene.GetCranes().Count > 0);

            _ = LoadCranesAsync();
        }

        public ObservableCollection<PrecastElement> Elements { get; } = new();
        public ObservableCollection<PrecastElement> FilteredElements { get; } = new();
        public ObservableCollection<Crane> Cranes { get; } = new();

        public CranePlacementViewModel CranePlacement { get; }
        public DrawingViewModel Drawing { get; }
        public IfcViewerViewModel IfcViewer { get; }
        public DxfTabViewModel Dxf { get; }

        public ICommand StartDrawingCommand { get; }
        public ICommand OptimizeCommand { get; }
        public ICommand AddMobileCraneCommand { get; }
        public ICommand RemoveSelectedCraneCommand { get; }
        public ICommand RemoveAllCranesCommand { get; }

        public bool ShowCraneRadius
        {
            get => _showCraneRadius;
            set
            {
                if (_showCraneRadius == value) return;
                _showCraneRadius = value;
                OnPropertyChanged();
                ReRenderCranes();
            }
        }

        public Guid? CurrentProjectId
        {
            get => _currentProjectId;
            private set
            {
                _currentProjectId = value;
                OnPropertyChanged();
            }
        }

        public string CurrentProjectName
        {
            get => _currentProjectName;
            private set
            {
                _currentProjectName = value;
                OnPropertyChanged();
            }
        }

        public string? CurrentProjectIfcPath
        {
            get => _currentProjectIfcPath;
            private set
            {
                _currentProjectIfcPath = value;
                OnPropertyChanged();
            }
        }

        public void OpenProject(Guid projectId, string projectName, string? ifcFilePath)
        {
            CurrentProjectId = projectId;
            CurrentProjectName = projectName;
            CurrentProjectIfcPath = ifcFilePath;
            StatusText = $"Project opened: {projectName}";
            HistoryBreadcrumb = "Load IFC when needed.";
        }

        public Crane? SelectedCrane
        {
            get => _selectedCrane;
            set
            {
                _selectedCrane = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(SelectedCraneSpec));

                CranePlacement.SelectedCrane = value;
                StatusText = $"Crane Selected: {value?.Name}";
            }
        }

        public CraneSpecification? SelectedCraneSpec =>
            SelectedCrane?.Specifications.FirstOrDefault();

        private void OpenOptimizerDialog()
        {
            if (CranePlacement.RunOptimizationWorkflow() && CranePlacement.Result != null)
            {
                StatusText = "Optimization Completed";
                HistoryBreadcrumb = $"Best Fitness: {CranePlacement.Result.FitnessScore:F2}";
            }
        }

        private void OnElementsChanged(List<PrecastElement> elements)
        {
            Debug.WriteLine("Scene ElementsChanged: " + elements.Count);

            _sceneElements.Clear();
            _sceneElements.AddRange(elements);

            Elements.Clear();
            FilteredElements.Clear();

            foreach (var e in elements)
            {
                Elements.Add(e);
                FilteredElements.Add(e);
            }

            if (_sceneElements.Count == 0)
            {
                _render.ClearLayer("Elements");
                OriginPoint = null;
                IsPickingOrigin = false;
                IsModelLoaded = false;
                StatusText = "Model cleared";
                return;
            }

            OriginPoint = null;
            IsPickingOrigin = false;

            IsModelLoaded = true;
            ReRenderElements();
            StatusText = $"Model loaded ({elements.Count} elements).";
        }

        private void OnCranesChanged(SceneCrane _) => ReRenderCranes();

        private void OnCranesCleared()
        {
            _render.ClearLayer("Cranes");
            _render.ClearLayer("CraneRadius");
        }

        public bool IsPickingOrigin
        {
            get => _isPickingOrigin;
            set
            {
                if (_isPickingOrigin == value) return;
                _isPickingOrigin = value;
                OnPropertyChanged();
            }
        }

        public DomainPoint3D? OriginPoint
        {
            get => _originPoint;
            private set
            {
                _originPoint = value;
                OnPropertyChanged();
            }
        }

        public void SetOrigin(DomainPoint3D origin)
        {
            OriginPoint = origin;
            IsPickingOrigin = false;
            ReRenderElements();
            ReRenderCranes();
        }

        public void ReRenderElements()
        {
            _render.ClearLayer("Elements");

            if (_sceneElements.Count == 0)
                return;

            Debug.WriteLine("Rendering Elements: " + _sceneElements.Count);

            _render.Render(
                _sceneElements.Select(e => (e, (LiftStatus?)null)),
                new ElementVisualBuilder(OriginPoint),
                "Elements"
            );

            if (OriginPoint != null)
                StatusText = $"Origin set at ({OriginPoint.X:F2}, {OriginPoint.Y:F2}, {OriginPoint.Z:F2}).";
        }

        public void PlaceSelectedCrane(DomainPoint3D point)
        {
            if (SelectedCrane == null)
                return;

            var activeSpec = SelectedCrane.Specifications
                .OrderByDescending(s => s.MaxRadius)
                .FirstOrDefault();

            var scenePoint = _coordinateTransform.ViewportToScene(point);

            var sceneCrane = new SceneCrane
            {
                CraneDefinition = SelectedCrane,
                ActiveSpecification = activeSpec,
                Position = new DomainPoint3D(scenePoint.X, scenePoint.Y, scenePoint.Z),
                Rotation = 0,
                PivotOffset = new DomainPoint3D(0, 0, Math.Max(1.2, SelectedCrane.VehicleHeight)),
                BoomLength = activeSpec != null ? Math.Clamp(activeSpec.MaxBoomLength * 0.6, activeSpec.MinBoomLength, activeSpec.MaxBoomLength) : 20,
                BoomAngle = 45
            };

            sceneCrane.IsValidPlacement = _craneValidationService.ValidatePlacement(sceneCrane, Scene.GetZones());

            Scene.AddCrane(sceneCrane);
            Scene.SetSelectedCrane(sceneCrane);

            StatusText = sceneCrane.IsValidPlacement
                ? $"Crane placed at ({point.X:F2}, {point.Y:F2})."
                : "Crane placed, but invalid: footprint/radius violate zone constraints.";
        }

        public void ReRenderCranes()
        {
            var cranes = Scene.GetCranes();

            foreach (var c in cranes)
                c.IsValidPlacement = _craneValidationService.ValidatePlacement(c, Scene.GetZones());

            _render.Render(cranes, new CraneVisualBuilder(), "Cranes");

            if (ShowCraneRadius)
                _render.Render(cranes, new CraneRadiusBuilder(), "CraneRadius");
            else
                _render.ClearLayer("CraneRadius");
        }

        public double EvaluateCraneFitness(SceneCrane crane)
        {
            return _craneValidationService.EvaluateCraneFitness(
                crane,
                Scene.GetElements(),
                Scene.GetZones());
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

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            Scene.ElementsChanged -= OnElementsChanged;
            Scene.CraneAdded -= OnCranesChanged;
            Scene.CraneMoved -= OnCranesChanged;
            Scene.CraneUpdated -= OnCranesChanged;
            Scene.CraneRemoved -= OnCranesChanged;
            Scene.CranesCleared -= OnCranesCleared;
        }

        private string _coordX = "0.00M";
        public string CoordX
        {
            get => _coordX;
            set { _coordX = value; OnPropertyChanged(); }
        }

        private string _coordY = "0.00M";
        public string CoordY
        {
            get => _coordY;
            set { _coordY = value; OnPropertyChanged(); }
        }

        private string _statusText = "Ready";
        public string StatusText
        {
            get => _statusText;
            set { _statusText = value; OnPropertyChanged(); }
        }

        private string _historyBreadcrumb = "No recent action";
        public string HistoryBreadcrumb
        {
            get => _historyBreadcrumb;
            set { _historyBreadcrumb = value; OnPropertyChanged(); }
        }

        private bool _isModelLoaded;
        public bool IsModelLoaded
        {
            get => _isModelLoaded;
            set { _isModelLoaded = value; OnPropertyChanged(); }
        }

        private string _selectedElementName = string.Empty;
        public string SelectedElementName
        {
            get => _selectedElementName;
            set { _selectedElementName = value; OnPropertyChanged(); }
        }

        private string _renderQualityLabel = "STD";
        public string RenderQualityLabel
        {
            get => _renderQualityLabel;
            set { _renderQualityLabel = value; OnPropertyChanged(); }
        }

        public void SetCoordinates(double x, double y)
        {
            CoordX = $"{x:F2}M";
            CoordY = $"{y:F2}M";
        }

        private async Task LoadCranesAsync()
        {
            var cranes = await _craneRepo.GetAllWithSpecsAsync();
            Cranes.Clear();
            foreach (var c in cranes)
                Cranes.Add(c);

            if (Cranes.Count > 0 && SelectedCrane == null)
                SelectedCrane = Cranes[0];

            CommandManager.InvalidateRequerySuggested();
        }
    }
}
